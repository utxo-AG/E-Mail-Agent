#!/usr/bin/env python3
"""
Final test script for semantic MCP API calls.
Shows exactly how Claude uses call_api with agent_id and api_name.
"""

import asyncio
import tempfile

async def main():
    from claude_code_sdk import ClaudeSDKClient, ClaudeCodeOptions
    
    # === CONFIGURATION ===
    AGENT_ID = 8  # Der Agent mit den konfigurierten APIs
    
    # MCP Server - liest DB-Connection aus seiner eigenen appsettings.json
    mcp_servers = {
        "utxo-http": {
            "command": "/usr/local/share/dotnet/dotnet",
            "args": ["/Users/saschatobler/RiderProjects/UTXO E-Mail Agent/UTXO E-Mail Agent McpServer/bin/Debug/net9.0/UTXO E-Mail Agent McpServer.dll"]
        }
    }
    
    # === SYSTEM PROMPT ===
    # Das ist der wichtige Teil - so sollte der Prompt aussehen:
    system_prompt = f"""Du bist ein Assistent der API-Aufrufe macht.

VERFÜGBARE APIs für Agent {AGENT_ID}:
Verwende das Tool 'mcp__utxo-http__call_api' um APIs aufzurufen.

Aufruf-Syntax:
  mcp__utxo-http__call_api(agent_id={AGENT_ID}, api_name="<name>", data="<optional json>")

Du kannst auch 'mcp__utxo-http__list_apis(agent_id={AGENT_ID})' verwenden um alle verfügbaren APIs zu sehen.

WICHTIG: Du kennst die URLs nicht - nur die API-Namen. Das System kümmert sich um URLs und Authentifizierung.
"""

    # === USER PROMPT ===
    user_prompt = """Bitte:
1. Liste alle verfügbaren APIs auf
2. Rufe dann die Störungs-API auf
3. Zeige mir die Ergebnisse übersichtlich an
"""

    # === ALLOWED TOOLS ===
    # Nur MCP-Tools erlauben, kein WebFetch!
    allowed_tools = [
        "mcp__utxo-http__call_api",
        "mcp__utxo-http__list_apis"
    ]

    print("=" * 60)
    print("MCP Server Test - Semantic API Calls")
    print("=" * 60)
    print(f"Agent ID: {AGENT_ID}")
    print(f"Tools: {allowed_tools}")
    print("=" * 60)
    print("\nSYSTEM PROMPT:")
    print(system_prompt)
    print("=" * 60 + "\n")

    work_dir = tempfile.mkdtemp(prefix="mcp_final_")
    
    options = ClaudeCodeOptions(
        system_prompt=system_prompt,
        max_turns=10,
        allowed_tools=allowed_tools,
        cwd=work_dir,
        permission_mode="default",
        mcp_servers=mcp_servers
    )

    async with ClaudeSDKClient(options) as client:
        await client.query(user_prompt)
        
        async for message in client.receive_response():
            msg_type = type(message).__name__
            
            if msg_type == "AssistantMessage" and hasattr(message, 'content'):
                for block in message.content:
                    block_type = type(block).__name__
                    if block_type == "TextBlock":
                        print(f"\n[Claude]\n{block.text}")
                    elif block_type == "ToolUseBlock":
                        print(f"\n[Tool Call] {block.name}")
                        print(f"  Parameters: {block.input}")
            
            elif msg_type == "UserMessage" and hasattr(message, 'content'):
                for block in message.content:
                    if type(block).__name__ == "ToolResultBlock":
                        content = block.content
                        if isinstance(content, list) and content:
                            content = content[0].get('text', str(content)) if isinstance(content[0], dict) else str(content)
                        print(f"\n[Tool Result]\n{str(content)[:800]}...")
            
            elif msg_type == "ResultMessage":
                print(f"\n{'=' * 60}")
                print(f"✅ Fertig! Dauer: {getattr(message, 'duration_ms', 'N/A')}ms")

    # Cleanup
    import shutil
    shutil.rmtree(work_dir, ignore_errors=True)

if __name__ == "__main__":
    asyncio.run(main())
