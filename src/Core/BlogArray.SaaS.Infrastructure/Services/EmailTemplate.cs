//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text.Encodings.Web;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Infrastructure.Services;

public interface IEmailTemplate
{
    void ConfirmEmail(string toEmail, string name, string callbackUrl);

    void EmailVerified(string toEmail, string name);

    void ForgotPassword(string toEmail, string name, string callbackUrl);

    void PasswordChangeSuccessed(string toEmail, string name);

    void ChangeEmail(string toEmail, string name, string callbackUrl, string newEmail);

    void ChangeEmailConfirmation(string toEmail, string name, string newEmail);

    void ChangeUsernameConfirmation(string toEmail, string name, string from, string to);

    void InviteWithPasswordLink(string toEmail, string name, string callbackUrl, string org, string orgUrl, string invitedBy);

    void Invite(string toEmail, string name, string org, string orgUrl, string invitedBy);

    void TenantWelcome(string toEmail, string tenantName, string tenantUrl, string clientSecret, string apiKey);

    /// <summary>
    /// Tells the user an administrator changed their account. <paramref name="advice"/> is the
    /// closing guidance; when omitted, the generic "reset your password if unexpected" advice is
    /// used, which only fits actions that leave the user able to sign in.
    /// </summary>
    Task SecurityActionByAdminNotice(string toEmail, string name, string action, string? advice = null);

    Task MfaResetByAdminNotice(string toEmail, string name);

    void ApiKeyRotated(string toEmail, string tenantName, string apiKey, string rotatedBy);

    void ClientSecretRotated(string toEmail, string tenantName, string clientSecret, string rotatedBy);
}

public class EmailTemplate(IEmailHelper emailHelper, IConfiguration configuration) : IEmailTemplate
{
    private static readonly string nextLine = "<br>";
    private static readonly string newLine = $"{nextLine}{nextLine}";
    private static readonly string footer = $"{newLine}Thanks,{nextLine}The App Team";

    public void ConfirmEmail(string toEmail, string name, string callbackUrl)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"Welcome to App. Please confirm that this is your email address by clicking the link below:" +
            $"{MakeLinkButton(callbackUrl, "Verify email")}" +
            $"You can sign in once your email address is confirmed.{newLine}" +
            $"If you did not create an App account, you can ignore this email: the account cannot be used without confirmation.{newLine}" +
            $"If you have any questions, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Welcome to App - please verify your email address", body);
    }

    public void EmailVerified(string toEmail, string name)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"Congratulations! Your email address has been successfully verified for your App account on {Now()}.{newLine}" +
            $"You can now sign in to App with this email address.{newLine}" +
            $"If you have any questions or need assistance, feel free to reach out to our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your email address has been successfully verified - App", body);
    }

    public void ForgotPassword(string toEmail, string name, string callbackUrl)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"You are receiving this email because a request to change your password for your App account has been initiated. " +
            $"If you did not request this change, please disregard this email.{newLine}" +
            $"To complete the password change process, please click the link below:" +
            $"{MakeLinkButton(callbackUrl, "Change password")}" +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Change your App Account password", body);
    }

    public void PasswordChangeSuccessed(string toEmail, string name)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to inform you that the password for your App account has been successfully changed on {Now()}.{newLine}" +
            $"If you did not initiate this change, please reset your password immediately by clicking {MakeLink(StringExtensions.MakeUrl(configuration["Links:Identity"], "forgotpassword"), "Reset Password Link")}. " +
            $"We also recommend reviewing your account for any unauthorized activity.{newLine}" +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your App Account password has been changed", body);
    }

    public void ChangeEmail(string toEmail, string name, string callbackUrl, string newEmail)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"We have received a request to change the email address associated with your App account to {Encode(newEmail)}. " +
            $"If you did not request this change, please disregard this email.{newLine}" +
            $"To verify your new email address and complete the change, please click the link below:" +
            $"{MakeLinkButton(callbackUrl, "Change email")}" +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Verify your new email address for App Account", body);
    }

    public void ChangeEmailConfirmation(string toEmail, string name, string newEmail)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to confirm that the email address associated with your App account has been successfully updated to {Encode(newEmail)} on {Now()}.{newLine}" +
            $"If you did not initiate this change, please contact our support team immediately at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your email address has been successfully updated - App", body);
    }

    public void ChangeUsernameConfirmation(string toEmail, string name, string from, string to)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to confirm that the username associated with your App account has been successfully changed from {Encode(from)} to {Encode(to)} on {Now()}.{newLine}" +
            $"If you did not initiate this change, please reset your password immediately by clicking {MakeLink(StringExtensions.MakeUrl(configuration["Links:Identity"], "forgotpassword"), "Reset Password Link")}. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your App username has been changed", body);
    }

    public void InviteWithPasswordLink(string toEmail, string name, string callbackUrl, string org, string orgUrl, string invitedBy)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"{Encode(Inviter(invitedBy))} invited you to join {Encode(org)} on App. " +
            $"To get started, please set up your account by creating a password using the link below:" +
            $"{MakeLinkButton(callbackUrl, "Create password")}" +
            (string.IsNullOrWhiteSpace(orgUrl) ? string.Empty : $"If you’ve already set a password, you can log in directly to your account here:{MakeLinkButton(orgUrl, "Login")}") +
            $"Once inside, you’ll gain access to your organization’s resources. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"We’re excited to have you on board!";

        string body = GenerateEmail(name, template);

        Send(toEmail, $"You're Invited to Join {org} on App", body);
    }

    public void Invite(string toEmail, string name, string org, string orgUrl, string invitedBy)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"{Encode(Inviter(invitedBy))} invited you to join {Encode(org)} on App. " +
            (string.IsNullOrWhiteSpace(orgUrl) ? $"You can sign in with your existing App account.{newLine}" : $"You can log in directly to your account here:{MakeLinkButton(orgUrl, "Login")}") +
            $"Once inside, you’ll gain access to your organization’s resources. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"We’re excited to have you on board!";

        string body = GenerateEmail(name, template);

        Send(toEmail, $"You're Invited to Join {org} on App", body);
    }


    public void TenantWelcome(string toEmail, string tenantName, string tenantUrl, string clientSecret, string apiKey)
    {
        string template = $"Hey there!{newLine}" +
            $"The tenant {Encode(tenantName)} has been created and you are listed as its administrator.{newLine}" +
            $"Keep the credentials below safe - they will not be shown or sent again:{newLine}" +
            $"Client secret:{MakeSecretBox(clientSecret)}" +
            $"API key:{MakeSecretBox(apiKey)}" +
            (string.IsNullOrWhiteSpace(tenantUrl) ? string.Empty : $"The tenant is available at {MakeLink(tenantUrl, tenantUrl)}.{newLine}") +
            $"If you were not expecting this email, please contact our support team immediately.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(tenantName, template);

        Send(toEmail, $"Your tenant {tenantName} is ready - App", body);
    }

    public Task SecurityActionByAdminNotice(string toEmail, string name, string action, string? advice = null)
    {
        advice ??= "If you were not expecting this change, please reset your password immediately and contact our support team.";

        string template = $"Hey {Encode(name)}!{newLine}" +
            $"An administrator has performed the following action on your App account on {Now()}:{newLine}" +
            $"{Encode(action)}{newLine}" +
            $"{Encode(advice)}{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "A security action was performed on your App account", body);

        return Task.CompletedTask;
    }

    public Task MfaResetByAdminNotice(string toEmail, string name)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"An administrator has reset the multi-factor authentication on your App account on {Now()}. " +
            $"Your authenticator enrollment has been cleared - you will be asked to set it up again at next sign-in.{newLine}" +
            $"If you did not request this reset, please reset your password immediately and contact our support team.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your App account multi-factor authentication has been reset by an administrator", body);

        return Task.CompletedTask;
    }

    public void ApiKeyRotated(string toEmail, string tenantName, string apiKey, string rotatedBy)
    {
        string template = $"Hey there!{newLine}" +
            $"The API key for the tenant {Encode(tenantName)} has been rotated by {Encode(rotatedBy)} on {Now()}.{newLine}" +
            $"The previous API key is no longer valid. Use the new API key below - it will not be shown or sent again:{newLine}" +
            $"{MakeSecretBox(apiKey)}" +
            $"If you did not request this change, please contact our support team immediately.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(tenantName, template);

        Send(toEmail, $"The API key for {tenantName} has been rotated - App", body);
    }

    public void ClientSecretRotated(string toEmail, string tenantName, string clientSecret, string rotatedBy)
    {
        string template = $"Hey there!{newLine}" +
            $"The client secret for the tenant {Encode(tenantName)} has been rotated by {Encode(rotatedBy)} on {Now()}.{newLine}" +
            $"The previous client secret no longer works. Any application that signs in with it must be updated with the new secret below - it will not be shown or sent again:{newLine}" +
            $"{MakeSecretBox(clientSecret)}" +
            $"If you did not request this change, please contact our support team immediately.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(tenantName, template);

        Send(toEmail, $"The client secret for {tenantName} has been rotated - App", body);
    }


    private static string MakeSecretBox(string secret)
    {
        return $"<div style=\"font-family:Consolas,monospace;font-size:14px;padding:10px;background-color:#f5f5f5;border-radius:6px;word-break:break-all;margin:6px 0 12px 0;\">{Encode(secret)}</div>";
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

    /// <summary>
    /// The current time for an email body, in one fixed format whatever the server's culture.
    /// </summary>
    private static string Now() => DateTime.UtcNow.ToString("d MMMM yyyy 'at' HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Who sent an invitation, for the opening line ("Jane invited you..."); falls back to
    /// "An administrator". A phrase ("the Acme administrator") is capitalised as it starts the
    /// sentence; an email address is left as it is.
    /// </summary>
    private static string Inviter(string? invitedBy)
    {
        string inviter = string.IsNullOrWhiteSpace(invitedBy) ? "An administrator" : invitedBy.Trim();

        return inviter.Contains('@') ? inviter : char.ToUpperInvariant(inviter[0]) + inviter[1..];
    }

    private static string MakeLink(string link, string name) => $"<a href=\"{HtmlEncoder.Default.Encode(link)}\">{Encode(name)}</a>";

    private static string MakeLinkButton(string link, string name) => $"{newLine}<a href=\"{HtmlEncoder.Default.Encode(link)}\" class=\"btn\">{Encode(name)}</a>{newLine}";

    private static string MakeList(List<string> list)
    {
        string listItem = "<ul>";

        foreach (string item in list)
        {
            listItem += $"<li>{item}</li>";
        }

        listItem += $"</ul>";

        return listItem;
    }

    private static string GenerateEmail(string title, string body)
    {
        string html = "<!DOCTYPE html>";
        html += "<html lang=\"en\">";
        html += "<head>";
        html += "<meta charset=\"UTF-8\" />";
        html += "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />";
        html += "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />";
        html += $"<title>{Encode(title)}</title>";
        html += "<style>";
        html += "body {";
        html += "font-family: -apple-system,system-ui,BlinkMacSystemFont,Segoe UI,SegoeUI,Helvetica Neue,sans-serif;";
        html += "margin: 0;";
        html += "padding: 0;";
        html += "}";
        html += "";
        html += ".container {";
        html += "max-width: 600px;";
        html += "margin: 0 auto;";
        html += "padding: 20px;";
        html += "}";
        html += "";
        html += ".btn {";
        html += "display: inline-block;";
        html += "padding: .4rem 1rem;";
        html += "background-color: #6A42C2 !important;";
        html += "color: #ffffff !important;";
        html += "text-decoration: none;";
        html += "border-radius: 6px;";
        html += "}";
        html += "";
        html += "a, a:hover {color: #6A42C2;}";
        html += "";
        html += ".logo-container {";
        html += "text-align: left;";
        html += "margin-bottom: 20px;";
        html += "}";
        html += "";
        html += ".logo {";
        html += "display: inline-block;";
        html += "max-width: 100%;";
        html += "height: 50px;";
        html += "}";
        html += "</style>";
        html += "</head>";
        html += "";
        html += "<body>";
        html += "<div class=\"container\">";
        html += "<div class=\"logo-container\">";
        html += $"<img src=\"{HtmlEncoder.Default.Encode(BlogArrayConstants.DefaultLogoUrl ?? string.Empty)}\" alt=\"App Logo\" class=\"logo\" />";
        html += "</div>";
        html += body;
        html += footer;
        html += "</div>";
        html += "</body>";
        html += "</html>";
        return html;
    }

    /// <summary>
    /// The subject is a plain-text header: values are not HTML-encoded there (a tenant named
    /// "R&D" showed as "R&amp;D"), and line breaks, which a header cannot contain, are removed.
    /// </summary>
    private void Send(string toEmail, string subject, string body) =>
        emailHelper.SendEmail(toEmail, subject.Replace('\r', ' ').Replace('\n', ' '), body);

}
