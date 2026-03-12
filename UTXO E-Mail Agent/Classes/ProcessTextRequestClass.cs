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

        /// <summary>
        /// Array of attachment filenames (for backwards compatibility)
        /// </summary>
        public string[] Attachments { get; set; }
        
        /// <summary>
        /// Array of attachment data objects with filename, content type, and base64 content.
        /// When provided, attachments will be saved to the temp directory automatically.
        /// </summary>
        public AttachmentDataClass[] AttachmentData { get; set; }
}

/// <summary>
/// Attachment data with base64 encoded content
/// </summary>
public class AttachmentDataClass
{
        public string Filename { get; set; }
        public string ContentType { get; set; }
        /// <summary>
        /// Base64 encoded file content
        /// </summary>
        public string Content { get; set; }
}
