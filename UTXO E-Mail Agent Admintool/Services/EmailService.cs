using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendPasswordResetEmail(string toEmail, string toName, string username, string newPassword)
    {
        var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
        var smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
        var fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
        var fromName = _configuration["Email:FromName"] ?? "NMKR E-Mail Agent";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Your New Password - NMKR E-Mail Agent";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>New Password Generated</h2>
                    <p>Hello {toName},</p>
                    <p>A new password has been generated for your account.</p>
                    <p><strong>Username:</strong> {username}</p>
                    <p><strong>New Password:</strong> <code style='background: #f4f4f4; padding: 5px 10px; border-radius: 3px;'>{newPassword}</code></p>
                    <p>Please change this password after your first login.</p>
                    <br/>
                    <p>Best regards,<br/>NMKR E-Mail Agent Team</p>
                </body>
                </html>
            ",
            TextBody = $@"
New Password Generated

Hello {toName},

A new password has been generated for your account.

Username: {username}
New Password: {newPassword}

Please change this password after your first login.

Best regards,
NMKR E-Mail Agent Team
            "
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUsername, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] Error sending email: {ex.Message}");
            throw;
        }
    }
}
