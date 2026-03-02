namespace UTXO_E_Mail_Agent_Api.DTOs;

/// <summary>
/// DTO for inbound email webhook payload
/// </summary>
public class InboundEmailWebhookDto
{
    public string? Id { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<InboundAttachmentDto>? Attachments { get; set; }
}

public class InboundAttachmentDto
{
    public string? Filename { get; set; }
    public string? ContentType { get; set; }
    public string? Content { get; set; } // Base64 encoded
}
