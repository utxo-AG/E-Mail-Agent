using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Mcpserver
{
    public int Id { get; set; }

    public int AgentId { get; set; }

    public string Description { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string Call { get; set; } = null!;

    public string? Bearer { get; set; }

    public virtual Agent Agent { get; set; } = null!;

    public virtual ICollection<Mcpserverrequest> Mcpserverrequests { get; set; } = new List<Mcpserverrequest>();
}
