using System;
using System.Collections.Generic;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class Package
{
    public int Id { get; set; }

    public string Description { get; set; } = null!;

    public int Maxconvsations { get; set; }

    public double Price { get; set; }

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
