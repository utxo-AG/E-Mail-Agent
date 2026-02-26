using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

namespace UTXO_E_Mail_Agent.Classes;

/// <summary>
/// Custom JSON converter that handles attachments as either:
/// - Array of strings (file paths)
/// - Array of Attachment objects
/// </summary>
public class AttachmentArrayConverter : JsonConverter<Attachment[]>
{
    public override Attachment[]? ReadJson(JsonReader reader, Type objectType, Attachment[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return Array.Empty<Attachment>();

        var token = JToken.Load(reader);
        
        if (token.Type == JTokenType.Array)
        {
            var attachments = new List<Attachment>();
            
            foreach (var item in token.Children())
            {
                if (item.Type == JTokenType.String)
                {
                    // String path - convert to Attachment object
                    var path = item.Value<string>();
                    if (!string.IsNullOrEmpty(path))
                    {
                        attachments.Add(new Attachment
                        {
                            Path = path,
                            Filename = System.IO.Path.GetFileName(path)
                        });
                    }
                }
                else if (item.Type == JTokenType.Object)
                {
                    // Already an Attachment object
                    var attachment = item.ToObject<Attachment>(serializer);
                    if (attachment != null)
                        attachments.Add(attachment);
                }
            }
            
            return attachments.ToArray();
        }
        
        return Array.Empty<Attachment>();
    }

    public override void WriteJson(JsonWriter writer, Attachment[]? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}
