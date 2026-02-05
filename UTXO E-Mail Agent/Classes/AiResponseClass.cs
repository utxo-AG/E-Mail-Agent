using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

namespace UTXO_E_Mail_Agent.Classes;

public class AiResponseClass
{
    public string EmailResponseText { get; set; } = string.Empty;
    public string EmailResponseHtml { get; set; } = string.Empty;
    public string EmailResponseSubject { get; set; } = string.Empty;
    public string? AiExplanation { get; set; }  // Text vor dem JSON (optional)
    public Attachment[] Attachments { get; set; }
}