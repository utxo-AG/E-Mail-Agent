using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Logmessage
{
    public long Id { get; set; }

    public int? AgentId { get; set; }

    public string Message { get; set; } = null!;

    public string? Additionaldata { get; set; }

    public DateTime Created { get; set; }

    public virtual Agent? Agent { get; set; }
}
