# 🐝 Ironbees Console Chat Sample

Interactive console chat application demonstrating multi-agent orchestration with Ironbees.

## Features

- **Interactive Chat**: Real-time conversation with AI agents
- **Agent Selection**: Manually select specific agents or use auto-selection
- **Streaming Responses**: Real-time streaming of agent responses
- **Multiple Commands**: Built-in commands for agent management
- **Color-Coded Output**: User input (cyan) and agent responses (green)
- **Error Handling**: Graceful error handling with helpful messages

## Prerequisites

- .NET 9.0 SDK
- Azure OpenAI account with API key
- Ironbees agents configured in `agents/` directory

## Setup

### 1. Environment Variables

Set the required environment variables:

**Linux/Mac:**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_KEY="your-api-key-here"
```

**Windows PowerShell:**
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
$env:AZURE_OPENAI_KEY="your-api-key-here"
```

**Windows CMD:**
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
set AZURE_OPENAI_KEY=your-api-key-here
```

### 2. Run the Application

From the `samples/ConsoleChatSample` directory:

```bash
dotnet run
```

Or from the solution root:

```bash
dotnet run --project samples/ConsoleChatSample
```

## Usage

### Starting the Chat

When you start the application, it will:
1. Load all available agents from the `agents/` directory
2. Display the list of loaded agents
3. Start an interactive chat session

### Chat Commands

| Command | Description |
|---------|-------------|
| `/exit`, `/quit` | Exit the application |
| `/agents` | List all available agents with their capabilities |
| `/agent <name>` | Switch to a specific agent |
| `/auto` | Enable automatic agent selection (default) |
| `/clear` | Clear the console |
| `/help` | Show help message with all commands |

### Example Session

```
╔═══════════════════════════════════════════════════════════╗
║           🐝 Ironbees Console Chat Sample               ║
║       Multi-Agent Orchestration for .NET 9.0             ║
╚═══════════════════════════════════════════════════════════╝

📂 Loading agents...
✅ Loaded 4 agent(s):
   • coding-agent - Expert software developer
   • writing-agent - Professional content writer
   • analysis-agent - Data analyst
   • review-agent - Quality reviewer

╔═══════════════════════════════════════════════════════════╗
║                 Chat Session Started                      ║
╚═══════════════════════════════════════════════════════════╝

Commands:
  /exit, /quit    - Exit the application
  /agents         - List all available agents
  /clear          - Clear the console
  /agent <name>   - Switch to specific agent
  /auto           - Enable auto agent selection (default)
  /help           - Show this help message

You (auto): Write a Python function to calculate fibonacci
Agent: Here's a Python function to calculate the Fibonacci sequence:

```python
def fibonacci(n):
    """Calculate the nth Fibonacci number."""
    if n <= 0:
        return 0
    elif n == 1:
        return 1
    else:
        return fibonacci(n - 1) + fibonacci(n - 2)
```

You (auto): /agent writing-agent
✅ Switched to agent: writing-agent

You (@writing-agent): Write a blog post introduction about AI
Agent: Artificial Intelligence is transforming the way we live and work...

You (@writing-agent): /exit
👋 Goodbye!
```

## Configuration

### Agent Selection

The sample supports two modes:

1. **Auto-selection (default)**: The orchestrator automatically selects the best agent based on your input
2. **Manual selection**: Use `/agent <name>` to switch to a specific agent

### Confidence Threshold

The application uses a confidence threshold of 0.6 for agent selection. If no agent meets this threshold, it falls back to `general-assistant`.

You can adjust this in `Program.cs`:

```csharp
options.ConfidenceThreshold = 0.7; // Higher = more selective
options.FallbackAgentName = "your-fallback-agent";
```

### Agents Directory

The application searches for agents in these locations (in order):
1. `./agents` (current directory)
2. `../../../../agents` (solution root, when running from bin/Debug)
3. `../../../agents` (project root)

If none exist, it defaults to `./agents` in the current directory.

## Troubleshooting

### "AZURE_OPENAI_ENDPOINT environment variable not set"

**Solution**: Set the environment variable as shown in the Setup section.

### "No agents loaded"

**Solution**:
1. Ensure the `agents/` directory exists
2. Verify it contains at least one agent with `agent.yaml` and `system-prompt.md`
3. Check agent configurations are valid

### "Agent 'xxx' not found"

**Solution**: Use `/agents` command to list all available agents and their names.

### Connection errors

**Solution**:
1. Verify your Azure OpenAI endpoint is correct
2. Check your API key is valid
3. Ensure your Azure OpenAI resource has the required model deployments

## Related Documentation

- [Quick Start Guide](../../docs/QUICK_START.md) - 5-minute tutorial
- [Getting Started](../../docs/GETTING_STARTED.md) - Comprehensive guide
- [Production Deployment](../../docs/PRODUCTION_DEPLOYMENT.md) - Production best practices
- [Custom Adapter](../../docs/CUSTOM_ADAPTER.md) - Building custom adapters

## License

MIT License - See [LICENSE](../../LICENSE) file for details.
