using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Apikey
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Apikey1 { get; set; } = null!;

    public DateTime Created { get; set; }

    public DateTime? Expires { get; set; }

    public string State { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;
}
