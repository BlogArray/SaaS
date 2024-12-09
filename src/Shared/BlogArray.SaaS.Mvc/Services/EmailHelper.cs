using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using BlogArray.SaaS.Mvc.ViewModels;

namespace BlogArray.SaaS.Mvc.Services;

public interface IEmailHelper
{
    void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[] attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false);
}

public class EmailHelper(IOptions<SmtpConfiguration> options) : IEmailHelper
{
    private readonly SmtpConfiguration Smtp = options.Value;

    /// <summary>
    /// Sends an Email
    /// </summary>
    /// <param name="fromEmail">From whicj email this need to be sent ref: AppSettings.</param>
    /// <param name="toEmail">Contains the recipients of this email message.</param>
    /// <param name="subject">Subject line for this email message.</param>
    /// <param name="body">The message body.</param>
    /// <param name="bccEMail">Collection that contains the blind carbon copy (BCC) recipients for this email message.</param>
    public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[] attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false)
    {
        MailAddress fromAddress = new(Smtp.FromEmail, Smtp.FromName);

        MailAddress toAddress = new(toEmail);
        bccEMails = bccEMails.Trim();

        SmtpClient smtp = new()
        {
            Host = Smtp.Host,
            Port = Smtp.Port,
            EnableSsl = Smtp.EnableSsl,
            Credentials = new NetworkCredential(Smtp.Username, Smtp.Password)
        };

        using MailMessage message = new(fromAddress, toAddress)
        {
            Subject = subject,
            IsBodyHtml = true,
            Body = body
        };

        if (clearToBeforeSend)
        {
            message.To.Clear();
        }

        if (!string.IsNullOrEmpty(bccEMails))
        {
            foreach (string bccEmailId in bccEMails.Split(','))
            {
                message.Bcc.Add(new MailAddress(bccEmailId));
            }
        }

        if (attachment != null && attachment.Length > 0)
        {
            message.Attachments.Add(new Attachment(new MemoryStream(attachment, false), fileName, fileType));
        }

        try
        {
            smtp.Send(message);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets Email body string
    /// </summary>
    /// <param name="userName">Username of receptent</param>
    /// <param name="message">Main message to user</param>
    /// <param name="title">Title of context</param>
    /// <param name="type">Type of context</param>
    /// <returns>HTML String</returns>
    public static string GetEmailBody(string userName, string message, string title, string type)
    {
        //string body = @$"Hi {userName},<br><br>{message}";
        //string returnBody = RawHtmlHelper.GetEmailForVerifications(title, body, userName, type);
        //return returnBody;
        return "";
    }
}