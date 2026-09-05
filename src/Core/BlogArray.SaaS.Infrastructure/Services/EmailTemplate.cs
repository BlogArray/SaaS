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

    void TwoFactorCode(string toEmail, string name, string code);

    void TenantWelcome(string toEmail, string tenantName, string tenantUrl, string clientSecret, string apiKey);

    void ApiKeyRotated(string toEmail, string tenantName, string apiKey, string rotatedBy);
}

public class EmailTemplate(IEmailHelper emailHelper, IConfiguration configuration) : IEmailTemplate
{
    private static readonly string nextLine = "<br>";
    private static readonly string newLine = $"{nextLine}{nextLine}";
    private static readonly string footer = $"{newLine}Thanks,{nextLine}The App Team";

    public void ConfirmEmail(string toEmail, string name, string callbackUrl)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"Welcome to App, your platform for connecting with mentors and advancing your developer skills through live 1:1 mentoring!{newLine}" +
            $"To kickstart your journey, we invite you to verify your email address. Just click the link below:" +
            $"{MakeLinkButton(callbackUrl, "Verify Email")}" +
            $"With your email verified, you'll unlock a treasure trove of features, including:" +
            $"{MakeList(["Booking sessions with experienced mentors", "Participation in engaging coding challenges and hackathons", "Direct access to top-tier mentors for personalized guidance"])}" +
            $"Our team is here to support you every step of the way. Should you have any questions or need assistance, please don't hesitate to reach out to us at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App. We're thrilled to have you on board and can't wait to see the amazing contributions you'll make to our community!";

        string body = GenerateEmail(name, template);

        Send(toEmail, "?? Welcome to App! Please verify your email address", body);
    }

    public void EmailVerified(string toEmail, string name)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"Congratulations! Your email address has been successfully verified for your App account on {DateTime.UtcNow} UTC.{newLine}" +
            $"Now that your email is verified, you can enjoy full access to all the features and benefits of App, " +
            $"including connecting with mentors, participating in coding challenges, and engaging with the community.{newLine}" +
            $"If you have any questions or need assistance, feel free to reach out to our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for verifying your email address and Thank you for choosing App.";

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

    public void TwoFactorCode(string toEmail, string name, string code)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"Use the verification code below to complete your sign-in to App. The code expires in a few minutes " +
            $"and can only be used once.{newLine}" +
            $"<div style=\"font-size:26px;font-weight:700;letter-spacing:8px;text-align:center;padding:14px;background-color:#f5f5f5;\">{Encode(code)}</div>" +
            $"If you did not request this code, someone may be trying to access your account - please reset your password immediately by clicking {MakeLink(StringExtensions.MakeUrl(configuration["Links:Identity"], "forgotpassword"), "Reset Password Link")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(Encode(code), template);

        Send(toEmail, $"Your App verification code: {Encode(code)}", body);
    }

    public void PasswordChangeSuccessed(string toEmail, string name)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to inform you that the password for your App account has been successfully changed on {DateTime.UtcNow} UTC.{newLine}" +
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
            $"To complete this change, please click the link below to verify your new email address:{newLine}" +
            $"If you did not request this change, please disregard this email.{newLine}" +
            $"To complete the email change process, please click the link below:" +
            $"{MakeLinkButton(callbackUrl, "Change email")}" +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Verify your new email address for App Account", body);
    }

    public void ChangeEmailConfirmation(string toEmail, string name, string newEmail)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to confirm that the email address associated with your App account has been successfully updated to {Encode(newEmail)} on {DateTime.UtcNow} UTC.{newLine}" +
            $"If you did not initiate this change, please contact our support team immediately at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your email address has been successfully updated - App", body);
    }

    public void ChangeUsernameConfirmation(string toEmail, string name, string from, string to)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"This is to confirm that the username associated with your App account has been successfully changed from {Encode(from)} to {Encode(to)} on {DateTime.UtcNow} UTC.{newLine}" +
            $"If you did not initiate this change, please reset your password immediately by clicking {MakeLink(StringExtensions.MakeUrl(configuration["Links:Identity"], "account/forgotpassword"), "Reset Password Link")}. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your App usernamae has been changed", body);
    }

    public void InviteWithPasswordLink(string toEmail, string name, string callbackUrl, string org, string orgUrl, string invitedBy)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"You are invited to join {Encode(org)} on App. " +
            $"To get started, please set up your account by creating a password using the link below:" +
            $"{MakeLinkButton(callbackUrl, "Change password")}" +
            $"If you’ve already set a password, you can log in directly to your account here:" +
            $"{MakeLinkButton(orgUrl, "Login")}" +
            $"Once inside, you’ll gain access to your organization’s resources. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"We’re excited to have you on board!";

        string body = GenerateEmail(name, template);

        Send(toEmail, $"You're Invited to Join {Encode(org)} on App", body);
    }

    public void Invite(string toEmail, string name, string org, string orgUrl, string invitedBy)
    {
        string template = $"Hey {Encode(name)}!{newLine}" +
            $"You are invited to join {Encode(org)} on App. " +
            $"You can log in directly to your account here:" +
            $"{MakeLinkButton(orgUrl, "Login")}" +
            $"Once inside, you’ll gain access to your organization’s resources. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"We’re excited to have you on board!";

        string body = GenerateEmail(name, template);

        Send(toEmail, $"You're Invited to Join {Encode(org)} on App", body);
    }


    public void TenantWelcome(string toEmail, string tenantName, string tenantUrl, string clientSecret, string apiKey)
    {
        string template = $"Hey there!{newLine}" +
            $"The tenant {Encode(tenantName)} has been created and you are listed as its administrator.{newLine}" +
            $"Keep the credentials below safe - they will not be shown or sent again:{newLine}" +
            $"Client secret:{MakeSecretBox(clientSecret)}" +
            $"API key:{MakeSecretBox(apiKey)}" +
            $"The tenant is available at {MakeLink(tenantUrl, tenantUrl)}.{newLine}" +
            $"If you were not expecting this email, please contact our support team immediately.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(tenantName, template);

        Send(toEmail, $"Your tenant {Encode(tenantName)} is ready - App", body);
    }

    public void ApiKeyRotated(string toEmail, string tenantName, string apiKey, string rotatedBy)
    {
        string template = $"Hey there!{newLine}" +
            $"The API key for the tenant {Encode(tenantName)} has been rotated by {Encode(rotatedBy)} on {DateTime.UtcNow} UTC.{newLine}" +
            $"The previous API key is no longer valid. Use the new API key below - it will not be shown or sent again:{newLine}" +
            $"{MakeSecretBox(apiKey)}" +
            $"If you did not request this change, please contact our support team immediately.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(tenantName, template);

        Send(toEmail, $"The API key for {Encode(tenantName)} has been rotated - App", body);
    }

    private static string MakeSecretBox(string secret)
    {
        return $"<div style=\"font-family:Consolas,monospace;font-size:14px;padding:10px;background-color:#f5f5f5;border-radius:6px;word-break:break-all;margin:6px 0 12px 0;\">{Encode(secret)}</div>";
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

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
        html += $"<img src=\"{BlogArrayConstants.DefaultLogoUrl}\" alt=\"App Logo\" class=\"logo\" />";
        html += "</div>";
        html += body;
        html += footer;
        html += "</div>";
        html += "</body>";
        html += "</html>";
        return html;
    }

    private void Send(string toEmail, string subject, string body) => emailHelper.SendEmail(toEmail, subject, body);

}
