using Newtonsoft.Json;

namespace UTXO_E_Mail_Agent.Classes;

public class MailClass
{
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("from", NullValueHandling = NullValueHandling.Ignore)]
        public string From { get; set; }

        [JsonProperty("to", NullValueHandling = NullValueHandling.Ignore)]
        public string[] To { get; set; }

        [JsonProperty("subject", NullValueHandling = NullValueHandling.Ignore)]
        public string Subject { get; set; }

        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
        public string CreatedAt { get; set; }

        [JsonProperty("has_attachments", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasAttachments { get; set; }

        [JsonProperty("cc", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Cc { get; set; }

        [JsonProperty("bcc", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Bcc { get; set; }

        [JsonProperty("reply_to", NullValueHandling = NullValueHandling.Ignore)]
        public string[] ReplyTo { get; set; }

        [JsonProperty("html", NullValueHandling = NullValueHandling.Ignore)]
        public string Html { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("attachments", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Attachments { get; set; }

}