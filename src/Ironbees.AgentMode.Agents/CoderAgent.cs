using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ironbees.AgentMode.Models;
using Ironbees.AgentMode.MCP;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Agents;

/// <summary>
/// Agent responsible for generating code based on the execution plan.
/// Uses LLM to create code modifications and insertions.
/// </summary>
public class CoderAgent : ICodingAgent
{
    private readonly IChatClient? _chatClient;
    private readonly IToolRegistry? _toolRegistry;

    public string Name => "coder";
    public string Description => "Generates code based on execution plan using LLM";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn", "msbuild" };

    public CoderAgent(IChatClient? chatClient = null, IToolRegistry? toolRegistry = null)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
    }

    public async Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate state
            if (state.Plan is null)
                throw new AgentExecutionException(Name, "Execution plan is required but not found in state");

            if (state.Spec is null)
                throw new AgentExecutionException(Name, "Specification is required but not found in state");

            // Generate code for each step in the plan
            var allEdits = new List<FileEdit>();

            foreach (var step in state.Plan.Steps)
            {
                var edits = await GenerateCodeForStep(state, step, cancellationToken);
                allEdits.AddRange(edits);
            }

            // Create response with generated code changes
            return new AgentResponse
            {
                Updates = new Dictionary<string, object?>
                {
                    ["CodeDiffs"] = allEdits.ToImmutableList(),
                    ["CodeGenerationComplete"] = true
                },
                NextNode = "VALIDATE",
                Metadata = new Dictionary<string, string>
                {
                    ["agent"] = Name,
                    ["editsCount"] = allEdits.Count.ToString(),
                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                }
            };
        }
        catch (Exception ex) when (ex is not AgentExecutionException)
        {
            throw new AgentExecutionException(Name, $"Failed to generate code: {ex.Message}", ex);
        }
    }

    private async Task<List<FileEdit>> GenerateCodeForStep(
        CodingState state,
        PlanStep step,
        CancellationToken cancellationToken)
    {
        // Build context-aware prompt
        var prompt = BuildCodeGenerationPrompt(state, step);

        // TODO: Full LLM integration - For now using MVP implementation
        // Will be completed once we verify the correct IChatClient API surface
        // Expected: var completion = await _chatClient.CompleteAsync(messages);

        // MVP: Generate placeholder code based on step action type
        var response = GeneratePlaceholderCode(state, step);

        // Parse response to extract file edits
        var edits = ParseFileEdits(response, step);

        return await Task.FromResult(edits);
    }

    private string GeneratePlaceholderCode(CodingState state, PlanStep step)
    {
        // MVP implementation: Generate simple placeholder based on action type
        var actionType = step.ActionType?.ToUpperInvariant() ?? "CREATE";

        return $@"```json
{{
  ""changes"": [
    {{
      ""filePath"": ""Generated/{step.Description.Replace(" ", "")}.cs"",
      ""changeType"": ""{actionType}"",
      ""content"": ""// TODO: Implement {step.Description}\n// Step {step.Number}: {step.Rationale}\n\nnamespace Generated\n{{\n    public class Placeholder\n    {{\n        // Implementation pending\n    }}\n}}""
    }}
  ]
}}
```";
    }

    private string BuildCodeGenerationPrompt(CodingState state, PlanStep step)
    {
        // Get repository path from metadata if available
        var repositoryPath = state.Metadata.TryGetValue("RepositoryPath", out var path)
            ? path
            : "unknown";

        var prompt = $@"You are an expert C# developer tasked with implementing code changes.

## Context
- Repository: {repositoryPath}
- User Request: {state.UserRequest}
- Specification: {state.Spec}

## Current Step
- Step {step.Number}: {step.Description}
- Action Type: {step.ActionType}
- Rationale: {step.Rationale}

## Instructions
Generate the code changes needed to complete this step. For each change, provide:

1. **File Path**: Relative path from repository root
2. **Change Type**: CREATE, UPDATE, or DELETE
3. **Code Content**: The actual code to write/modify
4. **Description**: Brief explanation of what this code does

## Output Format
Provide your response in the following JSON format:

```json
{{
  ""changes"": [
    {{
      ""filePath"": ""relative/path/to/file.cs"",
      ""changeType"": ""CREATE"",
      ""content"": ""// Complete file content here"",
      ""description"": ""Creates the UserService class with authentication""
    }}
  ]
}}
```

## Guidelines
- Follow C# coding conventions and best practices
- Use proper naming conventions (PascalCase for classes, camelCase for parameters)
- Include necessary using statements
- Add XML documentation comments for public members
- Ensure code is production-ready and follows SOLID principles
- Consider error handling and edge cases

Generate the code changes now:";

        return prompt;
    }

    private List<FileEdit> ParseFileEdits(string llmResponse, PlanStep step)
    {
        var edits = new List<FileEdit>();

        try
        {
            // Try to extract JSON from markdown code blocks
            var jsonMatch = Regex.Match(llmResponse, @"```json\s*\n(.*?)\n```", RegexOptions.Singleline);
            var jsonContent = jsonMatch.Success ? jsonMatch.Groups[1].Value : llmResponse;

            // Parse JSON response
            var responseDoc = JsonDocument.Parse(jsonContent);

            if (responseDoc.RootElement.TryGetProperty("changes", out var changesArray))
            {
                foreach (var changeElement in changesArray.EnumerateArray())
                {
                    var filePath = changeElement.GetProperty("filePath").GetString() ?? "";
                    var changeTypeStr = changeElement.GetProperty("changeType").GetString() ?? "MODIFY";
                    var content = changeElement.GetProperty("content").GetString() ?? "";

                    var editType = changeTypeStr.ToUpperInvariant() switch
                    {
                        "CREATE" => EditType.Create,
                        "DELETE" => EditType.Delete,
                        _ => EditType.Modify
                    };

                    edits.Add(new FileEdit
                    {
                        FilePath = filePath,
                        Type = editType,
                        NewContent = content,
                        OriginalContent = editType == EditType.Modify ? null : null
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            // Fallback: Try to parse as plain text with simple heuristics
            edits.AddRange(ParsePlainTextResponse(llmResponse, step));

            // Log warning
            Console.WriteLine($"Warning: Failed to parse JSON response, used fallback parser. Error: {ex.Message}");
        }

        return edits;
    }

    private List<FileEdit> ParsePlainTextResponse(string response, PlanStep step)
    {
        var edits = new List<FileEdit>();

        // Simple heuristic: Look for file paths and code blocks
        var fileMatches = Regex.Matches(response, @"(?:File|Path):\s*`?([^`\n]+)`?", RegexOptions.IgnoreCase);
        var codeBlocks = Regex.Matches(response, @"```(?:csharp|cs)?\s*\n(.*?)\n```", RegexOptions.Singleline);

        if (fileMatches.Count > 0 && codeBlocks.Count > 0)
        {
            for (int i = 0; i < Math.Min(fileMatches.Count, codeBlocks.Count); i++)
            {
                var filePath = fileMatches[i].Groups[1].Value.Trim();
                var content = codeBlocks[i].Groups[1].Value;

                // Infer edit type from step action
                var editType = step.ActionType?.ToUpperInvariant() switch
                {
                    "CREATE" => EditType.Create,
                    "DELETE" => EditType.Delete,
                    _ => EditType.Modify
                };

                edits.Add(new FileEdit
                {
                    FilePath = filePath,
                    Type = editType,
                    NewContent = content
                });
            }
        }
        else
        {
            // Last resort: Create a single edit with the entire response
            edits.Add(new FileEdit
            {
                FilePath = "generated_code.cs",
                Type = EditType.Create,
                NewContent = response
            });
        }

        return edits;
    }
}
