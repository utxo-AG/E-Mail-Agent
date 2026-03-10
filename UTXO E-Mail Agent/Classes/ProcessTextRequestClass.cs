namespace UTXO_E_Mail_Agent.Classes;

public class ProcessTextRequestClass
{
        public string? TextContent { get; set; }
        public int? AgentId { get; set; }
}

public class ProcessMailRequestClass
{
        public string MessageId { get; set; }
        public string AgentName { get; set; }
        public string From { get; set; }

        public string[] To { get; set; }

        public string Subject { get; set; }

        public string Status { get; set; }

        public string CreatedAt { get; set; }

        public bool? HasAttachments { get; set; }

        public string[] Cc { get; set; }

        public string[] Bcc { get; set; }

        public string[] ReplyTo { get; set; }

        public string Html { get; set; }

        public string Text { get; set; }

        public string[] Attachments { get; set; }
}