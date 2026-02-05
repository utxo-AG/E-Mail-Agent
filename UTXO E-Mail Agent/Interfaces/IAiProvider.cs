using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Interfaces;

public interface IAiProvider
{
    public Task<AiResponseClass> GenerateResponse(string systemPrompt, string prompt, MailClass mailClass, Agent agent, Conversation conversation);
}