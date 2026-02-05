using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Mcpserverrequest
{
    public int Id { get; set; }

    public int McpserverId { get; set; }

    public int ConversationId { get; set; }

    public DateTime Created { get; set; }

    public string Parameter { get; set; } = null!;

    public string Result { get; set; } = null!;

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual Mcpserver Mcpserver { get; set; } = null!;
}
