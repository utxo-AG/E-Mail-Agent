using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using UTXO_E_Mail_Agent.AiProvider.Claude;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.AiProvider.RstCloud;

/// <summary>
/// AI-Provider für rstcloud.de (OpenAI-kompatible API, z. B. Qwen-Modelle).
///
/// Schlanker Text-Provider: System-Prompt + E-Mail rein, strukturierte JSON-Antwort
/// (AiResponseClass) raus. Nutzt dasselbe Antwortformat wie der Claude-Provider und
/// die gemeinsame <see cref="ClaudeClass.ParseAiResponse"/>-Logik. Die Qwen-Modelle
/// liefern keine Dokumente/Anhänge und keine MCP-Tools – diese Felder bleiben leer.
///
/// Konfiguration (per Umgebungsvariablen):
///   RstCloud__ApiKey    – Pflicht
///   RstCloud__Endpoint  – optional, Default https://api.rstcloud.de/v1
/// </summary>
public class RstCloudClass : IAiProvider
{
    private readonly string _apiKey;
    private readonly string _endpoint;

    private const string DefaultEndpoint = "https://api.rstcloud.de/v1";

    // Verstärkt die JSON-Ausgabe für kleinere Modelle (zusätzlich zum System-Prompt).
    private const string JsonInstruction =
        "WICHTIG: Antworte ausschließlich mit einem einzigen gültigen JSON-Objekt im oben " +
        "beschriebenen Format. Schreibe keinen Text davor oder danach und verwende keine " +
        "Markdown-Codeblöcke.";

    public RstCloudClass(IConfiguration configuration)
    {
        _apiKey = configuration["RstCloud:ApiKey"]
            ?? throw new InvalidOperationException(
                "RstCloud:ApiKey not configured. Setze die Umgebungsvariable RstCloud__ApiKey.");
        _endpoint = configuration["RstCloud:Endpoint"] ?? DefaultEndpoint;
    }

    public async Task<AiResponseClass> GenerateResponse(string systemPrompt, string prompt,
        MailClass mailClass, Agent agent, Conversation conversation,
        List<Conversation>? conversationHistory = null)
    {
        var stopwatch = Stopwatch.StartNew();

        var model = string.IsNullOrWhiteSpace(agent.Aimodel) ? "qwen3-8b" : agent.Aimodel;

        var client = new ChatClient(model, new ApiKeyCredential(_apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_endpoint) });

        // System steuert das Verhalten, danach user/assistant abwechselnd.
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt + "\n\n" + JsonInstruction)
        };

        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            Logger.Log($"[RstCloud] Using conversation history: {conversationHistory.Count} previous emails", agent.Id);
            foreach (var prev in conversationHistory)
            {
                var userText = !string.IsNullOrWhiteSpace(prev.Prompt) ? prev.Prompt : prev.Text;
                if (!string.IsNullOrWhiteSpace(userText))
                    messages.Add(new UserChatMessage(userText));
                if (!string.IsNullOrWhiteSpace(prev.Agentresponsetext))
                    messages.Add(new AssistantChatMessage(prev.Agentresponsetext));
            }
        }

        messages.Add(new UserChatMessage(prompt));

        string fullText;
        long inputTokens = 0;
        long outputTokens = 0;

        try
        {
            Logger.Log($"[RstCloud] Calling {_endpoint} (model: {model})", agent.Id);
            var completion = await client.CompleteChatAsync(messages);

            fullText = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text
                : string.Empty;

            if (completion.Value.Usage != null)
            {
                inputTokens = completion.Value.Usage.InputTokenCount;
                outputTokens = completion.Value.Usage.OutputTokenCount;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[RstCloud] API error: {ex.Message}", agent.Id);
            throw;
        }

        // Gleiches Antwortformat wie beim Claude-Provider.
        var responseClass = ClaudeClass.ParseAiResponse(fullText, agent.Defaultlanguage, mailClass.Text);
        responseClass.FullResponse = fullText;

        // Qwen liefert keine Dokumente/Anhänge – entsprechende Felder neutralisieren.
        responseClass.MustCreateAttachment = false;
        responseClass.Attachments ??= Array.Empty<Attachment>();

        stopwatch.Stop();
        responseClass.AiDurationMs = stopwatch.ElapsedMilliseconds;
        responseClass.AiInputTokens = inputTokens;
        responseClass.AiOutputTokens = outputTokens;
        // Preise für rstcloud sind nicht öffentlich hinterlegt – Kosten bleiben 0.
        responseClass.AiCostUsd = 0m;

        return responseClass;
    }
}
