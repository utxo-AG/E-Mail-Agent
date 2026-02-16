using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Server
{
    public int Id { get; set; }

    public string Servername { get; set; } = null!;

    public DateTime? Lastlifesign { get; set; }

    public string State { get; set; } = null!;
}
