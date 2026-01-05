namespace Ironbees.Autonomous.Executors;

/// <summary>
/// Game/application configuration loaded from YAML.
/// Defines rules, modes, and agent assignments.
/// </summary>
public record GameDefinition
{
    /// <summary>Game identifier</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Game description</summary>
    public string? Description { get; init; }

    /// <summary>Available game modes</summary>
    public Dictionary<string, GameModeDefinition> Modes { get; init; } = new();

    /// <summary>Game rules</summary>
    public GameRules Rules { get; init; } = new();

    /// <summary>Messages and prompts</summary>
    public GameMessages Messages { get; init; } = new();

    /// <summary>Human input validation settings</summary>
    public ValidationSettings Validation { get; init; } = new();

    /// <summary>Human player prompts</summary>
    public PlayerPrompts Prompts { get; init; } = new();
}

/// <summary>
/// Definition of a game mode
/// </summary>
public record GameModeDefinition
{
    /// <summary>Mode name</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Mode description</summary>
    public string? Description { get; init; }

    /// <summary>Agent assignments for this mode</summary>
    public AgentAssignments Agents { get; init; } = new();
}

/// <summary>
/// Agent role assignments
/// </summary>
public record AgentAssignments
{
    /// <summary>Agent that asks questions (AI or Human)</summary>
    public string Questioner { get; init; } = "ai";

    /// <summary>Agent that answers questions (AI or Human)</summary>
    public string Answerer { get; init; } = "human";

    /// <summary>Agent that generates secrets (optional)</summary>
    public string? SecretGenerator { get; init; }
}

/// <summary>
/// Game rules configuration
/// </summary>
public record GameRules
{
    /// <summary>Maximum number of questions/iterations</summary>
    public int MaxQuestions { get; init; } = 20;

    /// <summary>Maximum guess attempts</summary>
    public int MaxGuessAttempts { get; init; } = 3;

    /// <summary>Confidence threshold for making a guess</summary>
    public double GuessConfidenceThreshold { get; init; } = 0.8;

    /// <summary>Valid answer types</summary>
    public List<string> ValidAnswers { get; init; } = new() { "yes", "no", "maybe" };
}

/// <summary>
/// Game messages and prompts
/// </summary>
public record GameMessages
{
    /// <summary>Message shown at game start</summary>
    public string? Welcome { get; init; }

    /// <summary>Message shown when AI wins</summary>
    public string? AiWins { get; init; }

    /// <summary>Message shown when human wins</summary>
    public string? HumanWins { get; init; }

    /// <summary>Message shown when game times out</summary>
    public string? Timeout { get; init; }

    /// <summary>Prompt for entering secret</summary>
    public string? EnterSecret { get; init; }
}

/// <summary>
/// Human input validation settings
/// </summary>
public record ValidationSettings
{
    /// <summary>Invalid question patterns (open-ended questions)</summary>
    public InvalidPatterns InvalidPatterns { get; init; } = new();

    /// <summary>Choice question patterns to reject</summary>
    public List<string> ChoicePatterns { get; init; } = [" or ", "인가요"];

    /// <summary>Validation error messages</summary>
    public ValidationMessages Messages { get; init; } = new();
}

/// <summary>
/// Invalid question patterns by language
/// </summary>
public record InvalidPatterns
{
    /// <summary>English open-ended patterns</summary>
    public List<string> English { get; init; } = ["what ", "where ", "when ", "who ", "which ", "how ", "why "];

    /// <summary>Korean open-ended patterns</summary>
    public List<string> Korean { get; init; } = ["뭐", "무엇", "어디", "언제", "누구", "어떻게", "왜", "몇", "어느"];

    /// <summary>Get all patterns flattened</summary>
    public IEnumerable<string> All => English.Concat(Korean);
}

/// <summary>
/// Validation error messages
/// </summary>
public record ValidationMessages
{
    /// <summary>Message for open-ended questions</summary>
    public string OpenEnded { get; init; } = "This looks like an open-ended question. Please ask a Yes/No question.";

    /// <summary>Message for choice questions</summary>
    public string Choice { get; init; } = "Please ask one Yes/No question at a time, not a choice question.";

    /// <summary>Message for empty input</summary>
    public string Empty { get; init; } = "Please enter a question or guess.";

    /// <summary>Example valid questions</summary>
    public string Examples { get; init; } = "Good examples: \"Is it alive?\", \"Can you eat it?\", \"살아있나요?\", \"먹을 수 있나요?\"";
}

/// <summary>
/// Human player prompts and UI text
/// </summary>
public record PlayerPrompts
{
    /// <summary>Questions remaining display</summary>
    public string QuestionsRemaining { get; init; } = "📊 Questions remaining: {remaining}";

    /// <summary>History section label</summary>
    public string HistoryLabel { get; init; } = "📜 History:";

    /// <summary>Input hint for human questioner</summary>
    public string InputHint { get; init; } = "💡 Type your question, or 'guess: [answer]' to make a guess";

    /// <summary>Yes/No hint</summary>
    public string YesNoHint { get; init; } = "(Questions must be answerable with Yes/No)";

    /// <summary>Move prompt</summary>
    public string YourMove { get; init; } = "❓ Your move: ";

    /// <summary>AI question display</summary>
    public string AiAsks { get; init; } = "❓ AI asks: {question}";

    /// <summary>Secret reminder</summary>
    public string YourSecret { get; init; } = "(Your secret: {secret})";

    /// <summary>Answer options prompt</summary>
    public string AnswerPrompt { get; init; } = "[Y]es / [N]o / [M]aybe: ";

    /// <summary>Clarification prompt</summary>
    public string Clarification { get; init; } = "Clarification (optional): ";

    /// <summary>AI guess display</summary>
    public string AiGuesses { get; init; } = "🎯 AI guesses: {guess}";

    /// <summary>Correct guess prompt</summary>
    public string CorrectPrompt { get; init; } = "Is this correct? [Y/N]: ";
}
