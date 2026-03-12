using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.Classes;

public class ProcessMailsClass(DefaultdbContext db, IConfiguration configuration)
{
    private DefaultdbContext _db = db;
    private IConfiguration _configuration = configuration;


    /// <summary>
    /// Processes an email with the configured AI provider
    /// </summary>
    /// <param name="mail">The email to process</param>
    /// <param name="agent">The agent with configuration</param>
    /// <param name="existingConversation">Optional existing conversation entry (for pre-claimed emails)</param>
    /// <returns>The generated response from the AI provider</returns>
    public async Task<AiResponseClass> ProcessMailAsync(MailClass mail, Agent agent, Conversation? existingConversation = null)
    {
        // Select AI provider based on agent configuration
        var aiProvider = AiProviderFactory.GetProvider(agent, _configuration);

        if (aiProvider == null)
        {
            throw new InvalidOperationException($"Unknown AI provider: {agent.Aiprovider}");
        }

        // Build prompt for the AI provider
        var prompt = BuildPrompt(mail, agent);
        var systemPrompt = BuildSystemPrompt(mail, agent);
        systemPrompt += GetMcpServerInformations(agent);
        
        // Use existing conversation or create new one
        var conversation = existingConversation ?? new Conversation
        {
            Subject = mail.Subject ?? "(No Subject)",
            Text = mail.Text,
            Htmltext = mail.Html,
            AgentId = agent.Id,
            Emailfrom = mail.From ?? "(Unknown)",
            Messageid = mail.Id ?? Guid.NewGuid().ToString(),
            Emailreceived = DateTime.Now,
        };
        
        // Update prompt (always set, as it wasn't set during claim)
        conversation.Prompt = systemPrompt;
        
        if (existingConversation == null)
        {
            await _db.Conversations.AddAsync(conversation);
            await _db.SaveChangesAsync();
        }
        
        // Load conversation history if enabled
        List<Conversation>? conversationHistory = null;
        if (agent.Useconversationhistory && !string.IsNullOrEmpty(mail.From))
        {
            conversationHistory = await _db.Conversations
                .Where(c => c.AgentId == agent.Id && c.Emailfrom == mail.From && c.Id != conversation.Id)
                .OrderBy(c => c.Emailreceived)
                .Take(10) // Limit to last 10 conversations to avoid token overflow
                .AsNoTracking()
                .ToListAsync();
            
            Logger.Log($"[ProcessMail] Loading conversation history: {conversationHistory.Count} previous emails from {mail.From}", agent.Id);
        }
        
        // Call the AI provider
        var response = await aiProvider.GenerateResponse(systemPrompt, prompt, mail, agent, conversation, conversationHistory);

        conversation.Agentresponsetext = response.EmailResponseText;
        conversation.Agentresponsehtml = response.EmailResponseHtml;
        conversation.Agentresponsesubject = response.EmailResponseSubject;
        conversation.Aiexplanation = response.AiExplanation;
        conversation.Aifullresponse = response.FullResponse;
        conversation.Aicostusd = response.AiCostUsd;
        conversation.Aidurationms = response.AiDurationMs;
        conversation.Aiinputtokens = response.AiInputTokens;
        conversation.Aioutputtokens = response.AiOutputTokens;

        foreach (var attachment in response.Attachments.OrEmptyIfNull())
        {
            // If content is empty but path is provided, read the file content
            var content = attachment.Content;
            if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(attachment.Path))
            {
                if (!File.Exists(attachment.Path))
                {
                    Logger.LogWarning($"[API] Attachment file not found: {attachment.Path}");
                }
                else
                {
                    try
                    {
                        var fileBytes = await File.ReadAllBytesAsync(attachment.Path);
                        content = Convert.ToBase64String(fileBytes);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[API] Could not read attachment file {attachment.Path}: {ex.Message}");
                    }
                }
            }

            // Skip attachments with no content
            if (string.IsNullOrEmpty(content))
            {
                Logger.LogWarning($"[API] Skipping attachment {attachment.Filename} - no content available");
                continue;
            }

            ConversationAttachment at = new ConversationAttachment()
            {
                ConversationId = conversation.Id,
                Filename = attachment.Filename ?? "attachment",
                ContentType = attachment.ContentType ?? "application/octet-stream",
                Content = content,
                Path = attachment.Path
            };
            _db.ConversationAttachments.Add(at);
        }

        await _db.SaveChangesAsync();
        response.Conversation=conversation;
        
        return response;
    }

    private string GetMcpServerInformations(Agent agent)
    {
        var s = Environment.NewLine + "🔧 VERFÜGBARE TOOLS:" + Environment.NewLine + Environment.NewLine;

        // Redirect reminder
        s += "📨 **E-MAIL WEITERLEITUNG**" + Environment.NewLine;
        s += "   Für Weiterleitungen verwende Action='redirect' in deiner JSON-Antwort!" + Environment.NewLine;
        s += "   Das System leitet die komplette Original-E-Mail inkl. HTML und Anhänge automatisch weiter." + Environment.NewLine;
        s += "   Du musst nur die Empfänger angeben (RedirectTo, optional RedirectCc)." + Environment.NewLine;
        s += Environment.NewLine;

        // Agent-specific MCP servers
        if (agent.Mcpservers.Any())
        {
            s += "📌 WEITERE SPEZIAL-TOOLS:" + Environment.NewLine;
            foreach (var agentMcpserver in agent.Mcpservers.OrEmptyIfNull())
            {
                s += $"   **{agentMcpserver.Name}**" + Environment.NewLine;
                s += $"   Beschreibung: {agentMcpserver.Description}" + Environment.NewLine;
                s += $"   Endpoint: {agentMcpserver.Call} {agentMcpserver.Url}" + Environment.NewLine;
                s += Environment.NewLine;
            }
        }

        s += "ZUSAMMENFASSUNG:" + Environment.NewLine;
        s += "• Antwort an Absender → Action='respond' mit EmailResponseText" + Environment.NewLine;
        s += "• Weiterleitung an andere → Action='redirect' mit RedirectTo Array" + Environment.NewLine;
        s += "• Spam/unwichtig → Action='delete'" + Environment.NewLine;
        s += "• Selbst erledigt → Action='ignore'" + Environment.NewLine;
        return s;
    }

    private string BuildSystemPrompt(MailClass mailClass, Agent agent)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Du bist ein E-Mail-Assistent.");
        promptBuilder.AppendLine($"Sprache in der Du die Antwort erstellen sollst: {agent.Defaultlanguage}");
        promptBuilder.AppendLine();
        


        if (!string.IsNullOrEmpty(agent.Customer.Companyinformation))
        {
            promptBuilder.AppendLine("Firmeninformationen:");
            promptBuilder.AppendLine(agent.Customer.Companyinformation);
            promptBuilder.AppendLine();
        }

        if (!string.IsNullOrEmpty(agent.Tasktobecompleted))
        {
            promptBuilder.AppendLine("Deine Aufgabe:");
            promptBuilder.AppendLine(agent.Tasktobecompleted);
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Kontext der aktuellen E-Mail:");
        promptBuilder.AppendLine($"Von: {mailClass.From}");
        promptBuilder.AppendLine($"Betreff: {mailClass.Subject}");
        promptBuilder.AppendLine($"Datum: {mailClass.CreatedAt}");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("KRITISCH - AUTO-REPLY ERKENNUNG:");
        promptBuilder.AppendLine("Prüfe ZUERST ob die E-Mail eine automatische Antwort ist!");
        promptBuilder.AppendLine("Automatische Antworten NIEMALS beantworten. Erkennungsmerkmale:");
        promptBuilder.AppendLine("- Out of Office / Abwesenheitsnotiz / Absence Message");
        promptBuilder.AppendLine("- Auto-Reply / Automatic Reply / Automatische Antwort");
        promptBuilder.AppendLine("- Vacation / Urlaub / Holiday Message");
        promptBuilder.AppendLine("- Currently unavailable / Derzeit nicht erreichbar");
        promptBuilder.AppendLine("- Will return / Bin zurück am");
        promptBuilder.AppendLine("- Headers: Auto-Submitted, X-Auto-Response-Suppress, Precedence: bulk/auto_reply");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Falls es eine automatische Antwort ist, gib NUR zurück:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"EmailResponseText\": null,");
        promptBuilder.AppendLine("  \"EmailResponseSubject\": null,");
        promptBuilder.AppendLine("  \"EmailResponseHtml\": null,");
        promptBuilder.AppendLine("  \"AiExplanation\": \"Automatische Antwort erkannt - keine Aktion erforderlich\",");
        promptBuilder.AppendLine("  \"attachments\": []");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("KRITISCH - AKTIONEN UND ANTWORTFORMAT:");
        promptBuilder.AppendLine("Du musst IMMER eine der folgenden Aktionen im JSON angeben:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("📧 ACTION = \"respond\" - Antwort an den URSPRÜNGLICHEN Absender senden");
        promptBuilder.AppendLine("   → Fülle EmailResponseText, EmailResponseSubject, EmailResponseHtml aus");
        promptBuilder.AppendLine("   → Das System sendet die Antwort automatisch");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("📨 ACTION = \"redirect\" - E-Mail an ANDERE Empfänger weiterleiten");
        promptBuilder.AppendLine("   → Setze RedirectTo (Array mit E-Mail-Adressen)");
        promptBuilder.AppendLine("   → Optional: RedirectCc für CC-Empfänger");
        promptBuilder.AppendLine("   → Optional: RedirectMessage für eine kurze Nachricht vor der Original-Mail");
        promptBuilder.AppendLine("   → Das System leitet die KOMPLETTE Original-E-Mail automatisch weiter (inkl. HTML und Anhänge)!");
        promptBuilder.AppendLine("   → EmailResponseText etc. auf null setzen");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("🗑️ ACTION = \"delete\" - E-Mail ist Spam/unwichtig, keine Aktion nötig");
        promptBuilder.AppendLine("   → Alle EmailResponse-Felder auf null setzen");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("⏭️ ACTION = \"ignore\" - Du hast die Aufgabe bereits selbst erledigt (z.B. über API-Aufrufe)");
        promptBuilder.AppendLine("   → Alle EmailResponse-Felder auf null setzen");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Deine FINALE Antwort MUSS ZWINGEND folgendes JSON-Format haben:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"Action\": \"respond\",  // oder \"redirect\", \"delete\", \"ignore\"");
        promptBuilder.AppendLine("  \"EmailResponseText\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseSubject\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseHtml\": \"...\",");
        promptBuilder.AppendLine("  \"RedirectTo\": [\"empfaenger@example.com\"],  // nur bei Action=redirect");
        promptBuilder.AppendLine("  \"RedirectCc\": [],  // optional bei redirect");
        promptBuilder.AppendLine("  \"RedirectMessage\": \"Kurze Info...\",  // optional bei redirect");
        promptBuilder.AppendLine("  \"AiExplanation\": \"...\",");
        promptBuilder.AppendLine("  \"attachments\": [],");
        promptBuilder.AppendLine("  \"MustCreateAttachment\": false,");
        promptBuilder.AppendLine("  \"AttachmentType\": null,");
        promptBuilder.AppendLine("  \"AttachmentData\": null,");
        promptBuilder.AppendLine("  \"AttachmentFilename\": null");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("WICHTIGE Regeln für die Antwort:");
        promptBuilder.AppendLine("- Das 'Action' Feld ist PFLICHT und bestimmt was passiert");
        promptBuilder.AppendLine("- Du kannst optional KURZ deine Überlegungen erklären, aber dann MUSS das vollständige JSON folgen");
        promptBuilder.AppendLine("- Das JSON muss IMMER am Ende deiner finalen Antwort stehen");
        promptBuilder.AppendLine("- Bei Action='respond': Antworte in HTML wenn die Original-Mail HTML war");
        promptBuilder.AppendLine("- Bei Action='respond': Die Antwort soll EmailResponseText UND EmailResponseHtml enthalten");
        promptBuilder.AppendLine("- Bei Action='respond': Die ursprüngliche E-Mail des Kunden soll eingerückt/quoted sein");
        promptBuilder.AppendLine("- Bei Action='respond': Der Betreff soll ein 'RE:' vorangestellt haben");
        promptBuilder.AppendLine("- Bei Action='redirect': Das System kümmert sich um ALLES - du gibst nur die Empfänger an!");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("ATTACHMENTS (PDFs, DOCs, etc.) - WICHTIG:");
        promptBuilder.AppendLine("Wenn der Kunde ein Dokument (PDF, Word, Excel, PowerPoint) anfordert:");
        promptBuilder.AppendLine("- Erstelle das Dokument NICHT selbst!");
        promptBuilder.AppendLine("- Setze stattdessen folgende Felder im JSON:");
        promptBuilder.AppendLine("  * \"MustCreateAttachment\": true");
        promptBuilder.AppendLine("  * \"AttachmentType\": \"pdf\" (oder \"docx\", \"xlsx\", \"pptx\")");
        promptBuilder.AppendLine("  * \"AttachmentData\": Alle Daten die ins Dokument sollen als strukturierter Text");
        promptBuilder.AppendLine("  * \"AttachmentFilename\": Vorgeschlagener Dateiname (z.B. \"Angebot_Murg.pdf\")");
        promptBuilder.AppendLine("- Das \"attachments\" Array bleibt leer - das System erstellt das Dokument automatisch");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Beispiel für AttachmentData bei einem Tarif-PDF:");
        promptBuilder.AppendLine("\"AttachmentData\": \"INTERNET-ANGEBOT\\n\\nKunde: Max Mustermann\\nAdresse: Musterstraße 1, 12345 Musterstadt\\n\\nVerfügbare Tarife:\\n1. Tarif Basic - 25 Mbit/s - 24,90€/Monat\\n2. Tarif Premium - 100 Mbit/s - 44,90€/Monat\\n\\nKontakt: info@firma.de\"");
        promptBuilder.AppendLine();
        
        // Delegation instructions
        promptBuilder.AppendLine("AUFGABEN DELEGIEREN:");
        promptBuilder.AppendLine("Wenn eine E-Mail nicht in deinen Zuständigkeitsbereich fällt, kannst du sie an einen anderen Agenten delegieren.");
        promptBuilder.AppendLine("Die verfügbaren Agenten für Delegation stehen in deiner Aufgabenbeschreibung oben.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Um zu delegieren, führe folgenden curl-Aufruf aus:");
        promptBuilder.AppendLine("```bash");
        promptBuilder.AppendLine("curl -X POST http://localhost:5051/api/processemail \\");
        promptBuilder.AppendLine("  -H \"Content-Type: application/json\" \\");
        promptBuilder.AppendLine("  -d '{");
        promptBuilder.AppendLine("    \"agentName\": \"<ZIEL-AGENT-NAME>\",");
        promptBuilder.AppendLine($"    \"messageId\": \"{mailClass.OriginalMessageId ?? mailClass.Id}\",");
        promptBuilder.AppendLine($"    \"from\": \"{mailClass.From}\",");
        promptBuilder.AppendLine($"    \"to\": {System.Text.Json.JsonSerializer.Serialize(mailClass.To ?? Array.Empty<string>())},");
        promptBuilder.AppendLine($"    \"subject\": \"{EscapeJsonString(mailClass.Subject)}\",");
        promptBuilder.AppendLine($"    \"status\": \"{mailClass.Status}\",");
        promptBuilder.AppendLine($"    \"createdAt\": \"{mailClass.CreatedAt}\",");
        promptBuilder.AppendLine($"    \"hasAttachments\": {(mailClass.HasAttachments ?? false).ToString().ToLower()},");
        promptBuilder.AppendLine($"    \"cc\": {System.Text.Json.JsonSerializer.Serialize(mailClass.Cc ?? Array.Empty<string>())},");
        promptBuilder.AppendLine($"    \"bcc\": {System.Text.Json.JsonSerializer.Serialize(mailClass.Bcc ?? Array.Empty<string>())},");
        promptBuilder.AppendLine($"    \"replyTo\": {System.Text.Json.JsonSerializer.Serialize(mailClass.ReplyTo ?? Array.Empty<string>())},");
        promptBuilder.AppendLine("    \"html\": \"<ORIGINAL-HTML-CONTENT>\",");
        promptBuilder.AppendLine("    \"text\": \"<ORIGINAL-TEXT-CONTENT>\",");
        promptBuilder.AppendLine($"    \"attachments\": {System.Text.Json.JsonSerializer.Serialize(mailClass.Attachments ?? Array.Empty<string>())}");
        promptBuilder.AppendLine("  }'");
        promptBuilder.AppendLine("```");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("WICHTIG bei Delegation:");
        promptBuilder.AppendLine("- Ersetze <ZIEL-AGENT-NAME> mit dem Namen des zuständigen Agenten");
        promptBuilder.AppendLine("- Ersetze <ORIGINAL-HTML-CONTENT> und <ORIGINAL-TEXT-CONTENT> mit dem tatsächlichen E-Mail-Inhalt");
        promptBuilder.AppendLine("- Nach erfolgreicher Delegation (HTTP 202) antworte NICHT selbst auf die E-Mail");
        promptBuilder.AppendLine("- Setze in deiner JSON-Antwort alle EmailResponse-Felder auf null");
        promptBuilder.AppendLine("- Erkläre in AiExplanation, dass du die E-Mail delegiert hast");
        promptBuilder.AppendLine();
        
        return promptBuilder.ToString();
    }
    private string BuildPrompt(MailClass mail, Agent agent)
    {
        var promptBuilder = new StringBuilder();

        if (!string.IsNullOrEmpty(mail.Text))
        {
            promptBuilder.AppendLine("Inhalt (Text):");
            promptBuilder.AppendLine(mail.Text);
        }
        else if (!string.IsNullOrEmpty(mail.Html))
        {
            promptBuilder.AppendLine("Inhalt (HTML):");
            promptBuilder.AppendLine(mail.Html);
        }

        return promptBuilder.ToString();
    }
    
    private static string EscapeJsonString(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
