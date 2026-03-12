using System.Text.Json.Serialization;

namespace UTXO_E_Mail_Agent_Api.DTOs;

/// <summary>
/// DTO for inbound.new webhook payload
/// </summary>
public class InboundEmailWebhookDto
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }
    
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
    
    [JsonPropertyName("email")]
    public InboundEmailDto? Email { get; set; }
    
    [JsonPropertyName("endpoint")]
    public InboundEndpointDto? Endpoint { get; set; }
}

public class InboundEmailDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
    
    [JsonPropertyName("from")]
    public InboundAddressFieldDto? From { get; set; }
    
    [JsonPropertyName("to")]
    public InboundAddressFieldDto? To { get; set; }
    
    [JsonPropertyName("recipient")]
    public string? Recipient { get; set; }
    
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
    
    [JsonPropertyName("receivedAt")]
    public DateTime? ReceivedAt { get; set; }
    
    [JsonPropertyName("parsedData")]
    public InboundParsedDataDto? ParsedData { get; set; }
}

public class InboundAddressFieldDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("addresses")]
    public List<InboundAddressDto>? Addresses { get; set; }
}

public class InboundAddressDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public class InboundParsedDataDto
{
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
    
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }
    
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
    
    [JsonPropertyName("from")]
    public InboundAddressFieldDto? From { get; set; }
    
    [JsonPropertyName("to")]
    public InboundAddressFieldDto? To { get; set; }
    
    [JsonPropertyName("cc")]
    public InboundAddressFieldDto? Cc { get; set; }
    
    [JsonPropertyName("bcc")]
    public InboundAddressFieldDto? Bcc { get; set; }
    
    [JsonPropertyName("replyTo")]
    public InboundAddressFieldDto? ReplyTo { get; set; }
    
    [JsonPropertyName("textBody")]
    public string? TextBody { get; set; }
    
    [JsonPropertyName("htmlBody")]
    public string? HtmlBody { get; set; }
    
    [JsonPropertyName("attachments")]
    public List<InboundAttachmentDto>? Attachments { get; set; }
}

public class InboundAttachmentDto
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }
    
    [JsonPropertyName("size")]
    public long? Size { get; set; }
    
    [JsonPropertyName("contentId")]
    public string? ContentId { get; set; }
    
    [JsonPropertyName("contentDisposition")]
    public string? ContentDisposition { get; set; }
    
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}

public class InboundEndpointDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
