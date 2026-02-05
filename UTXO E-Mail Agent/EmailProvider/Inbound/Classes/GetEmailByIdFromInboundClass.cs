using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;

namespace UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

    public partial class GetEmailByIdFromInboundClass : MailClass
    {
        [JsonProperty("object", NullValueHandling = NullValueHandling.Ignore)]
        public string Object { get; set; }

        [JsonProperty("sent_at")]
        public object SentAt { get; set; }

        [JsonProperty("scheduled_at")]
        public object ScheduledAt { get; set; }

        [JsonProperty("is_read", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsRead { get; set; }

        [JsonProperty("thread_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ThreadId { get; set; }

        [JsonProperty("thread_position", NullValueHandling = NullValueHandling.Ignore)]
        public long? ThreadPosition { get; set; }

        [JsonProperty("headers", NullValueHandling = NullValueHandling.Ignore)]
        public Headers Headers { get; set; }
        
        public MailClass? ToMailsClass()
        {
            return new MailClass
            {
                Id = this.Id,
                Type = this.Type,
                From = this.From,
                To = this.To,
                Subject = this.Subject,
                Status = this.Status,
                CreatedAt = this.CreatedAt,
                HasAttachments = this.HasAttachments,
                Cc = this.Cc,
                Bcc = this.Bcc,
                ReplyTo = this.ReplyTo,
                Html = this.Html,
                Text = this.Text,
                Attachments = this.Attachments
            };
        }

    }

    public partial class Headers
    {
        [JsonProperty("return-path", NullValueHandling = NullValueHandling.Ignore)]
        public From ReturnPath { get; set; }

        [JsonProperty("received", NullValueHandling = NullValueHandling.Ignore)]
        public string Received { get; set; }

        [JsonProperty("received-spf", NullValueHandling = NullValueHandling.Ignore)]
        public string ReceivedSpf { get; set; }

        [JsonProperty("authentication-results", NullValueHandling = NullValueHandling.Ignore)]
        public string[] AuthenticationResults { get; set; }

        [JsonProperty("x-ses-receipt", NullValueHandling = NullValueHandling.Ignore)]
        public string XSesReceipt { get; set; }

        [JsonProperty("x-ses-dkim-signature", NullValueHandling = NullValueHandling.Ignore)]
        public string XSesDkimSignature { get; set; }

        [JsonProperty("from", NullValueHandling = NullValueHandling.Ignore)]
        public From From { get; set; }

        [JsonProperty("dkim-signature", NullValueHandling = NullValueHandling.Ignore)]
        public DkimSignature DkimSignature { get; set; }

        [JsonProperty("content-type", NullValueHandling = NullValueHandling.Ignore)]
        public ContentType ContentType { get; set; }

        [JsonProperty("mime-version", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeVersion { get; set; }

        [JsonProperty("subject", NullValueHandling = NullValueHandling.Ignore)]
        public string Subject { get; set; }

        [JsonProperty("message-id", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }

        [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? Date { get; set; }

        [JsonProperty("to", NullValueHandling = NullValueHandling.Ignore)]
        public From To { get; set; }

        [JsonProperty("x-spamd-bar", NullValueHandling = NullValueHandling.Ignore)]
        public string XSpamdBar { get; set; }
    }

    public partial class ContentType
    {
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public ContentTypeParams Params { get; set; }
    }

    public partial class ContentTypeParams
    {
        [JsonProperty("boundary", NullValueHandling = NullValueHandling.Ignore)]
        public string Boundary { get; set; }
    }

    public partial class DkimSignature
    {
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public DkimSignatureParams Params { get; set; }
    }

    public partial class DkimSignatureParams
    {
        [JsonProperty("a", NullValueHandling = NullValueHandling.Ignore)]
        public string A { get; set; }

        [JsonProperty("c", NullValueHandling = NullValueHandling.Ignore)]
        public string C { get; set; }

        [JsonProperty("d", NullValueHandling = NullValueHandling.Ignore)]
        public string D { get; set; }

        [JsonProperty("s", NullValueHandling = NullValueHandling.Ignore)]
        public string S { get; set; }

        [JsonProperty("t", NullValueHandling = NullValueHandling.Ignore)]
        public string T { get; set; }

        [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
        public string H { get; set; }

        [JsonProperty("bh", NullValueHandling = NullValueHandling.Ignore)]
        public string Bh { get; set; }

        [JsonProperty("b", NullValueHandling = NullValueHandling.Ignore)]
        public string B { get; set; }
    }

    public partial class From
    {
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public Value[] Value { get; set; }

        [JsonProperty("html", NullValueHandling = NullValueHandling.Ignore)]
        public string Html { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
    }

    public partial class Value
    {
        [JsonProperty("address", NullValueHandling = NullValueHandling.Ignore)]
        public string Address { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
    }

    