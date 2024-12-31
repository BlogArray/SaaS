using BlogArray.SaaS.Mvc.Extensions;
using Microsoft.Extensions.Configuration;
using System.Text.Encodings.Web;

namespace BlogArray.SaaS.Mvc.Services;

public interface IEmailTemplate
{
    void ConfirmEmail(string toEmail, string name, string callbackUrl);

    void EmailVerified(string toEmail, string name);

    void ForgotPassword(string toEmail, string name, string callbackUrl);

    void PasswordChangeSuccessed(string toEmail, string name);

    void ChangeEmail(string toEmail, string name, string callbackUrl, string newEmail);

    void ChangeEmailConfirmation(string toEmail, string name, string newEmail);

    void ChangeUsernameConfirmation(string toEmail, string name, string from, string to);

    void Invite(string toEmail, string name, string callbackUrl, string org, string orgUrl, string invitedBy);
}

public class EmailTemplate(IEmailHelper emailHelper, IConfiguration configuration) : IEmailTemplate
{
    private static readonly string nextLine = "<br>";
    private static readonly string newLine = $"{nextLine}{nextLine}";
    private static readonly string footer = $"{newLine}Thanks,{nextLine}The App Team";

    public void ConfirmEmail(string toEmail, string name, string callbackUrl)
    {
        string template = $"Hey {name}!{newLine}" +
            $"Welcome to App, your platform for connecting with mentors and advancing your developer skills through live 1:1 mentoring!{newLine}" +
            $"To kickstart your journey, we invite you to verify your email address. Just click the link below:" +
            $"{MakeLinkButton(callbackUrl, "Verify Email")}" +
            $"With your email verified, you'll unlock a treasure trove of features, including:" +
            $"{MakeList(["Booking sessions with experienced mentors", "Participation in engaging coding challenges and hackathons", "Direct access to top-tier mentors for personalized guidance"])}" +
            $"Our team is here to support you every step of the way. Should you have any questions or need assistance, please don't hesitate to reach out to us at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App. We're thrilled to have you on board and can't wait to see the amazing contributions you'll make to our community!";

        string body = GenerateEmail(name, template);

        Send(toEmail, "🚀 Welcome to App! Please verify your email address", body);
    }

    public void EmailVerified(string toEmail, string name)
    {
        string template = $"Hey {name}!{newLine}" +
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
        string template = $"Hey {name}!{newLine}" +
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
        string template = $"Hey {name}!{newLine}" +
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
        string template = $"Hey {name}!{newLine}" +
            $"We have received a request to change the email address associated with your App account to {newEmail}. " +
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
        string template = $"Hey {name}!{newLine}" +
            $"This is to confirm that the email address associated with your App account has been successfully updated to {newEmail} on {DateTime.UtcNow} UTC.{newLine}" +
            $"If you did not initiate this change, please contact our support team immediately at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your email address has been successfully updated - App", body);
    }

    public void ChangeUsernameConfirmation(string toEmail, string name, string from, string to)
    {
        string template = $"Hey {name}!{newLine}" +
            $"This is to confirm that the username associated with your App account has been successfully changed from {from} to {to} on {DateTime.UtcNow} UTC.{newLine}" +
            $"If you did not initiate this change, please reset your password immediately by clicking {MakeLink(StringExtensions.MakeUrl(configuration["Links:Identity"], "account/forgotpassword"), "Reset Password Link")}. " +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"Thank you for choosing App.";

        string body = GenerateEmail(name, template);

        Send(toEmail, "Your App usernamae has been changed", body);
    }

    public void Invite(string toEmail, string name, string callbackUrl, string org, string orgUrl, string invitedBy)
    {
        string template = $"Hey {name}!{newLine}" +
            $"{invitedBy} has invited you to join {org} on App." +
            $"To get started, please set up your account by creating a password using the link below:" +
            $"{MakeLinkButton(callbackUrl, "Change password")}" +
            $"If you’ve already set a password, you can log in directly to your account here:" +
            $"{MakeLinkButton(orgUrl, "Login")}" +
            $"Once inside, you’ll gain access to your organization’s resources." +
            $"If you have any questions or concerns, please contact our support team at {MakeLink("mailto:support@app.com", "support@app.com")}.{newLine}" +
            $"We’re excited to have you on board!";

        string body = GenerateEmail(name, template);

        Send(toEmail, $"You're Invited to Join {org} on App", body);
    }


    private static string MakeLink(string link, string name)
    {
        return $"<a href=\"{HtmlEncoder.Default.Encode(link)}\">{name}</a>";
    }

    private static string MakeLinkButton(string link, string name)
    {
        return $"{newLine}<a href=\"{HtmlEncoder.Default.Encode(link)}\" class=\"btn\">{name}</a>{newLine}";
    }

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
        html += $"<title>{title}</title>";
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
        html += "text-align: center;";
        html += "margin-bottom: 20px;";
        html += "}";
        html += "";
        html += ".logo {";
        html += "display: inline-block;";
        html += "max-width: 100%;";
        html += "height: auto;";
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

    private void Send(string toEmail, string subject, string body)
    {
        emailHelper.SendEmail(toEmail, subject, body);
    }

}