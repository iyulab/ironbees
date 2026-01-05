using Ironbees.Autonomous.Abstractions;

namespace TwentyQuestionsSample;

// ============================================================================
// Domain Models - Minimal, SDK-compliant
// ============================================================================

/// <summary>
/// Game request implementing SDK's ITaskRequest
/// </summary>
public record GameRequest : ITaskRequest
{
    public required string RequestId { get; init; }
    public required string Prompt { get; init; }
    public int QuestionNumber { get; init; }
    public IReadOnlyList<QAPair> History { get; init; } = [];
    public string? CategoryHint { get; init; }

    public static GameRequest Create(int questionNumber, IReadOnlyList<QAPair> history, string? hint = null) => new()
    {
        RequestId = $"q{questionNumber:D2}",
        Prompt = $"Question {questionNumber} of 20",
        QuestionNumber = questionNumber,
        History = history,
        CategoryHint = hint
    };
}

/// <summary>
/// Game result implementing SDK's ITaskResult
/// </summary>
public record GameResult : ITaskResult
{
    public required string RequestId { get; init; }
    public bool Success { get; init; }
    public required string Output { get; init; }
    public string? ErrorOutput { get; init; }
    public required string Content { get; init; }
    public bool IsGuess { get; init; }
    public string? Reasoning { get; init; }
    public double Confidence { get; init; }
}

/// <summary>
/// Question-Answer pair
/// </summary>
public record QAPair
{
    public required int Number { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public string? Clarification { get; init; }
}

/// <summary>
/// Game state tracking
/// </summary>
public class GameState
{
    private readonly List<QAPair> _history = [];
    private int _guessAttempts;

    public string? Secret { get; set; }
    public int CurrentQuestion { get; private set; }
    public int GuessAttempts => _guessAttempts;
    public IReadOnlyList<QAPair> History => _history.AsReadOnly();
    public bool IsGameOver { get; private set; }
    public bool IsWin { get; private set; }
    public string? WinningGuess { get; private set; }

    public void AddQA(string question, string answer, string? clarification = null)
    {
        _history.Add(new QAPair
        {
            Number = _history.Count + 1,
            Question = question,
            Answer = answer,
            Clarification = clarification
        });
        CurrentQuestion = _history.Count;
    }

    public void RecordGuess(bool isCorrect, string guess)
    {
        _guessAttempts++;
        if (isCorrect)
        {
            IsWin = true;
            IsGameOver = true;
            WinningGuess = guess;
        }
    }

    public void SetGameOver(bool isWin)
    {
        IsGameOver = true;
        IsWin = isWin;
    }
}
