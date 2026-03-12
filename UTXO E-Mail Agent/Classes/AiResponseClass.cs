using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

namespace UTXO_E_Mail_Agent.Classes;

public class AiResponseClass
{
    public Conversation Conversation;

    /// <summary>
    /// Tracks recipients that already received an email via send_email tool during AI processing.
    /// Used to prevent duplicate sends from SendReplyResponseEmail.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public HashSet<string> AlreadySentTo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string EmailResponseText { get; set; } = string.Empty;
    public string EmailResponseHtml { get; set; } = string.Empty;
    public string EmailResponseSubject { get; set; } = string.Empty;
    public string? AiExplanation { get; set; }  // Text before the JSON (optional)
    public string? FullResponse { get; set; }  // Complete AI response for conversation history
    
    /// <summary>
    /// Total cost of AI processing in USD
    /// </summary>
    public decimal? AiCostUsd { get; set; }
    
    /// <summary>
    /// Total duration of AI processing in milliseconds
    /// </summary>
    public long? AiDurationMs { get; set; }
    
    /// <summary>
    /// Total input tokens used
    /// </summary>
    public long? AiInputTokens { get; set; }
    
    /// <summary>
    /// Total output tokens generated
    /// </summary>
    public long? AiOutputTokens { get; set; }
    
    /// <summary>
    /// Working directory used for this conversation (for cleanup after email is sent)
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string? WorkingDirectory { get; set; }
    
    [Newtonsoft.Json.JsonConverter(typeof(AttachmentArrayConverter))]
    public Attachment[] Attachments { get; set; }

    // Document generation fields - if true, a second agent will create the document
    public bool MustCreateAttachment { get; set; } = false;
    public string? AttachmentType { get; set; }  // "pdf", "docx", "xlsx", "pptx"
    public string? AttachmentData { get; set; }  // Structured data for the document (JSON or text)
    public string? AttachmentFilename { get; set; }  // Suggested filename for the attachment
    
    // Action fields - what to do with the email
    /// <summary>
    /// Action to perform: "respond" (send reply), "redirect" (forward to others), "delete" (spam), "ignore" (no action needed)
    /// </summary>
    public string? Action { get; set; }  // "respond", "redirect", "delete", "ignore"
    
    /// <summary>
    /// Recipients for redirect action (TO addresses)
    /// </summary>
    public string[]? RedirectTo { get; set; }
    
    /// <summary>
    /// CC recipients for redirect action
    /// </summary>
    public string[]? RedirectCc { get; set; }
    
    /// <summary>
    /// Optional message to prepend when redirecting
    /// </summary>
    public string? RedirectMessage { get; set; }
}