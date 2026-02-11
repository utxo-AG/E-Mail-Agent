namespace UTXO_E_Mail_Agent_Admintool.Models;

/// <summary>
/// Request model for the process text API
/// </summary>
public class ChatWithAgentRequest
{
    public string? TextContent { get; set; }
    public int? AgentId { get; set; }
}

/// <summary>
/// Response model for the process text API
/// </summary>
public class ChatWithAgentResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? EmailResponseText { get; set; }
    public string? EmailResponseSubject { get; set; }
    public string? EmailResponseHtml { get; set; }
    public string? AiExplanation { get; set; }
    public List<ChatAttachment> Attachments { get; set; } = new();
}

/// <summary>
/// Attachment model for chat responses
/// </summary>
public class ChatAttachment
{
    public string? Filename { get; set; }
    public string? ContentType { get; set; }
    public string? Content { get; set; } // Base64 encoded
}
