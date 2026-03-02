namespace UTXO_E_Mail_Agent_Api.DTOs;

public class ConversationResponseDto
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public string Messageid { get; set; } = null!;
    public string Emailfrom { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string? Text { get; set; }
    public string? Htmltext { get; set; }
    public string? Agentresponsetext { get; set; }
    public string? Agentresponsehtml { get; set; }
    public string? Agentresponsesubject { get; set; }
    public DateTime Emailreceived { get; set; }
    public string? Aiexplanation { get; set; }
    public List<ConversationAttachmentDto>? Attachments { get; set; }
}

public class ConversationAttachmentDto
{
    public int Id { get; set; }
    public string Filename { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}

public class ConversationListResponseDto
{
    public List<ConversationResponseDto> Conversations { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
