namespace Ironbees.AgentMode.Exceptions;

/// <summary>
/// Exception thrown when workflow YAML parsing fails.
/// </summary>
public class WorkflowParseException : AgentModeException
{
    /// <summary>
    /// Path to the file being parsed (if applicable).
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Line number where error occurred (if available).
    /// </summary>
    public int? LineNumber { get; }

    /// <summary>
    /// Column number where error occurred (if available).
    /// </summary>
    public int? Column { get; }

    public WorkflowParseException(string message)
        : base(message)
    {
    }

    public WorkflowParseException(string message, string? filePath)
        : base(message)
    {
        FilePath = filePath;
    }

    public WorkflowParseException(string message, string? filePath, int? lineNumber, int? column)
        : base(message)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
        Column = column;
    }

    public WorkflowParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public WorkflowParseException(string message, string? filePath, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
    }

    public override string ToString()
    {
        var location = FilePath != null
            ? $" in '{FilePath}'"
            : "";

        if (LineNumber.HasValue)
        {
            location += $" at line {LineNumber}";
            if (Column.HasValue)
            {
                location += $", column {Column}";
            }
        }

        return $"WorkflowParseException{location}: {Message}";
    }
}
