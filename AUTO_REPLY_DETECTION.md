# Auto-Reply Detection Documentation

## Overview
The UTXO E-Mail Agent now includes automatic detection of out-of-office messages and other auto-replies to prevent email loops.

## How It Works

### Detection Patterns
The system checks for the following patterns in incoming emails:
- **Out of Office** / Abwesenheitsnotiz / Absence Message
- **Auto-Reply** / Automatic Reply / Automatische Antwort
- **Vacation** / Urlaub / Holiday Message
- **Currently unavailable** / Derzeit nicht erreichbar
- **Will return** / Bin zurück am
- Email headers: Auto-Submitted, X-Auto-Response-Suppress, Precedence: bulk/auto_reply

### Response Behavior
When an auto-reply is detected:
1. **No email is sent** - All email response fields return `null`
2. **Record is saved** - The email is still stored in the database to mark it as processed
3. **AI explanation provided** - The system explains why no action was taken
4. **Prevents email loops** - Stops infinite back-and-forth with auto-responders

## Implementation Details

### System Prompt Addition (ProcessMailsClass.cs)
The detection logic is implemented in the system prompt at lines 118-137:

```csharp
promptBuilder.AppendLine("KRITISCH - AUTO-REPLY ERKENNUNG:");
promptBuilder.AppendLine("Prüfe ZUERST ob die E-Mail eine automatische Antwort ist!");
promptBuilder.AppendLine("Automatische Antworten NIEMALS beantworten. Erkennungsmerkmale:");
// ... pattern list ...
promptBuilder.AppendLine("Falls es eine automatische Antwort ist, gib NUR zurück:");
promptBuilder.AppendLine("{");
promptBuilder.AppendLine("  \"EmailResponseText\": null,");
promptBuilder.AppendLine("  \"EmailResponseSubject\": null,");
promptBuilder.AppendLine("  \"EmailResponseHtml\": null,");
promptBuilder.AppendLine("  \"AiExplanation\": \"Automatische Antwort erkannt - keine Aktion erforderlich\",");
promptBuilder.AppendLine("  \"attachments\": []");
promptBuilder.AppendLine("}");
```

### API Behavior
The `/api/processtext` endpoint respects this detection:
- Returns `success: true` (request processed successfully)
- All email fields are `null` (no email to send)
- `aiExplanation` explains the detection

## Testing

### Test Files Created
- `test-autoreply.json` - English out-of-office message
- `test-vacation-reply.json` - German vacation auto-reply
- `test-normal-email.json` - Normal customer inquiry

### Test Results
✅ **Out-of-Office Detection**: Returns null responses with explanation
✅ **Vacation Reply Detection**: Correctly identified vacation patterns
✅ **Normal Email Processing**: Still generates proper responses
✅ **MCP Tool Integration**: Tools continue to work for normal emails

### Example Test Commands
```bash
# Test auto-reply detection
curl -X POST http://localhost:5050/api/processtext \
  -H "Content-Type: application/json" \
  -d @test-autoreply.json

# Expected response:
{
  "success": true,
  "emailResponseText": null,
  "emailResponseSubject": null,
  "emailResponseHtml": null,
  "aiExplanation": "Automatische Antwort erkannt...",
  "attachments": []
}
```

## Benefits
1. **Prevents Email Loops** - No infinite exchanges with auto-responders
2. **Saves Resources** - Avoids processing unnecessary responses
3. **Maintains Database Integrity** - Still tracks received emails
4. **Multilingual Support** - Detects patterns in English and German
5. **Header-Based Detection** - Also checks email headers for auto-reply indicators

## Configuration
No additional configuration required. The feature is automatically active for all agents.

## Future Enhancements
- Add more language patterns (French, Spanish, etc.)
- Configurable detection patterns per agent
- Whitelist for specific auto-replies that should receive responses
- Statistics tracking for auto-reply detection rates