using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;

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

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod("POST"), _apiUrl + $"emails/{mail.Id}/reply");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");

        // Attachments vorbereiten: Path-Feld entfernen (nur für lokale Verwendung)
        var attachmentsForApi = emailResponse.Attachments;
        if (attachmentsForApi != null && attachmentsForApi.Length > 0)
        {
            foreach (var att in attachmentsForApi)
            {
                // Path auf null setzen - wird nicht an API geschickt
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
            Console.WriteLine($"[Inbound] Sende {emailResponse.Attachments.Length} Attachment(s):");
            for (int i = 0; i < emailResponse.Attachments.Length; i++)
            {
                var att = emailResponse.Attachments[i];
                Console.WriteLine($"  [{i + 1}] Filename: {att.Filename}");
                Console.WriteLine($"      ContentType: {att.ContentType}");
                Console.WriteLine($"      Content Length: {att.Content?.Length ?? 0} chars");
                Console.WriteLine($"      Path: {att.Path}");
            }
        }

        var jsonPayload = JsonConvert.SerializeObject(replyResponse, Formatting.Indented);

        // Debug: JSON-Payload (gekürzt) ausgeben
        var payloadPreview = jsonPayload.Length > 500
            ? jsonPayload.Substring(0, 500) + "..."
            : jsonPayload;
        Console.WriteLine($"[Inbound] Sende POST zu: {_apiUrl}emails/{mail.Id}/reply");
        Console.WriteLine($"[Inbound] Payload Preview:\n{payloadPreview}");

        request.Content = new StringContent(jsonPayload);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Inbound] FEHLER beim Senden der E-Mail:");
            Console.WriteLine($"  Status Code: {response.StatusCode}");
            Console.WriteLine($"  Response Body: {errorBody}");
            Console.WriteLine($"  Request URL: {_apiUrl}emails/{mail.Id}/reply");
        }
        else
        {
            Console.WriteLine($"[Inbound] E-Mail erfolgreich gesendet! Status: {response.StatusCode}");
        }
    }
}