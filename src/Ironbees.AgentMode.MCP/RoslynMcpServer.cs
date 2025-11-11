using System.Collections.Immutable;
using Ironbees.AgentMode.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ironbees.AgentMode.MCP;

/// <summary>
/// MCP server providing Roslyn-based C# code analysis and compilation tools.
/// </summary>
public class RoslynMcpServer : IMcpServer
{
    private readonly List<ToolDefinition> _tools = new();
    private bool _initialized;

    public string Name => "roslyn";
    public string Version => "0.1.0";
    public IReadOnlyList<ToolDefinition> Tools => _tools;

    public Task InitializeAsync(
        IReadOnlyDictionary<string, object> configuration,
        CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return Task.CompletedTask;

        // Register available tools
        _tools.Add(new ToolDefinition
        {
            Name = "compile_code",
            Description = "Compiles C# code and returns compilation diagnostics (errors and warnings)",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["code"] = new JsonSchema
                    {
                        Type = "string",
                        Description = "C# source code to compile"
                    },
                    ["assemblyName"] = new JsonSchema
                    {
                        Type = "string",
                        Description = "Optional assembly name (defaults to 'DynamicAssembly')"
                    }
                },
                Required = new[] { "code" }
            }
        });

        _tools.Add(new ToolDefinition
        {
            Name = "analyze_syntax",
            Description = "Analyzes C# code syntax without full compilation",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["code"] = new JsonSchema
                    {
                        Type = "string",
                        Description = "C# source code to analyze"
                    }
                },
                Required = new[] { "code" }
            }
        });

        _initialized = true;
        return Task.CompletedTask;
    }

    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Server not initialized. Call InitializeAsync first.");

        return toolName switch
        {
            "compile_code" => await CompileCodeAsync(arguments, cancellationToken),
            "analyze_syntax" => await AnalyzeSyntaxAsync(arguments, cancellationToken),
            _ => throw new ToolNotFoundException(Name, toolName)
        };
    }

    private async Task<ToolResult> CompileCodeAsync(
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!arguments.TryGetValue("code", out var codeObj) || codeObj is not string code)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Missing required argument 'code' (string)"
                };
            }

            var assemblyName = arguments.TryGetValue("assemblyName", out var nameObj) && nameObj is string name
                ? name
                : "DynamicAssembly";

            // Parse the code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);

            // Create compilation with basic references
            var references = GetBasicReferences();
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Get diagnostics
            var diagnostics = compilation.GetDiagnostics(cancellationToken);

            // Separate errors and warnings
            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new CompilationError
                {
                    Code = d.Id,
                    Message = d.GetMessage(),
                    FilePath = d.Location.SourceTree?.FilePath,
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToImmutableList();

            var warnings = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => new CompilationWarning
                {
                    Code = d.Id,
                    Message = d.GetMessage(),
                    FilePath = d.Location.SourceTree?.FilePath,
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToImmutableList();

            var buildResult = new BuildResult
            {
                Success = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                Duration = TimeSpan.Zero // TODO: Track actual compilation time
            };

            return await Task.FromResult(new ToolResult
            {
                Success = true,
                Content = buildResult,
                Metadata = new Dictionary<string, string>
                {
                    ["errorsCount"] = errors.Count.ToString(),
                    ["warningsCount"] = warnings.Count.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = $"Compilation failed: {ex.Message}"
            };
        }
    }

    private async Task<ToolResult> AnalyzeSyntaxAsync(
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!arguments.TryGetValue("code", out var codeObj) || codeObj is not string code)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Missing required argument 'code' (string)"
                };
            }

            // Parse the code and get syntax diagnostics only
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var diagnostics = syntaxTree.GetDiagnostics(cancellationToken);

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new
                {
                    Code = d.Id,
                    Message = d.GetMessage(),
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToList();

            return await Task.FromResult(new ToolResult
            {
                Success = errors.Count == 0,
                Content = new
                {
                    SyntaxValid = errors.Count == 0,
                    Errors = errors,
                    LineCount = code.Split('\n').Length
                },
                Metadata = new Dictionary<string, string>
                {
                    ["errorsCount"] = errors.Count.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = $"Syntax analysis failed: {ex.Message}"
            };
        }
    }

    private static IEnumerable<MetadataReference> GetBasicReferences()
    {
        // Basic .NET references for compilation
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // System.Private.CoreLib
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Console.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll"))
        };
    }

    public async ValueTask DisposeAsync()
    {
        // No resources to dispose for now
        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// Exception thrown when a tool is not found in an MCP server.
/// </summary>
public class ToolNotFoundException : Exception
{
    public string ServerName { get; }
    public string ToolName { get; }

    public ToolNotFoundException(string serverName, string toolName)
        : base($"Tool '{toolName}' not found in server '{serverName}'")
    {
        ServerName = serverName;
        ToolName = toolName;
    }
}

/// <summary>
/// Exception thrown when tool execution fails.
/// </summary>
public class ToolExecutionException : Exception
{
    public string ToolName { get; }

    public ToolExecutionException(string toolName, string message, Exception? innerException = null)
        : base($"Tool '{toolName}' execution failed: {message}", innerException)
    {
        ToolName = toolName;
    }
}
