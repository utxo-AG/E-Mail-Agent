#!/usr/bin/env python3
"""
Test script to verify MCP HTTP server with RST Störungen API.
Explicitly instructs Claude to use MCP HTTP tools (not WebFetch).
"""

import asyncio
import os
import sys
import tempfile

async def main():
    try:
        from claude_code_sdk import ClaudeSDKClient, ClaudeCodeOptions
        print("✓ claude_code_sdk imported successfully")
    except ImportError as e:
        print(f"✗ Failed to import claude_code_sdk: {e}")
        sys.exit(1)

    work_dir = tempfile.mkdtemp(prefix="mcp_stoerungen_")
    print(f"Working directory: {work_dir}")

    # System prompt that EXPLICITLY requires MCP HTTP tools
    system_prompt = """Du bist ein API-Test-Assistent.

WICHTIG: Du MUSST die MCP HTTP-Tools verwenden! WebFetch ist NICHT erlaubt für diese Aufgabe.

Verfügbare MCP HTTP-Tools:
- mcp__utxo-http__http_get: Für GET-Requests (Parameter: url, bearerToken optional)
- mcp__utxo-http__http_post: Für POST-Requests (Parameter: url, body, bearerToken optional)
- mcp__utxo-http__http_request: Für alle HTTP-Methoden (Parameter: method, url, body, headers)

Verwende IMMER mcp__utxo-http__http_get für GET-Requests.
Verwende NIEMALS WebFetch oder Bash curl.
"""

    # User prompt - list all Störungen
    user_prompt = """Bitte rufe die RST Störungs-API auf: https://rst.de/api/stoerungen

WICHTIG: Verwende das Tool mcp__utxo-http__http_get (NICHT WebFetch!)

Liste dann alle Störungen auf mit:
- ID
- Titel
- Beschreibung
- Status
- Erstellungsdatum
"""

    # Only allow MCP tools and basic tools (explicitly NO WebFetch)
    allowed_tools = [
        "Read", "Write", "Edit", "Glob", "Grep",
        "mcp__utxo-http__http_request",
        "mcp__utxo-http__http_get", 
        "mcp__utxo-http__http_post"
    ]

    # MCP server configuration
    mcp_servers = {
        "utxo-http": {
            "command": "/usr/local/share/dotnet/dotnet",
            "args": ["/Users/saschatobler/RiderProjects/UTXO E-Mail Agent/UTXO E-Mail Agent McpServer/bin/Debug/net9.0/UTXO E-Mail Agent McpServer.dll"]
        }
    }

    print("\n" + "=" * 60)
    print("Testing MCP HTTP Server - RST Störungen API")
    print("=" * 60)
    print(f"URL: https://rst.de/api/stoerungen")
    print(f"Allowed tools: {allowed_tools}")
    print(f"WebFetch: DISABLED")
    print("=" * 60 + "\n")

    options = ClaudeCodeOptions(
        system_prompt=system_prompt,
        max_turns=10,
        allowed_tools=allowed_tools,
        cwd=work_dir,
        permission_mode="default",
        mcp_servers=mcp_servers
    )

    full_response = ""
    tools_used = []
    
    async with ClaudeSDKClient(options) as client:
        await client.query(user_prompt)
        
        async for message in client.receive_response():
            msg_type = type(message).__name__
            
            if msg_type == "AssistantMessage":
                if hasattr(message, 'content'):
                    for block in message.content:
                        block_type = type(block).__name__
                        if block_type == "TextBlock":
                            text = block.text
                            print(f"[Claude] {text[:300]}..." if len(text) > 300 else f"[Claude] {text}")
                            full_response += text + "\n"
                        elif block_type == "ToolUseBlock":
                            tool_name = block.name if hasattr(block, 'name') else str(block)
                            tool_input = block.input if hasattr(block, 'input') else {}
                            tools_used.append(tool_name)
                            print(f"[Tool] {tool_name}")
                            print(f"  Input: {tool_input}")
            
            elif msg_type == "UserMessage":
                if hasattr(message, 'content'):
                    for block in message.content:
                        block_type = type(block).__name__
                        if block_type == "ToolResultBlock":
                            content = block.content if hasattr(block, 'content') else str(block)
                            if isinstance(content, list) and len(content) > 0:
                                content = content[0].get('text', str(content)) if isinstance(content[0], dict) else str(content)
                            result_preview = str(content)[:500]
                            print(f"[Result] {result_preview}...")
            
            elif msg_type == "ResultMessage":
                print(f"\n[Done] Task completed!")
                if hasattr(message, 'cost_usd'):
                    print(f"  Cost: ${message.cost_usd:.4f}")
                if hasattr(message, 'duration_ms'):
                    print(f"  Duration: {message.duration_ms}ms")

    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    print(f"Tools used: {tools_used}")
    
    mcp_used = any('mcp__utxo-http' in t for t in tools_used)
    webfetch_used = 'WebFetch' in tools_used
    bash_used = 'Bash' in tools_used
    
    print(f"\n✓ MCP HTTP tool used: {mcp_used}")
    print(f"✗ WebFetch used: {webfetch_used}")
    print(f"✗ Bash/curl used: {bash_used}")
    
    if mcp_used and not webfetch_used and not bash_used:
        print("\n🎉 SUCCESS: MCP HTTP Server wurde korrekt verwendet!")
    else:
        print("\n⚠️  WARNING: MCP HTTP Server wurde NICHT verwendet!")
    
    print("\n" + "=" * 60)
    print("FULL RESPONSE")
    print("=" * 60)
    print(full_response)

    # Cleanup
    import shutil
    try:
        shutil.rmtree(work_dir)
    except:
        pass

if __name__ == "__main__":
    asyncio.run(main())
