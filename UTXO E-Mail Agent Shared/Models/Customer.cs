using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Customer
{
    public int Id { get; set; }

    public string? Companyname { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Street { get; set; } = null!;

    public string Zip { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string Emailaddress { get; set; } = null!;

    public string State { get; set; } = null!;

    public DateTime Created { get; set; }

    public string? Username { get; set; }

    public string? Passwordhash { get; set; }

    public int? PackageId { get; set; }

    public string? Companyinformation { get; set; }

    public virtual ICollection<Agent> Agents { get; set; } = new List<Agent>();

    public virtual Package? Package { get; set; }
}
