using System.Text;
using System.Text.Json;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public EmailService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task SendPasswordResetEmail(string toEmail, string toName, string username, string newPassword)
    {
        var inboundApiKey = _configuration["Email:InboundApiKey"] ?? throw new InvalidOperationException("Email:InboundApiKey not configured");
        var fromEmail = _configuration["Email:FromEmail"] ?? throw new InvalidOperationException("Email:FromEmail not configured");
        var fromName = _configuration["Email:FromName"] ?? "UTXO E-Mail Agent";

        var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>New Password Generated</h2>
                    <p>Hello {toName},</p>
                    <p>A new password has been generated for your account.</p>
                    <p><strong>Username:</strong> {username}</p>
                    <p><strong>New Password:</strong> <code style='background: #f4f4f4; padding: 5px 10px; border-radius: 3px;'>{newPassword}</code></p>
                    <p>Please change this password after your first login.</p>
                    <br/>
                    <p>Best regards,<br/>UTXO E-Mail Agent Team</p>
                </body>
                </html>
            ";

        var textBody = $@"
New Password Generated

Hello {toName},

A new password has been generated for your account.

Username: {username}
New Password: {newPassword}

Please change this password after your first login.

Best regards,
UTXO E-Mail Agent Team
            ";

        var emailPayload = new
        {
            from = $"{fromName} <{fromEmail}>",
            to = toEmail,
            subject = "Your New Password - UTXO E-Mail Agent",
            html = htmlBody,
            text = textBody
        };

        var jsonContent = JsonSerializer.Serialize(emailPayload);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://inbound.new/api/e2/emails");
            request.Headers.Add("Authorization", $"Bearer {inboundApiKey}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[EmailService] Error sending email via Inbound API: {response.StatusCode} - {errorContent}");
                throw new Exception($"Failed to send email via Inbound API: {response.StatusCode}");
            }

            Console.WriteLine($"[EmailService] Email sent successfully to {toEmail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] Error sending email: {ex.Message}");
            throw;
        }
    }
}
