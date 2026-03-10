#!/usr/bin/env python3
"""
Test script to verify MCP HTTP server integration with Claude Code SDK.

This script tests if Claude can use the MCP HTTP tools to make API calls.
"""

import asyncio
import os
import sys

# Add the directory to path for imports
script_dir = os.path.dirname(os.path.abspath(__file__))

async def main():
    try:
        from claude_code_sdk import ClaudeSDKClient, ClaudeCodeOptions
        print("✓ claude_code_sdk imported successfully")
    except ImportError as e:
        print(f"✗ Failed to import claude_code_sdk: {e}")
        print("Install with: pip install claude-code-sdk")
        sys.exit(1)

    # Create a temporary working directory
    import tempfile
    work_dir = tempfile.mkdtemp(prefix="mcp_test_")
    print(f"Working directory: {work_dir}")

    # System prompt that instructs Claude to use the MCP HTTP tool
    system_prompt = """Du bist ein Test-Assistent.
Deine Aufgabe ist es, einen HTTP GET-Request zu machen.

VERFÜGBARE MCP HTTP-TOOLS:
- mcp__utxo-http__http_get: Für GET-Requests (Parameter: url)
- mcp__utxo-http__http_post: Für POST-Requests (Parameter: url, body)
- mcp__utxo-http__http_request: Für beliebige HTTP-Methoden (Parameter: method, url, body, headers)

Verwende diese Tools um HTTP-Requests zu machen.
"""

    # User prompt
    user_prompt = """Bitte mache einen GET-Request auf die URL: https://rst.de/api/stoerungen

Gib mir das Ergebnis zurück. Wenn es ein JSON-Array ist, beschreibe kurz was drin steht."""

    # Allowed tools - including MCP HTTP tools
    allowed_tools = [
        "Read", "Write", "Edit", "Bash", "WebFetch", "Glob", "Grep",
        "mcp__utxo-http__http_request",
        "mcp__utxo-http__http_get", 
        "mcp__utxo-http__http_post"
    ]

    print("\n" + "=" * 60)
    print("Testing MCP HTTP Server with Claude Code SDK")
    print("=" * 60)
    print(f"Allowed tools: {allowed_tools}")
    print(f"Target URL: https://rst.de/api/stoerungen")
    print("=" * 60 + "\n")

    # MCP server configuration - point to the utxo-http server
    # Use full path to dotnet since it may not be in PATH
    mcp_servers = {
        "utxo-http": {
            "command": "/usr/local/share/dotnet/dotnet",
            "args": ["/Users/saschatobler/RiderProjects/UTXO E-Mail Agent/UTXO E-Mail Agent McpServer/bin/Debug/net9.0/UTXO E-Mail Agent McpServer.dll"]
        }
    }
    
    options = ClaudeCodeOptions(
        system_prompt=system_prompt,
        max_turns=10,
        allowed_tools=allowed_tools,
        cwd=work_dir,
        permission_mode="default",
        mcp_servers=mcp_servers
    )

    print("[Query] Sending request to Claude...")
    full_response = ""
    tools_used = []
    
    async with ClaudeSDKClient(options) as client:
        await client.query(user_prompt)
        
        async for message in client.receive_response():
            msg_type = type(message).__name__
            print(f"[Message] Type: {msg_type}")
            
            if msg_type == "AssistantMessage":
                if hasattr(message, 'content'):
                    for block in message.content:
                        block_type = type(block).__name__
                        if block_type == "TextBlock":
                            print(f"  [Text] {block.text[:200]}..." if len(block.text) > 200 else f"  [Text] {block.text}")
                            full_response += block.text + "\n"
                        elif block_type == "ToolUseBlock":
                            tool_name = block.name if hasattr(block, 'name') else str(block)
                            tool_input = block.input if hasattr(block, 'input') else {}
                            tools_used.append(tool_name)
                            print(f"  [Tool] {tool_name}")
                            print(f"  [Input] {tool_input}")
                        elif block_type == "ThinkingBlock":
                            thinking = block.thinking[:100] if hasattr(block, 'thinking') else str(block)[:100]
                            print(f"  [Thinking] {thinking}...")
            
            elif msg_type == "UserMessage":
                if hasattr(message, 'content'):
                    for block in message.content:
                        block_type = type(block).__name__
                        if block_type == "ToolResultBlock":
                            result = block.content[:200] if hasattr(block, 'content') and block.content else "No content"
                            print(f"  [ToolResult] {result}...")
            
            elif msg_type == "ResultMessage":
                print(f"[Result] Task completed!")
                if hasattr(message, 'cost_usd'):
                    print(f"  Cost: ${message.cost_usd:.4f}")
                if hasattr(message, 'duration_ms'):
                    print(f"  Duration: {message.duration_ms}ms")

    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    print(f"Tools used: {tools_used}")
    print(f"\nMCP HTTP tool used: {'mcp__utxo-http' in ' '.join(tools_used)}")
    print(f"Bash/curl fallback: {'Bash' in tools_used}")
    print("\n[Full Response]")
    print(full_response)
    
    # Cleanup
    import shutil
    try:
        shutil.rmtree(work_dir)
    except:
        pass

if __name__ == "__main__":
    asyncio.run(main())
