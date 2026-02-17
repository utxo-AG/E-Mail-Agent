using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Sentemail
{
    public int Id { get; set; }

    public int? ConversationId { get; set; }

    public string Emailreceiver { get; set; } = null!;

    public string Emailtext { get; set; } = null!;

    public DateTime Created { get; set; }

    public string Subject { get; set; } = null!;

    public virtual Conversation? Conversation { get; set; }
}
