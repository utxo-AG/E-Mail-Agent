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
    /// Verarbeitet eine E-Mail mit dem konfigurierten AI Provider
    /// </summary>
    /// <param name="mail">Die zu verarbeitende E-Mail</param>
    /// <param name="agent">Der Agent mit Konfiguration</param>
    /// <returns>Die generierte Antwort vom AI Provider</returns>
    public async Task<AiResponseClass> ProcessMailAsync(MailClass mail, Agent agent)
    {
        // AI Provider anhand des Agents auswählen
        var aiProvider = AiProviderFactory.GetProvider(agent, _configuration);

        if (aiProvider == null)
        {
            throw new InvalidOperationException($"Unknown AI provider: {agent.Aiprovider}");
        }

        // Prompt für den AI Provider aufbauen
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
        
        // AI Provider aufrufen
        var response = await aiProvider.GenerateResponse(systemPrompt, prompt, mail, agent, conversation);

        conversation.Agentresponsetext = response.EmailResponseText;
        conversation.Agentresponsehtml = response.EmailResponseHtml;
        conversation.Agentresponsesubject = response.EmailResponseSubject;
        conversation.Aiexplanation = response.AiExplanation;

        foreach (var attachment in response.Attachments.OrEmptyIfNull())
        {
            ConversationAttachment at = new ConversationAttachment() { ConversationId = conversation.Id, Filename = attachment.Filename, ContentType = attachment.ContentType, Content = attachment.Content, Path = attachment.Path };
            _db.ConversationAttachments.Add(at);
        }

        await _db.SaveChangesAsync();
        
        return response;
    }

    private string GetMcpServerInformations(Agent agent)
    {
        if (!agent.Mcpservers.Any())
            return string.Empty;

        var s = Environment.NewLine + "🔧 WICHTIG - VERFÜGBARE SPEZIAL-TOOLS:" + Environment.NewLine;
        s += "Du MUSST diese spezialisierten Tools verwenden, wenn sie zur Anfrage passen:" + Environment.NewLine;
        s += "VERWENDE DIESE TOOLS ANSTATT bash/curl für die folgenden Aufgaben:" + Environment.NewLine + Environment.NewLine;

        foreach (var agentMcpserver in agent.Mcpservers.OrEmptyIfNull())
        {
            s += $"📌 **{agentMcpserver.Name}**" + Environment.NewLine;
            s += $"   Beschreibung: {agentMcpserver.Description}" + Environment.NewLine;
            s += $"   Endpoint: {agentMcpserver.Call} {agentMcpserver.Url}" + Environment.NewLine;
            s += Environment.NewLine;
        }

        s += "WICHTIG: Wenn eine Kundenanfrage zu einem dieser Tools passt, verwende das Tool DIREKT." + Environment.NewLine;
        s += "Verwende NICHT bash oder curl für diese Aufgaben!" + Environment.NewLine;
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
        promptBuilder.AppendLine("KRITISCH - ANTWORTFORMAT:");
        promptBuilder.AppendLine("Deine FINALE Antwort MUSS ZWINGEND folgendes JSON-Format haben:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"EmailResponseText\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseSubject\": \"...\",");
        promptBuilder.AppendLine("  \"EmailResponseHtml\": \"...\",");
        promptBuilder.AppendLine("  \"attachments\": [");
        promptBuilder.AppendLine("    {");
        promptBuilder.AppendLine("      \"filename\": \"...\",");
        promptBuilder.AppendLine("      \"content\": \"...\",");
        promptBuilder.AppendLine("      \"content_type\": \"...\",");
        promptBuilder.AppendLine("      \"path\": \"...\"");
        promptBuilder.AppendLine("    }");
        promptBuilder.AppendLine("  ]");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("WICHTIGE Regeln für die Antwort:");
        promptBuilder.AppendLine("- Du darfst Tools verwenden (z.B. für PDF-Erstellung), aber danach MUSST du das JSON zurückgeben");
        promptBuilder.AppendLine("- Du kannst optional KURZ deine Überlegungen erklären, aber dann MUSS das vollständige JSON folgen");
        promptBuilder.AppendLine("- Das JSON muss IMMER am Ende deiner finalen Antwort stehen");
        promptBuilder.AppendLine("- Wenn Du eine Mail als HTML bekommen hast, antworte auch als HTML");
        promptBuilder.AppendLine("- Die Antwort soll sowohl EmailResponseText (plain text) als auch EmailResponseHtml (HTML) enthalten");
        promptBuilder.AppendLine("- Die ursprüngliche E-Mail des Kunden soll eingerückt/quoted sein");
        promptBuilder.AppendLine("- Der Betreff soll ein 'RE:' vorangestellt haben");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("ATTACHMENTS (PDFs, DOCs, etc.):");
        promptBuilder.AppendLine("- Du DARFST Attachments mit Tools erstellen (Write, Bash, etc.)");
        promptBuilder.AppendLine("- Speichere die Dateien lokal im aktuellen Verzeichnis oder einem temp-Ordner");
        promptBuilder.AppendLine("- Im JSON gibst du NUR den absoluten PFAD zur Datei zurück, NICHT den Inhalt!");
        promptBuilder.AppendLine("- Lasse das 'content' Feld leer oder weg - das System befüllt es automatisch");
        promptBuilder.AppendLine("- Beispiel: {\"filename\": \"angebot.pdf\", \"content_type\": \"application/pdf\", \"path\": \"/pfad/zur/datei.pdf\"}");
        
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
