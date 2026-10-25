//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Identity.Infrastructure;

public static class EmailSending
{
    /// <summary>
    /// Sends an email without letting an SMTP failure fail the request. Mail is sent
    /// synchronously and the helper throws on any SMTP error, so a notice sent after an action
    /// already succeeded (password changed, email verified) turned that success into an error
    /// page. Returns false when the email could not be sent; the failure is logged.
    /// </summary>
    public static bool TrySend(ILogger logger, Action send, string emailKind)
    {
        try
        {
            send();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send the {EmailKind} email.", emailKind);
            return false;
        }
    }
}
