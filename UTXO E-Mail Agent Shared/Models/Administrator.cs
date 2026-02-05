using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Administrator
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Passwordhash { get; set; } = null!;

    public string State { get; set; } = null!;

    public int Loginattempts { get; set; }
}
