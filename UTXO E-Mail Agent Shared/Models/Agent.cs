using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Agent
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Emailaddress { get; set; } = null!;

    public string State { get; set; } = null!;

    public string Defaultlanguage { get; set; } = null!;

    public string Emailprovider { get; set; } = null!;

    public string? Emailusername { get; set; }

    public string? Emailpassword { get; set; }

    public string? Emailserver { get; set; }

    public string? Companyinformation { get; set; }

    public string? Tasktobecompleted { get; set; }

    public string Aiprovider { get; set; } = null!;

    public string Aimodel { get; set; } = null!;

    public string Emailprovidertype { get; set; } = null!;

    public int? Emailport { get; set; }

    public bool? Emailusessl { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Mcpserver> Mcpservers { get; set; } = new List<Mcpserver>();
}
