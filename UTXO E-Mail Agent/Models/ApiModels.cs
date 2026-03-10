using UTXO_E_Mail_Agent.Classes;

namespace UTXO_E_Mail_Agent.Models;

/// <summary>
/// Request model for the process email API
/// </summary>
public class ProcessEmailRequest : ProcessTextRequestClass
{
    public string? Subject { get; set; }
    public string? HtmlContent { get; set; }
    public string? From { get; set; }
}

/// <summary>
/// Response model for the process email API
/// </summary>
public class ProcessEmailResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? EmailResponseText { get; set; }
    public string? EmailResponseSubject { get; set; }
    public string? EmailResponseHtml { get; set; }
    public string? AiExplanation { get; set; }
    public List<AttachmentResponse> Attachments { get; set; } = new();
}

public class AttachmentResponse
{
    public string? Filename { get; set; }
    public string? ContentType { get; set; }
    public string? Content { get; set; } // Base64 encoded
}

/// <summary>
/// Request model for the send email API
/// </summary>
public class SendEmailRequest
{
    /// <summary>Agent name (required to determine email provider and from address)</summary>
    public string AgentName { get; set; } = null!;
    public string? From { get; set; }
    public string To { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string? Text { get; set; }
    public string? Html { get; set; }
    public string? ReplyTo { get; set; }
}