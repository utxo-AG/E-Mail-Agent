#!/usr/bin/env python3
"""
Test script for the new semantic call_api MCP tool.
Tests calling the RST Störungen API via agent_id and api_name.
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

    work_dir = tempfile.mkdtemp(prefix="call_api_test_")
    print(f"Working directory: {work_dir}")

    # Agent ID that has the "stoerungen" API configured
    # You may need to adjust this based on your database
    AGENT_ID = 8  # stoerungen1@agents.utxoag.com
    
    # System prompt
    system_prompt = f"""Du bist ein API-Test-Assistent.

Du hast Zugriff auf APIs über das Tool 'mcp__utxo-http__call_api'.

Tool-Aufruf:
  mcp__utxo-http__call_api(agent_id={AGENT_ID}, api_name="<name>", data="<json>")

Verfügbare APIs für Agent {AGENT_ID}:
  - stoerungen (GET): Listet alle aktuellen Störungen auf

Beispiel:
  mcp__utxo-http__call_api(agent_id={AGENT_ID}, api_name="stoerungen")
"""

    # User prompt
    user_prompt = f"""Bitte rufe die Störungs-API auf und liste alle Störungen auf.

Verwende: mcp__utxo-http__call_api(agent_id={AGENT_ID}, api_name="stoerungen")

Zeige mir dann:
- Anzahl der Störungen
- Details zu jeder Störung (ID, Titel, Beschreibung, Status)
"""

    # Only allow the new call_api tool
    allowed_tools = [
        "mcp__utxo-http__call_api",
        "mcp__utxo-http__list_apis"
    ]

    # MCP server configuration - reads DB connection from its own appsettings.json
    mcp_servers = {
        "utxo-http": {
            "command": "/usr/local/share/dotnet/dotnet",
            "args": ["/Users/saschatobler/RiderProjects/UTXO E-Mail Agent/UTXO E-Mail Agent McpServer/bin/Debug/net9.0/UTXO E-Mail Agent McpServer.dll"]
        }
    }

    print("\n" + "=" * 60)
    print("Testing call_api MCP Tool")
    print("=" * 60)
    print(f"Agent ID: {AGENT_ID}")
    print(f"API Name: stoerungen")
    print(f"Allowed tools: {allowed_tools}")
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
                            print(f"[Claude] {text[:400]}..." if len(text) > 400 else f"[Claude] {text}")
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
                            result_preview = str(content)[:600]
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
    
    call_api_used = 'mcp__utxo-http__call_api' in tools_used
    
    if call_api_used:
        print("\n🎉 SUCCESS: call_api Tool wurde verwendet!")
    else:
        print("\n⚠️  WARNING: call_api Tool wurde NICHT verwendet!")
    
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
