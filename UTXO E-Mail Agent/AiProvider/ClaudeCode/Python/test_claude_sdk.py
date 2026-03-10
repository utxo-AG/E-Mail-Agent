#!/usr/bin/env python3
"""
Simple test script for Claude Agent SDK
Usage: python test_claude_sdk.py "your prompt here"
       python test_claude_sdk.py --interactive
"""

import asyncio
import json
import os
import sys
import shutil

try:
    from claude_agent_sdk import (
        ClaudeAgentOptions,
        ClaudeSDKClient,
        PermissionResultAllow,
        ToolPermissionContext,
        AssistantMessage,
        ResultMessage,
        TextBlock,
        ToolUseBlock,
    )
except ImportError as e:
    print(f"Error: claude-agent-sdk not installed. Run: pip install claude-agent-sdk")
    sys.exit(1)


async def allow_all_tools(
    tool_name: str,
    input_data: dict,
    context: ToolPermissionContext
) -> PermissionResultAllow:
    """Allow all tools automatically."""
    print(f"  [Tool] {tool_name}")
    return PermissionResultAllow()


async def run_prompt(prompt: str, working_dir: str = None, with_skill: bool = False):
    """Run a prompt through Claude Agent SDK."""
    
    # Use temp directory if not specified
    if working_dir is None:
        working_dir = os.path.join(os.path.expanduser("~"), ".claude_sdk_test")
    
    os.makedirs(working_dir, exist_ok=True)
    print(f"Working directory: {working_dir}")
    
    # Optionally install a test skill
    if with_skill:
        skills_dir = os.path.join(working_dir, ".claude", "skills", "test-skill")
        os.makedirs(skills_dir, exist_ok=True)
        skill_content = """---
name: test-skill
description: A simple test skill that greets the user
---

# Test Skill

When invoked, respond with "Hello from test-skill! 🎉"
"""
        with open(os.path.join(skills_dir, "SKILL.md"), "w") as f:
            f.write(skill_content)
        print(f"Installed test skill at: {skills_dir}")
    
    # Check for skills
    skills_path = os.path.join(working_dir, ".claude", "skills")
    if os.path.exists(skills_path):
        skills = os.listdir(skills_path)
        print(f"Skills in working directory: {skills}")
    else:
        print("No .claude/skills/ directory in working directory")
    
    # Also check global skills
    global_skills = os.path.expanduser("~/.claude/skills")
    if os.path.exists(global_skills):
        print(f"Global skills (~/.claude/skills): {os.listdir(global_skills)}")
    
    print("-" * 50)
    print(f"Prompt: {prompt}")
    print("-" * 50)
    
    options = ClaudeAgentOptions(
        system_prompt="Du bist ein hilfreicher Assistent. Antworte auf Deutsch.",
        max_turns=10,
        allowed_tools=["Read", "Write", "Edit", "Bash", "WebFetch", "WebSearch", "Glob", "Grep", "Skill"],
        cwd=working_dir,
        can_use_tool=allow_all_tools,
        permission_mode="default",
        setting_sources=["user", "project"]  # Required to load Skills from filesystem!
    )
    
    response_parts = []
    
    async with ClaudeSDKClient(options) as client:
        await client.query(prompt)
        
        async for message in client.receive_response():
            if isinstance(message, AssistantMessage):
                for block in message.content:
                    if isinstance(block, TextBlock):
                        response_parts.append(block.text)
                    elif isinstance(block, ToolUseBlock):
                        print(f"  [ToolUse] {block.name}: {json.dumps(block.input, default=str)[:100]}")
            elif isinstance(message, ResultMessage):
                print(f"\n[Done] Duration: {message.duration_ms}ms")
    
    print("-" * 50)
    print("Response:")
    print("-" * 50)
    print("\n".join(response_parts))
    
    return "\n".join(response_parts)


def main():
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python test_claude_sdk.py \"your prompt\"")
        print("  python test_claude_sdk.py --interactive")
        print("  python test_claude_sdk.py --with-skill \"your prompt\"")
        print("")
        print("Examples:")
        print("  python test_claude_sdk.py \"Welche Skills hast du installiert?\"")
        print("  python test_claude_sdk.py --with-skill \"Rufe den test-skill auf\"")
        sys.exit(1)
    
    with_skill = "--with-skill" in sys.argv
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    
    if "--interactive" in sys.argv:
        print("Interactive mode. Type 'exit' to quit.")
        while True:
            try:
                prompt = input("\nPrompt> ").strip()
                if prompt.lower() == "exit":
                    break
                if prompt:
                    asyncio.run(run_prompt(prompt, with_skill=with_skill))
            except KeyboardInterrupt:
                break
    else:
        prompt = " ".join(args)
        asyncio.run(run_prompt(prompt, with_skill=with_skill))


if __name__ == "__main__":
    main()
