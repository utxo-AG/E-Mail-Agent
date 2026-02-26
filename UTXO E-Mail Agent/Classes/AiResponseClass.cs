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
    
    [Newtonsoft.Json.JsonConverter(typeof(AttachmentArrayConverter))]
    public Attachment[] Attachments { get; set; }

    // Document generation fields - if true, a second agent will create the document
    public bool MustCreateAttachment { get; set; } = false;
    public string? AttachmentType { get; set; }  // "pdf", "docx", "xlsx", "pptx"
    public string? AttachmentData { get; set; }  // Structured data for the document (JSON or text)
    public string? AttachmentFilename { get; set; }  // Suggested filename for the attachment
}