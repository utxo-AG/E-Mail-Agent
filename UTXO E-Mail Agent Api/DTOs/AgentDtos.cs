namespace UTXO_E_Mail_Agent_Api.DTOs;

public class AgentResponseDto
{
    public int Id { get; set; }
    public string Emailaddress { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Defaultlanguage { get; set; } = null!;
    public string Emailprovider { get; set; } = null!;
    public string? Emailusername { get; set; }
    public string? Emailserver { get; set; }
    public int? Emailport { get; set; }
    public bool? Emailusessl { get; set; }
    public string? Tasktobecompleted { get; set; }
    public string Aiprovider { get; set; } = null!;
    public string Aimodel { get; set; } = null!;
    public string Emailprovidertype { get; set; } = null!;
    public string? Smtpserver { get; set; }
    public int? Smtpport { get; set; }
    public string? Smtpusername { get; set; }
    public bool? Smtpusessl { get; set; }
    public DateTime? Lastpoll { get; set; }
    public bool Useconversationhistory { get; set; }
}

public class CreateAgentDto
{
    public string Emailaddress { get; set; } = null!;
    public string State { get; set; } = "active";
    public string Defaultlanguage { get; set; } = "en";
    public string Emailprovider { get; set; } = null!;
    public string? Emailusername { get; set; }
    public string? Emailpassword { get; set; }
    public string? Emailserver { get; set; }
    public int? Emailport { get; set; }
    public bool? Emailusessl { get; set; }
    public string? Tasktobecompleted { get; set; }
    public string Aiprovider { get; set; } = "anthropic";
    public string Aimodel { get; set; } = "claude-sonnet-4-20250514";
    public string Emailprovidertype { get; set; } = "imap";
    public string? Smtpserver { get; set; }
    public int? Smtpport { get; set; }
    public string? Smtpusername { get; set; }
    public string? Smtppassword { get; set; }
    public bool? Smtpusessl { get; set; }
    public bool Useconversationhistory { get; set; } = false;
}

public class UpdateAgentDto
{
    public string? Emailaddress { get; set; }
    public string? State { get; set; }
    public string? Defaultlanguage { get; set; }
    public string? Emailprovider { get; set; }
    public string? Emailusername { get; set; }
    public string? Emailpassword { get; set; }
    public string? Emailserver { get; set; }
    public int? Emailport { get; set; }
    public bool? Emailusessl { get; set; }
    public string? Tasktobecompleted { get; set; }
    public string? Aiprovider { get; set; }
    public string? Aimodel { get; set; }
    public string? Emailprovidertype { get; set; }
    public string? Smtpserver { get; set; }
    public int? Smtpport { get; set; }
    public string? Smtpusername { get; set; }
    public string? Smtppassword { get; set; }
    public bool? Smtpusessl { get; set; }
    public bool? Useconversationhistory { get; set; }
}
