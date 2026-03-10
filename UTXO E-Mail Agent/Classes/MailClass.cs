using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UTXO_E_Mail_Agent.Classes;

public class MailClass
{
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        /// <summary>
        /// Original Inbound message ID (for reply functionality).
        /// If set, this ID will be used for reply instead of Id.
        /// </summary>
        [JsonProperty("original_message_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? OriginalMessageId { get; set; }

        /// <summary>
        /// Checks if this email has a valid Inbound message ID for reply.
        /// Inbound IDs start with "inbnd_"
        /// </summary>
        [JsonIgnore]
        public bool HasValidInboundId => 
            !string.IsNullOrEmpty(OriginalMessageId) && OriginalMessageId.StartsWith("inbnd_") ||
            !string.IsNullOrEmpty(Id) && Id.StartsWith("inbnd_");

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
        [JsonConverter(typeof(FlexibleAttachmentsConverter))]
        public string[] Attachments { get; set; }

}

/// <summary>
/// Converter that handles attachments as either:
/// - Array of strings (file paths/IDs)
/// - Array of objects (extracts filename or id as string)
/// </summary>
public class FlexibleAttachmentsConverter : JsonConverter<string[]>
{
    public override string[]? ReadJson(JsonReader reader, Type objectType, string[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var token = JToken.Load(reader);
        
        if (token.Type == JTokenType.Array)
        {
            var results = new List<string>();
            
            foreach (var item in token.Children())
            {
                if (item.Type == JTokenType.String)
                {
                    // Already a string
                    var value = item.Value<string>();
                    if (!string.IsNullOrEmpty(value))
                        results.Add(value);
                }
                else if (item.Type == JTokenType.Object)
                {
                    // Object - extract filename, id, or content_id
                    var filename = item["filename"]?.Value<string>() 
                                ?? item["id"]?.Value<string>()
                                ?? item["content_id"]?.Value<string>();
                    if (!string.IsNullOrEmpty(filename))
                        results.Add(filename);
                }
            }
            
            return results.ToArray();
        }
        
        return Array.Empty<string>();
    }

    public override void WriteJson(JsonWriter writer, string[]? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}