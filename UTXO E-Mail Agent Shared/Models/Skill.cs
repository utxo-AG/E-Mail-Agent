using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Skill
{
    public int Id { get; set; }

    public int AgentId { get; set; }

    public string Skillname { get; set; } = null!;

    public string State { get; set; } = null!;

    public byte[]? Skillfiles { get; set; }

    public string Filetype { get; set; } = null!;

    public string? Skillid { get; set; }

    public virtual Agent Agent { get; set; } = null!;
}
