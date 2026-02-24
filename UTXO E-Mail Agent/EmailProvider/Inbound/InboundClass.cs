using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.EmailProvider.Inbound;

public class InboundClass : IEmailProvider
{
    private readonly IConfiguration _config;
   private string _apiUrl;
   private string _bearerToken;
   private DefaultdbContext _db;

    public InboundClass(IConfiguration config, DefaultdbContext dbContext)
    {
        _config = config;
        _db = dbContext;
         _apiUrl = _config["Email:ApiUrl"] ?? throw new InvalidOperationException("Email:ApiUrl not configured");
         _bearerToken = _config["Email:BearerToken"] ?? throw new InvalidOperationException("Email:BearerToken not configured");
    }

    public async Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiUrl + $"emails?limit=50&offset=0&type=received&status=unread&time_range=1h&address={agent.Emailaddress}");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");

        var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonConvert.DeserializeObject<ListEmailsFromInboundClass>(content);
            
            if (res!=null && res.Data.Any())
            {
                foreach (var listNewEmailsClass in res.Data.OrEmptyIfNull())
                {
                   var m=await (from a in _db.Conversations
                           where a.AgentId==agent.Id && a.Messageid==listNewEmailsClass.Id
                               select a).AsNoTracking().FirstOrDefaultAsync();
                   if (m!=null)
                       listNewEmailsClass.SetAlreadyRead();
                }
                return res.ToListNewEmailsClass();
            }
        }

        return null;
    }

    public async Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiUrl + $"emails/{email.Id}");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");

        var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonConvert.DeserializeObject<GetEmailByIdFromInboundClass>(content);
            
            if (res!=null)
            {
                return res.ToMailsClass();
            }
        }

        return null;
    }

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent, Conversation? conversation)
    {
        // Prepare attachments: Remove path field (only for local use)
        var attachmentsForApi = emailResponse.Attachments;
        if (attachmentsForApi != null && attachmentsForApi.Length > 0)
        {
            foreach (var att in attachmentsForApi)
            {
                att.Path = null;
            }
        }

        ReplyToEmailInboundClass replyResponse = new ReplyToEmailInboundClass
        {
            From = agent.Emailaddress,
            Subject = emailResponse.EmailResponseSubject,
            Html = emailResponse.EmailResponseHtml,
            Text = emailResponse.EmailResponseText,
            Attachments = attachmentsForApi,
        };

        // Debug: Attachment-Struktur loggen
        if (emailResponse.Attachments != null && emailResponse.Attachments.Length > 0)
        {
            Logger.Log($"[Inbound] Sending {emailResponse.Attachments.Length} attachment(s):");
            for (int i = 0; i < emailResponse.Attachments.Length; i++)
            {
                var att = emailResponse.Attachments[i];
                Logger.Log($"  [{i + 1}] Filename: {att.Filename}");
                Logger.Log($"      ContentType: {att.ContentType}");
                Logger.Log($"      Content Length: {att.Content?.Length ?? 0} chars");
                Logger.Log($"      Path: {att.Path}");
            }
        }

        var jsonPayload = JsonConvert.SerializeObject(replyResponse, Formatting.Indented);

        var payloadPreview = jsonPayload.Length > 500
            ? jsonPayload.Substring(0, 500) + "..."
            : jsonPayload;
        Logger.Log($"[Inbound] Sending POST to: {_apiUrl}emails/{mail.Id}/reply");
        Logger.Log($"[Inbound] Payload Preview:\n{payloadPreview}");

        // Retry with exponential backoff: 5s, 15s, 30s
        const int maxRetries = 3;
        int[] delaysSeconds = [5, 15, 30];

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl + $"emails/{mail.Id}/reply");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");
            request.Content = new StringContent(jsonPayload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Logger.Log($"[Inbound] Email sent successfully! Status: {response.StatusCode}" +
                    (attempt > 1 ? $" (attempt {attempt})" : ""));
                Sentemail se = new Sentemail()
                {
                    Created = DateTime.UtcNow,
                    ConversationId = conversation?.Id,
                    Emailreceiver = mail.From,
                    Emailtext = emailResponse.EmailResponseText,
                    Subject = emailResponse.EmailResponseSubject,
                };
                _db.Sentemails.Add(se);
                await _db.SaveChangesAsync();
                return;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            Logger.LogError($"[Inbound] ERROR sending email (attempt {attempt}/{maxRetries}):");
            Logger.LogError($"  Status Code: {response.StatusCode}");
            Logger.LogError($"  Response Body: {errorBody}");
            Logger.LogError($"  Request URL: {_apiUrl}emails/{mail.Id}/reply");

            if (attempt < maxRetries)
            {
                var delay = delaysSeconds[attempt - 1];
                Logger.Log($"[Inbound] Retrying in {delay} seconds...");
                await Task.Delay(delay * 1000);
            }
        }

        Logger.LogError($"[Inbound] All {maxRetries} attempts failed for email {mail.Id}");
    }
}