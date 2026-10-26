//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Infrastructure.Services;

/// <summary>
/// An email waiting to be sent by <see cref="EmailDispatcherService"/>.
/// </summary>
public sealed record QueuedEmail(string ToEmail, string Subject, string Body, string BccEmails, byte[]? Attachment, string FileName, string FileType, bool ClearToBeforeSend);

/// <summary>
/// In-memory queue between requests and the email dispatcher. Bounded, so a mail outage can't
/// grow memory without limit: when full, new emails are dropped and logged.
/// </summary>
public class EmailQueue(ILogger<EmailQueue> logger)
{
    private const int Capacity = 1000;

    private readonly Channel<QueuedEmail> channel = Channel.CreateBounded<QueuedEmail>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    public ChannelReader<QueuedEmail> Reader => channel.Reader;

    /// <summary>
    /// Emails waiting to be sent.
    /// </summary>
    public int Count => channel.Reader.Count;

    public void Enqueue(QueuedEmail email)
    {
        if (!channel.Writer.TryWrite(email))
        {
            logger.LogError("The email queue is full ({Capacity}); the email '{Subject}' was dropped.", Capacity, email.Subject);
        }
    }
}

/// <summary>
/// Sends email after the request instead of during it. With synchronous SMTP, an endpoint that
/// emails only existing accounts (forgot password, resend confirmation) answered measurably slower
/// for them, revealing which addresses have accounts, and every alert added SMTP latency to the
/// action. Callers keep using <see cref="IEmailHelper"/> and <see cref="EmailTemplate"/> unchanged;
/// failures surface in the log rather than as exceptions.
/// </summary>
public class BackgroundEmailHelper(EmailQueue queue) : IEmailHelper
{
    public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[]? attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false)
    {
        queue.Enqueue(new QueuedEmail(toEmail, subject, body, bccEMails, attachment, fileName, fileType, clearToBeforeSend));
    }
}

/// <summary>
/// Sends queued emails through SMTP (<see cref="ISmtpEmailSender"/>). A failed send is retried once
/// after a short delay, then logged and dropped.
/// </summary>
public class EmailDispatcherService(EmailQueue queue, ISmtpEmailSender smtp, ILogger<EmailDispatcherService> logger) : BackgroundService
{
    /// <summary>
    /// Delay before the single retry of a failed send.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (QueuedEmail email in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(email, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (queue.Count > 0)
        {
            logger.LogWarning("The application is stopping with {Count} queued email(s) not sent.", queue.Count);
        }
    }

    /// <summary>
    /// Sends one email, retrying once. Never throws (except for cancellation).
    /// </summary>
    public async Task SendAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                smtp.SendEmail(email.ToEmail, email.Subject, email.Body, email.BccEmails, email.Attachment, email.FileName, email.FileType, email.ClearToBeforeSend);
                return;
            }
            catch (Exception ex) when (attempt == 1)
            {
                logger.LogWarning(ex, "Sending the email '{Subject}' failed; retrying once.", email.Subject);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The email '{Subject}' could not be sent and was dropped.", email.Subject);
            }
        }
    }
}
