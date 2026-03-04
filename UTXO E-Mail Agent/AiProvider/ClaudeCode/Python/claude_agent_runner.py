"""
Claude Agent SDK Runner
This script interfaces with the claude-agent-sdk to run Claude Code agents.
Called from C# as a subprocess with a JSON input file.
"""

import asyncio
import json
import os
import sys
from typing import Optional

try:
    from claude_agent_sdk import (
        AssistantMessage,
        ClaudeAgentOptions,
        ClaudeSDKClient,
        ResultMessage,
        TextBlock,
        ToolUseBlock,
        ToolResultBlock,
        UserMessage,
        SystemMessage,
    )
except ImportError as e:
    print(json.dumps({
        "success": False,
        "error": f"claude-agent-sdk not installed or import error: {str(e)}. Run: pip install claude-agent-sdk"
    }))
    sys.exit(1)


# Track tool usage for debugging
tool_usage_log = []


def log(msg: str):
    """Log to stderr so C# can capture it."""
    print(msg, file=sys.stderr, flush=True)


async def run_email_agent_async(
    system_prompt: str,
    user_prompt: str,
    email_subject: str,
    email_text: str,
    email_html: Optional[str],
    email_from: str,
    working_directory: str,
    max_turns: int = 20,
    model: Optional[str] = None
) -> dict:
    """
    Run Claude Code agent to process an email and generate a response.
    Uses the API key stored by Claude Code CLI (in ~/.claude/).
    
    Supported models: claude-sonnet-4-20250514, claude-opus-4-5-20251101, etc.
    """
    
    # Build the full prompt
    full_prompt = f"""
{user_prompt}

---
E-Mail Subject: {email_subject}
E-Mail From: {email_from}
E-Mail Text:
{email_text}
"""
    
    if email_html:
        full_prompt += f"""
E-Mail HTML:
{email_html}
"""
    
    try:
        # Clear tool usage log
        tool_usage_log.clear()
        
        # Build allowed tools list
        # Skills are loaded from ~/.claude/skills/ (global) or .claude/skills/ (project)
        # Bash is always enabled - Claude uses curl for API calls with parameters from system prompt
        allowed_tools = ["Read", "Write", "Edit", "Bash", "WebFetch", "Glob", "Grep", "MultiEdit", "Skill"]
        
        log("=" * 60)
        log("[Config] Starting Claude Code session")
        log(f"[Config] Working directory: {working_directory}")
        log(f"[Config] Model: {model or 'default'}")
        log(f"[Config] Allowed tools: {allowed_tools}")
        log(f"[Config] Max turns: {max_turns}")
        
        # Check for skills in working directory
        skills_dir = os.path.join(working_directory, ".claude", "skills")
        if os.path.exists(skills_dir):
            skills_found = os.listdir(skills_dir)
            log(f"[Config] Skills directory exists: {skills_dir}")
            log(f"[Config] Skills found: {skills_found}")
            for skill_name in skills_found:
                skill_path = os.path.join(skills_dir, skill_name)
                if os.path.isdir(skill_path):
                    skill_files = os.listdir(skill_path)
                    log(f"[Config] Skill '{skill_name}' files: {skill_files}")
        else:
            log(f"[Config] No skills directory at: {skills_dir}")
        log("=" * 60)
        log("[System Prompt Preview]")
        log(system_prompt[:500] + "..." if len(system_prompt) > 500 else system_prompt)
        log("=" * 60)
        log("[User Prompt Preview]")
        log(full_prompt[:500] + "..." if len(full_prompt) > 500 else full_prompt)
        log("=" * 60)
        
        # Configure Claude Agent options
        # setting_sources is required to load Skills from filesystem!
        options = ClaudeAgentOptions(
            system_prompt=system_prompt,
            max_turns=max_turns,
            allowed_tools=allowed_tools,
            cwd=working_directory,
            permission_mode="default",
            setting_sources=["user", "project"],  # Required to load Skills from ~/.claude/skills/ and .claude/skills/
            model=model  # Pass model (e.g. claude-sonnet-4-20250514, claude-opus-4-5-20251101)
        )
        
        # Collect response parts
        full_response_parts = []
        message_count = 0
        total_cost_usd = 0.0
        total_duration_ms = 0
        total_input_tokens = 0
        total_output_tokens = 0
        
        # Use ClaudeSDKClient for proper handling
        async with ClaudeSDKClient(options) as client:
            # Send the query
            log("[Query] Sending query to Claude...")
            await client.query(full_prompt)
            
            # Receive and process response
            log("[Response] Receiving response...")
            async for message in client.receive_response():
                message_count += 1
                message_type = type(message).__name__
                log(f"[Message #{message_count}] Type: {message_type}")
                
                if isinstance(message, AssistantMessage):
                    # Log all content blocks
                    for i, block in enumerate(message.content):
                        block_type = type(block).__name__
                        log(f"  [Block {i}] Type: {block_type}")
                        
                        if isinstance(block, TextBlock):
                            text_preview = block.text[:200] + "..." if len(block.text) > 200 else block.text
                            log(f"  [Block {i}] Text: {text_preview}")
                            full_response_parts.append(block.text)
                        elif isinstance(block, ToolUseBlock):
                            log(f"  [Block {i}] ToolUse: {block.name}")
                            log(f"  [Block {i}] ToolInput: {json.dumps(block.input, default=str)[:300]}")
                        elif isinstance(block, ToolResultBlock):
                            content_str = str(block.content) if hasattr(block, 'content') else "N/A"
                            log(f"  [Block {i}] ToolResult: {content_str[:200]}...")
                        else:
                            log(f"  [Block {i}] Other: {str(block)[:100]}")
                            
                elif isinstance(message, ResultMessage):
                    log(f"[Result] Task completed!")
                    if hasattr(message, 'duration_ms') and message.duration_ms:
                        total_duration_ms += message.duration_ms
                        log(f"[Result] Duration: {message.duration_ms}ms (Total: {total_duration_ms}ms)")
                    if hasattr(message, 'total_cost_usd') and message.total_cost_usd:
                        total_cost_usd += message.total_cost_usd
                        log(f"[Result] Cost: ${message.total_cost_usd:.4f} (Total: ${total_cost_usd:.4f})")
                    # Extract token usage from usage dict
                    if hasattr(message, 'usage') and message.usage:
                        input_tokens = message.usage.get('input_tokens', 0)
                        output_tokens = message.usage.get('output_tokens', 0)
                        total_input_tokens += input_tokens
                        total_output_tokens += output_tokens
                        log(f"[Result] Tokens: {input_tokens} in, {output_tokens} out (Total: {total_input_tokens} in, {total_output_tokens} out)")
                    if hasattr(message, 'session_id'):
                        log(f"[Result] Session ID: {message.session_id}")
                        
                elif isinstance(message, UserMessage):
                    log(f"[UserMessage] {str(message)[:100]}")
                    
                elif isinstance(message, SystemMessage):
                    log(f"[SystemMessage] {str(message)[:100]}")
                    
                else:
                    # Log unknown message types
                    log(f"[Unknown] {message_type}: {str(message)[:200]}")
        
        full_response = "\n".join(full_response_parts)
        
        log("=" * 60)
        log(f"[Summary] Total messages received: {message_count}")
        log(f"[Summary] Response length: {len(full_response)} chars")
        log(f"[Summary] Total cost: ${total_cost_usd:.4f}")
        log(f"[Summary] Total duration: {total_duration_ms}ms ({total_duration_ms/1000:.1f}s)")
        log(f"[Summary] Total tokens: {total_input_tokens} input, {total_output_tokens} output")
        log("=" * 60)
        
        # Build response
        response = {
            "success": True,
            "response_text": full_response,
            "full_response": full_response,
            "files_created": [],
            "tools_used": [t["tool"] for t in tool_usage_log],
            "total_cost_usd": total_cost_usd,
            "total_duration_ms": total_duration_ms,
            "total_input_tokens": total_input_tokens,
            "total_output_tokens": total_output_tokens,
            "error": None
        }
        
        # Check for created files in output directory
        output_dir = os.path.join(working_directory, "output")
        log(f"[Files] Scanning output directory: {output_dir}")
        if os.path.exists(output_dir):
            for root, dirs, files in os.walk(output_dir):
                log(f"[Files] Checking directory: {root}")
                log(f"[Files] Found {len(files)} file(s), {len(dirs)} subdirectorie(s)")
                for file in files:
                    file_path = os.path.join(root, file)
                    file_size = os.path.getsize(file_path)
                    response["files_created"].append(file_path)
                    log(f"[Files] Found: {file_path} ({file_size} bytes)")
        else:
            log(f"[Files] Output directory does not exist: {output_dir}")
        

        

        
        log("=" * 60)
        log(f"[Final] Total files found: {len(response['files_created'])}")
        log(f"[Final] Tools used via permission callback: {response['tools_used']}")
        log("=" * 60)
        
        # Log full response for debugging
        log("[Full Response Preview]")
        log(full_response[:1000] + "..." if len(full_response) > 1000 else full_response)
        log("=" * 60)
        
        return response
        
    except Exception as e:
        import traceback
        log(f"[Error] Exception: {str(e)}")
        log(f"[Error] Traceback:\n{traceback.format_exc()}")
        return {
            "success": False,
            "response_text": None,
            "full_response": None,
            "files_created": [],
            "tools_used": [t["tool"] for t in tool_usage_log],
            "error": str(e)
        }


def run_email_agent(
    system_prompt: str,
    user_prompt: str,
    email_subject: str,
    email_text: str,
    email_html: Optional[str],
    email_from: str,
    working_directory: str,
    max_turns: int = 20,
    model: Optional[str] = None
) -> dict:
    """Synchronous wrapper for the async function."""
    return asyncio.run(run_email_agent_async(
        system_prompt=system_prompt,
        user_prompt=user_prompt,
        email_subject=email_subject,
        email_text=email_text,
        email_html=email_html,
        email_from=email_from,
        working_directory=working_directory,
        max_turns=max_turns,
        model=model
    ))


def main():
    """Main entry point when called as subprocess."""
    if len(sys.argv) < 2:
        print(json.dumps({
            "success": False,
            "error": "Usage: python claude_agent_runner.py <input_json_path>"
        }))
        sys.exit(1)
    
    input_path = sys.argv[1]
    
    if not os.path.exists(input_path):
        print(json.dumps({
            "success": False,
            "error": f"Input file not found: {input_path}"
        }))
        sys.exit(1)
    
    try:
        with open(input_path, 'r', encoding='utf-8') as f:
            input_data = json.load(f)
    except Exception as e:
        print(json.dumps({
            "success": False,
            "error": f"Failed to read input file: {str(e)}"
        }))
        sys.exit(1)
    
    log(f"[Start] Processing email: {input_data.get('email_subject', 'N/A')}")
    log(f"[Start] Input file: {input_path}")
    
    # Extract parameters from input
    result = run_email_agent(
        system_prompt=input_data.get("system_prompt", ""),
        user_prompt=input_data.get("user_prompt", ""),
        email_subject=input_data.get("email_subject", ""),
        email_text=input_data.get("email_text", ""),
        email_html=input_data.get("email_html"),
        email_from=input_data.get("email_from", ""),
        working_directory=input_data.get("working_directory", "."),
        max_turns=input_data.get("max_turns", 20),
        model=input_data.get("model")
    )
    
    log(f"[End] Success: {result['success']}")
    log(f"[End] Files created: {len(result.get('files_created', []))}")
    log(f"[End] Tools used: {result.get('tools_used', [])}")
    
    # Output result as JSON (to stdout - this is what C# reads)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
