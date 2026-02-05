# Dynamische MCP Server aus der Datenbank

## Übersicht

Das System lädt automatisch MCP Server-Definitionen aus der `mcpserver` Tabelle und macht sie für Claude verfügbar.

## Datenbank-Schema

```sql
CREATE TABLE mcpserver (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    AgentId INT NOT NULL,
    Name VARCHAR(255) NOT NULL,          -- Tool-Name für Claude
    Description TEXT NOT NULL,           -- Beschreibung was das Tool macht
    Url VARCHAR(500) NOT NULL,           -- API-Endpunkt URL
    Call VARCHAR(10) NOT NULL,           -- HTTP Method: GET, POST, PUT, DELETE
    FOREIGN KEY (AgentId) REFERENCES agent(Id)
);
```

## Beispiele

### 1. GET Request (Parameter in URL)

**Use Case:** Produktinformationen abrufen

```sql
INSERT INTO mcpserver (AgentId, Name, Description, Url, Call) VALUES (
    1,
    'get_product_info',
    'Ruft Produktinformationen ab. Parameter: productId (string) - Die Produkt-ID',
    'https://api.example.com/products',
    'GET'
);
```

**Claude wird aufrufen:**
```
GET https://api.example.com/products?productId=ABC123
```

### 2. POST Request (JSON Body)

**Use Case:** Kundendaten suchen

```sql
INSERT INTO mcpserver (AgentId, Name, Description, Url, Call) VALUES (
    1,
    'search_customer',
    'Sucht nach Kunden. Übergeben muss ein JSON im Format: {"email": "string", "name": "string"}. Mindestens ein Parameter muss angegeben werden.',
    'https://api.example.com/customers/search',
    'POST'
);
```

**Claude wird aufrufen:**
```
POST https://api.example.com/customers/search
Content-Type: application/json

{
  "email": "kunde@example.com",
  "name": "Max Mustermann"
}
```

### 3. DELETE Request

**Use Case:** Temporäre Daten löschen

```sql
INSERT INTO mcpserver (AgentId, Name, Description, Url, Call) VALUES (
    1,
    'delete_cache',
    'Löscht Cache-Einträge. Parameter: cacheKey (string) - Der zu löschende Cache-Key',
    'https://api.example.com/cache',
    'DELETE'
);
```

**Claude wird aufrufen:**
```
DELETE https://api.example.com/cache?cacheKey=user_123
```

### 4. Komplexes Beispiel - Bestellstatus

```sql
INSERT INTO mcpserver (AgentId, Name, Description, Url, Call) VALUES (
    1,
    'get_order_status',
    'Prüft den Status einer Bestellung. Parameter: orderId (string) - Die Bestellnummer',
    'https://api.example.com/orders/status',
    'GET'
);
```

### 5. Komplexes POST - Ticket erstellen

```sql
INSERT INTO mcpserver (AgentId, Name, Description, Url, Call) VALUES (
    1,
    'create_support_ticket',
    'Erstellt ein Support-Ticket. JSON Format: {"subject": "string", "description": "string", "priority": "low|medium|high", "customerEmail": "string"}',
    'https://api.example.com/support/tickets',
    'POST'
);
```

## Wie Claude die Tools verwendet

Claude sieht die Tools und deren Beschreibungen und entscheidet selbst, wann sie verwendet werden:

```
User: "Wie ist der Status meiner Bestellung #12345?"

Claude denkt:
- Ich habe ein Tool "get_order_status"
- Es benötigt eine orderId
- Ich rufe es auf

→ GET https://api.example.com/orders/status?orderId=12345
→ Erhält Response: {"status": "shipped", "tracking": "..."}
→ Antwortet: "Ihre Bestellung wurde versendet. Tracking-Nummer: ..."
```

## HTTP Methoden Verhalten

| Method | Parameter-Übergabe | Content-Type |
|--------|-------------------|--------------|
| GET    | Query String (?key=value) | - |
| POST   | JSON Body | application/json |
| PUT    | JSON Body | application/json |
| DELETE | Query String (?key=value) | - |

## Parameter-Format

Claude übergibt Parameter als JSON-String:

```json
{
  "productId": "ABC123",
  "quantity": 5,
  "includeDetails": true
}
```

### Bei GET/DELETE:
Wird automatisch zu: `?productId=ABC123&quantity=5&includeDetails=true`

### Bei POST/PUT:
Wird als JSON Body gesendet (wie oben)

## Best Practices für Descriptions

1. **Klar beschreiben, was das Tool macht**
   ```
   "Sucht nach Kunden in der Datenbank basierend auf E-Mail oder Namen"
   ```

2. **Parameter-Format explizit angeben**
   ```
   "JSON Format: {\"email\": \"string\", \"limit\": number}"
   ```

3. **Optionale vs. Required Parameter kennzeichnen**
   ```
   "Parameter: email (required), name (optional), limit (optional, default: 10)"
   ```

4. **Beispiele geben**
   ```
   "Beispiel: {\"orderId\": \"12345\", \"includeItems\": true}"
   ```

## Debugging

Die Logs zeigen alle MCP-Aufrufe:

```
[MCP] Loading 3 MCP server(s) for agent 1
[MCP] Registering tool: get_product_info
[MCP] Registering tool: search_customer
[MCP get_product_info] Executing GET to https://api.example.com/products
[MCP get_product_info] Success: 200
```

## API Response Format

Das Tool gibt die rohe API-Response an Claude zurück. Es ist hilfreich, wenn deine APIs strukturierte JSON-Responses zurückgeben:

```json
{
  "success": true,
  "data": {
    "productName": "Widget",
    "price": 29.99,
    "inStock": true
  }
}
```

## Fehlerbehandlung

Bei API-Fehlern wird der Status-Code und die Response an Claude zurückgegeben:

```
ERROR (404): {"error": "Product not found"}
```

Claude kann darauf reagieren und dem User eine passende Antwort geben.

## Sicherheit

**WICHTIG:**
- Stelle sicher, dass die APIs nur für den internen Gebrauch sind
- Verwende API-Keys/Authentication in deinen APIs
- Claude kann alle konfigurierten Tools aufrufen
- Setze `.AllowTools("tool1", "tool2")` statt `.AllowAllTools()` wenn du die Tool-Nutzung einschränken willst

## Mehrere Agents

Jeder Agent kann seine eigenen MCP Server haben:

```sql
-- Agent 1 (Vertrieb) hat Zugriff auf Produkt-APIs
INSERT INTO mcpserver (AgentId, Name, ...) VALUES (1, 'get_product', ...);

-- Agent 2 (Support) hat Zugriff auf Ticket-APIs
INSERT INTO mcpserver (AgentId, Name, ...) VALUES (2, 'create_ticket', ...);
```

## Testing

Um einen MCP Server zu testen, füge ihn in die DB ein und führe den Agent aus.
Schreibe eine Test-E-Mail, die das Tool verwenden sollte:

```
"Kannst du mir Infos zum Produkt ABC123 geben?"
```

Claude wird automatisch das `get_product_info` Tool aufrufen.
