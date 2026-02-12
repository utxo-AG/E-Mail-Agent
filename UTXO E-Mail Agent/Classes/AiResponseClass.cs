using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

namespace UTXO_E_Mail_Agent.Classes;

public class AiResponseClass
{
    public string EmailResponseText { get; set; } = string.Empty;
    public string EmailResponseHtml { get; set; } = string.Empty;
    public string EmailResponseSubject { get; set; } = string.Empty;
    public string? AiExplanation { get; set; }  // Text before the JSON (optional)
    public Attachment[] Attachments { get; set; }

    // Document generation fields - if true, a second agent will create the document
    public bool MustCreateAttachment { get; set; } = false;
    public string? AttachmentType { get; set; }  // "pdf", "docx", "xlsx", "pptx"
    public string? AttachmentData { get; set; }  // Structured data for the document (JSON or text)
    public string? AttachmentFilename { get; set; }  // Suggested filename for the attachment
}