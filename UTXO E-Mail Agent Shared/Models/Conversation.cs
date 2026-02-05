using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Conversation
{
    public int Id { get; set; }

    public int AgentId { get; set; }

    public string Messageid { get; set; } = null!;

    public string Emailfrom { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string? Text { get; set; }

    public string? Htmltext { get; set; }

    public string? Prompt { get; set; }

    public string? Agentresponsetext { get; set; }

    public string? Agentresponsehtml { get; set; }

    public string? Agentresponsesubject { get; set; }

    public int? ConversationreferenceId { get; set; }

    public DateTime Emailreceived { get; set; }

    public string? Aiexplanation { get; set; }

    public virtual Agent Agent { get; set; } = null!;

    public virtual ICollection<ConversationAttachment> ConversationAttachments { get; set; } = new List<ConversationAttachment>();

    public virtual Conversation? Conversationreference { get; set; }

    public virtual ICollection<Conversation> InverseConversationreference { get; set; } = new List<Conversation>();

    public virtual ICollection<Mcpserverrequest> Mcpserverrequests { get; set; } = new List<Mcpserverrequest>();
}
