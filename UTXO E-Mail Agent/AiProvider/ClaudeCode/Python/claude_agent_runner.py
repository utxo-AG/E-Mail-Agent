"""
Claude Agent SDK Runner
This script interfaces with the claude-code-sdk to run Claude Code agents.
Called from C# via Python.NET
"""

import json
import os
import sys
from typing import Optional

try:
    from claude_code_sdk import ClaudeCode, ClaudeCodeOptions
except ImportError:
    print("Error: claude-code-sdk not installed. Run: pip install claude-code-sdk", file=sys.stderr)
    sys.exit(1)


def run_email_agent(
    system_prompt: str,
    user_prompt: str,
    email_subject: str,
    email_text: str,
    email_html: Optional[str],
    email_from: str,
    working_directory: str,
    max_turns: int = 20,
    api_key: Optional[str] = None
) -> str:
    """
    Run Claude Code agent to process an email and generate a response.
    
    Args:
        system_prompt: The system instructions for the agent
        user_prompt: The user message/prompt
        email_subject: Subject of the email
        email_text: Plain text content of the email
        email_html: HTML content of the email (optional)
        email_from: Sender email address
        working_directory: Directory for agent to work in
        max_turns: Maximum number of agent turns
        api_key: Anthropic API key (optional, uses env var if not provided)
    
    Returns:
        JSON string with the agent's response
    """
    
    # Set API key if provided
    if api_key:
        os.environ["ANTHROPIC_API_KEY"] = api_key
    
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
        # Configure Claude Code options
        options = ClaudeCodeOptions(
            system_prompt=system_prompt,
            max_turns=max_turns,
            allowed_tools=["Read", "Write", "Edit", "Bash", "WebFetch"],
            working_directory=working_directory
        )
        
        # Create Claude Code instance
        claude = ClaudeCode(options=options)
        
        # Run the agent
        result = claude.run(full_prompt)
        
        # Extract the response
        response = {
            "success": True,
            "response_text": result.text if hasattr(result, 'text') else str(result),
            "full_response": str(result),
            "files_created": [],
            "error": None
        }
        
        # Check for created files in working directory
        if os.path.exists(working_directory):
            for root, dirs, files in os.walk(working_directory):
                for file in files:
                    file_path = os.path.join(root, file)
                    response["files_created"].append(file_path)
        
        return json.dumps(response, ensure_ascii=False)
        
    except Exception as e:
        error_response = {
            "success": False,
            "response_text": None,
            "full_response": None,
            "files_created": [],
            "error": str(e)
        }
        return json.dumps(error_response, ensure_ascii=False)


def run_simple_query(
    prompt: str,
    system_prompt: Optional[str] = None,
    working_directory: Optional[str] = None,
    max_turns: int = 10,
    api_key: Optional[str] = None
) -> str:
    """
    Run a simple query through Claude Code.
    
    Args:
        prompt: The prompt to send to Claude
        system_prompt: Optional system instructions
        working_directory: Optional working directory
        max_turns: Maximum number of turns
        api_key: Optional API key
    
    Returns:
        JSON string with the response
    """
    
    if api_key:
        os.environ["ANTHROPIC_API_KEY"] = api_key
    
    try:
        options_dict = {
            "max_turns": max_turns,
        }
        
        if system_prompt:
            options_dict["system_prompt"] = system_prompt
        
        if working_directory:
            options_dict["working_directory"] = working_directory
        
        options = ClaudeCodeOptions(**options_dict)
        claude = ClaudeCode(options=options)
        result = claude.run(prompt)
        
        return json.dumps({
            "success": True,
            "response": str(result),
            "error": None
        }, ensure_ascii=False)
        
    except Exception as e:
        return json.dumps({
            "success": False,
            "response": None,
            "error": str(e)
        }, ensure_ascii=False)


# For testing from command line
if __name__ == "__main__":
    if len(sys.argv) > 1:
        test_prompt = " ".join(sys.argv[1:])
        result = run_simple_query(test_prompt)
        print(result)
    else:
        print("Usage: python claude_agent_runner.py <prompt>")
