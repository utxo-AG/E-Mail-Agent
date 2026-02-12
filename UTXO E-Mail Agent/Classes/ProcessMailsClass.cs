using System.Text;
using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent_Shared.Models;

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
    /// <returns>The generated response from the AI provider</returns>
    public async Task<AiResponseClass> ProcessMailAsync(MailClass mail, Agent agent)
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
        
        var conversation=new Conversation
        {
            Subject = mail.Subject,
            Text = mail.Text,
            Htmltext = mail.Html,
            AgentId = agent.Id,
            Emailfrom = mail.From,
            Messageid = mail.Id,
            Prompt = systemPrompt,
            Emailreceived = DateTime.Now,
        };
        await _db.Conversations.AddAsync(conversation);
        await _db.SaveChangesAsync();
        
        // Call the AI provider
        var response = await aiProvider.GenerateResponse(systemPrompt, prompt, mail, agent, conversation);

        conversation.Agentresponsetext = response.EmailResponseText;
        conversation.Agentresponsehtml = response.EmailResponseHtml;
        conversation.Agentresponsesubject = response.EmailResponseSubject;
        conversation.Aiexplanation = response.AiExplanation;

        foreach (var attachment in response.Attachments.OrEmptyIfNull())
        {
            // If content is empty but path is provided, read the file content
            var content = attachment.Content;
            if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(attachment.Path))
            {
                if (!File.Exists(attachment.Path))
                {
                    Console.WriteLine($"[API] Warning: Attachment file not found: {attachment.Path}");
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
                        Console.WriteLine($"[API] Warning: Could not read attachment file {attachment.Path}: {ex.Message}");
                    }
                }
            }

            // Skip attachments with no content
            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine($"[API] Warning: Skipping attachment {attachment.Filename} - no content available");
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
        
        return response;
    }

    private string GetMcpServerInformations(Agent agent)
    {
        var s = Environment.NewLine + "🔧 VERFÜGBARE TOOLS:" + Environment.NewLine + Environment.NewLine;

        // Built-in send_email tool - always available
        s += "📧 **send_email** (IMMER VERFÜGBAR)" + Environment.NewLine;
        s += "   Verwende dieses Tool um E-Mails weiterzuleiten oder neue E-Mails zu senden." + Environment.NewLine;
        s += "   Parameter:" + Environment.NewLine;
        s += "   - to: Empfänger E-Mail-Adresse (erforderlich)" + Environment.NewLine;
        s += "   - subject: Betreff (erforderlich)" + Environment.NewLine;
        s += "   - text: Inhalt als Plain-Text (erforderlich)" + Environment.NewLine;
        s += "   - html: Inhalt als HTML (optional)" + Environment.NewLine;
        s += $"   Die Absenderadresse ist immer: {agent.Emailaddress}" + Environment.NewLine;
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

        s += "WICHTIG: Wenn eine Anfrage erfordert eine E-Mail weiterzuleiten oder zu senden, verwende send_email." + Environment.NewLine;
        s += "Verwende NICHT bash oder curl für E-Mail-Versand!" + Environment.NewLine;
        return s;
    }

    private string BuildSystemPrompt(MailClass mailClass, Agent agent)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Du bist ein E-Mail-Assistent.");
        promptBuilder.AppendLine($"Sprache in der Du die Antwort erstellen sollst: {agent.Defaultlanguage}");
        promptBuilder.AppendLine();

        if (!string.IsNullOrEmpty(agent.Companyinformation))
        {
            promptBuilder.AppendLine("Firmeninformationen:");
            promptBuilder.AppendLine(agent.Companyinformation);
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
        promptBuilder.AppendLine("KRITISCH - ANTWORTFORMAT:");
        promptBuilder.AppendLine("Deine FINALE Antwort MUSS ZWINGEND folgendes JSON-Format haben:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"EmailResponseText\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseSubject\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseHtml\": \"...\",");
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
        promptBuilder.AppendLine("- Du kannst optional KURZ deine Überlegungen erklären, aber dann MUSS das vollständige JSON folgen");
        promptBuilder.AppendLine("- Das JSON muss IMMER am Ende deiner finalen Antwort stehen");
        promptBuilder.AppendLine("- Wenn Du eine Mail als HTML bekommen hast, antworte auch als HTML");
        promptBuilder.AppendLine("- Die Antwort soll sowohl EmailResponseText (plain text) als auch EmailResponseHtml (HTML) enthalten");
        promptBuilder.AppendLine("- Die ursprüngliche E-Mail des Kunden soll eingerückt/quoted sein");
        promptBuilder.AppendLine("- Der Betreff soll ein 'RE:' vorangestellt haben");
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
}
