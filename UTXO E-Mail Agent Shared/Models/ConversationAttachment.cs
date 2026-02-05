using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class ConversationAttachment
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public string Filename { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string? Path { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;
}
