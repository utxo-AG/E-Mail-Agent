using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;

namespace UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes
{
    public partial class ListEmailsFromInboundClass
    {
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public Datum[] Data { get; set; }

        [JsonProperty("pagination", NullValueHandling = NullValueHandling.Ignore)]
        public Pagination Pagination { get; set; }

        [JsonProperty("filters", NullValueHandling = NullValueHandling.Ignore)]
        public Filters Filters { get; set; }

        public ListNewEmailsClass[]? ToListNewEmailsClass()
        {
            List<ListNewEmailsClass> listNewEmailsClass = new List<ListNewEmailsClass>();
            foreach (var d in Data.Where(x=>x.IsAlreadyRead()==false))
            {
                ListNewEmailsClass l = new ListNewEmailsClass() { Id = d.Id, CreatedAt = d.CreatedAt, From = d.From, HasAttachments = d.HasAttachments, Status = d.Status, Subject = d.Subject, To = d.To, Type = d.Type };
                listNewEmailsClass.Add(l);
            }
            return listNewEmailsClass.ToArray();
        }
    }

    public partial class Datum : ListNewEmailsClass
    {
        [JsonProperty("message_id", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }

        [JsonProperty("from_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FromName { get; set; }

        [JsonProperty("cc", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Cc { get; set; }

        [JsonProperty("preview", NullValueHandling = NullValueHandling.Ignore)]
        public string Preview { get; set; }

        [JsonProperty("sent_at", NullValueHandling = NullValueHandling.Ignore)]
        public string SentAt { get; set; }

        [JsonProperty("scheduled_at", NullValueHandling = NullValueHandling.Ignore)]
        public string ScheduledAt { get; set; }

        [JsonProperty("attachment_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? AttachmentCount { get; set; }

        [JsonProperty("is_read", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsRead { get; set; }

        [JsonProperty("read_at", NullValueHandling = NullValueHandling.Ignore)]
        public string ReadAt { get; set; }

        [JsonProperty("is_archived", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsArchived { get; set; }

        [JsonProperty("thread_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ThreadId { get; set; }

        private bool AlreadyRead { get; set; }
        public void SetAlreadyRead()
        {
            AlreadyRead = true;
        }
        public bool IsAlreadyRead()
        {
            return AlreadyRead;
        }
    }

    public partial class Filters
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("time_range", NullValueHandling = NullValueHandling.Ignore)]
        public string TimeRange { get; set; }

        [JsonProperty("search", NullValueHandling = NullValueHandling.Ignore)]
        public string Search { get; set; }

        [JsonProperty("domain", NullValueHandling = NullValueHandling.Ignore)]
        public string Domain { get; set; }

        [JsonProperty("address", NullValueHandling = NullValueHandling.Ignore)]
        public string Address { get; set; }
    }

    public partial class Pagination
    {
        [JsonProperty("limit", NullValueHandling = NullValueHandling.Ignore)]
        public long? Limit { get; set; }

        [JsonProperty("offset", NullValueHandling = NullValueHandling.Ignore)]
        public long? Offset { get; set; }

        [JsonProperty("total", NullValueHandling = NullValueHandling.Ignore)]
        public long? Total { get; set; }

        [JsonProperty("has_more", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasMore { get; set; }
    }
}
