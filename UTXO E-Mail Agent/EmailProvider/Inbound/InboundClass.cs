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
        var requestUrl = _apiUrl + $"emails?limit=50&offset=0&type=received&status=unread&time_range=1h&address={agent.Emailaddress}";
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "UTXO-EmailAgent/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");

        var response = await httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonConvert.DeserializeObject<ListEmailsFromInboundClass>(content);
            
            if (res != null && res.Data != null && res.Data.Any())
            {
                foreach (var listNewEmailsClass in res.Data.OrEmptyIfNull())
                {
                    var m = await (from a in _db.Conversations
                            where a.AgentId == agent.Id && a.Messageid == listNewEmailsClass.Id
                            select a).AsNoTracking().FirstOrDefaultAsync();
                    if (m != null)
                    {
                        listNewEmailsClass.SetAlreadyRead();
                    }
                }
                
                return res.ToListNewEmailsClass();
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError($"[Inbound] GetEmailsAsync: API error - Status: {response.StatusCode}, Response: {errorContent}");
        }

        return null;
    }

    public async Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        var requestUrl = _apiUrl + $"emails/{email.Id}";
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "UTXO-EmailAgent/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");

        var response = await httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonConvert.DeserializeObject<GetEmailByIdFromInboundClass>(content);
            
            if (res != null)
            {
                return res.ToMailsClass();
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError($"[Inbound] GetMail: API error for email '{email.Id}' - Status: {response.StatusCode}, Response: {errorContent}");
        }

        return null;
    }

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent, Conversation? conversation)
    {
        // Determine if we can use reply (need valid Inbound message ID) or must send new email
        var canReply = mail.HasValidInboundId;
        var messageIdForReply = !string.IsNullOrEmpty(mail.OriginalMessageId) && mail.OriginalMessageId.StartsWith("inbnd_")
            ? mail.OriginalMessageId
            : mail.Id;

        // Prepare attachments: Remove path field (only for local use)
        var attachmentsForApi = emailResponse.Attachments;
        if (attachmentsForApi != null && attachmentsForApi.Length > 0)
        {
            foreach (var att in attachmentsForApi)
            {
                att.Path = null;
            }
        }

        string jsonPayload;
        string requestUrl;

        if (canReply)
        {
            // Use reply endpoint
            var replyResponse = new ReplyToEmailInboundClass
            {
                From = agent.Emailaddress,
                Subject = emailResponse.EmailResponseSubject,
                Html = emailResponse.EmailResponseHtml,
                Text = emailResponse.EmailResponseText,
                Attachments = attachmentsForApi,
            };
            jsonPayload = JsonConvert.SerializeObject(replyResponse, Formatting.Indented);
            requestUrl = _apiUrl + $"emails/{messageIdForReply}/reply";
            Logger.Log($"[Inbound] Using REPLY to message: {messageIdForReply}");
        }
        else
        {
            // Use send endpoint (new email)
            var sendRequest = new
            {
                from = agent.Emailaddress,
                to = new[] { mail.From }, // Send to original sender
                subject = emailResponse.EmailResponseSubject,
                html = emailResponse.EmailResponseHtml,
                text = emailResponse.EmailResponseText,
                attachments = attachmentsForApi,
            };
            jsonPayload = JsonConvert.SerializeObject(sendRequest, Formatting.Indented);
            requestUrl = _apiUrl + "emails";
            Logger.Log($"[Inbound] Using SEND (no valid Inbound ID available, original ID: {mail.Id})");
        }

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

        var payloadPreview = jsonPayload.Length > 500
            ? jsonPayload.Substring(0, 500) + "..."
            : jsonPayload;
        Logger.Log($"[Inbound] Sending POST to: {requestUrl}");
        Logger.Log($"[Inbound] Payload Preview:\n{payloadPreview}");

        // Retry with exponential backoff: 5s, 15s, 30s
        const int maxRetries = 3;
        int[] delaysSeconds = [5, 15, 30];

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
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
            Logger.LogError($"  Request URL: {requestUrl}");

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