using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Executors;
using Ironbees.Autonomous.Models;
using Ironbees.Autonomous.Strategies;
using Ironbees.Autonomous.Utilities;
using OpenAI.Chat;

namespace TwentyQuestionsSample;

// ============================================================================
// AI Executors - Configuration-driven, uses SDK utilities
// ============================================================================

/// <summary>
/// AI-powered question generator using SDK utilities
/// </summary>
public class AiQuestionExecutor : ITaskExecutor<GameRequest, GameResult>
{
    private readonly ChatClient _chatClient;
    private readonly AgentDefinition _agent;
    private readonly GameDefinition _gameConfig;
    private readonly OrchestratorSettings _settings;
    private readonly string _systemPrompt;
    private readonly ContextAwareFallbackStrategy<GameRequest, GameResult> _fallbackStrategy;
    private readonly HashSet<string> _askedQuestions = new(StringComparer.OrdinalIgnoreCase);

    public AiQuestionExecutor(OrchestratorSettings settings, AgentDefinition agent, GameDefinition gameConfig)
    {
        _settings = settings;
        _agent = agent;
        _gameConfig = gameConfig;
        _chatClient = LlmClientFactory.CreateChatClient(settings);

        // Use SDK's PromptTemplateBuilder
        _systemPrompt = PromptTemplateBuilder.BuildAgentPrompt(agent, new Dictionary<string, string>
        {
            ["max_questions"] = gameConfig.Rules.MaxQuestions.ToString(),
            ["game_name"] = gameConfig.Name
        });

        // Use SDK's ContextAwareFallbackStrategy
        _fallbackStrategy = new ContextAwareFallbackStrategy<GameRequest, GameResult>(
            agent.Fallback ?? new FallbackConfig(),
            agent.GuessRules,
            CreateResult);
    }

    public async Task<GameResult> ExecuteAsync(
        GameRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        var maxQuestions = _gameConfig.Rules.MaxQuestions;
        var remaining = maxQuestions - request.QuestionNumber + 1;
        var mustGuess = remaining <= 1;

        // Track history
        foreach (var qa in request.History)
            _askedQuestions.Add(qa.Question);

        // Use SDK's ResilienceSettings
        var resilience = ResilienceSettings.FromConfig(_agent.Resilience);

        for (int attempt = 1; attempt <= resilience.MaxRetries; attempt++)
        {
            try
            {
                var content = await CallLlmAsync(request, maxQuestions, remaining, mustGuess, cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    onOutput?.Invoke(new TaskOutput
                    {
                        RequestId = request.RequestId,
                        Type = TaskOutputType.Output,
                        Content = content
                    });
                    return ParseResponse(request.RequestId, content, mustGuess);
                }

                if (attempt < resilience.MaxRetries)
                    await Task.Delay(resilience.InitialDelayMs * attempt, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (attempt < resilience.MaxRetries)
                    await Task.Delay(resilience.InitialDelayMs * attempt, cancellationToken);
            }
        }

        // Use SDK's fallback strategy with proper context
        var context = new FallbackContext<GameRequest>
        {
            FailedRequest = request,
            Iteration = request.QuestionNumber,
            PreviousOutputs = request.History.Select(h => h.Question).ToList(), // Just questions for duplicate detection
            Metadata = new Dictionary<string, object>
            {
                ["must_guess"] = mustGuess,
                ["asked_questions"] = _askedQuestions.ToList(),
                ["yes_answers"] = request.History
                    .Where(h => h.Answer.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    .Select(h => h.Question).ToList()
            }
        };

        var fallback = await _fallbackStrategy.GetFallbackAsync(context, cancellationToken);
        if (fallback != null)
        {
            _askedQuestions.Add(fallback.Content);
            // Emit TaskOutput for fallback questions too
            onOutput?.Invoke(new TaskOutput
            {
                RequestId = request.RequestId,
                Type = TaskOutputType.Output,
                Content = fallback.Output
            });
            return fallback;
        }

        // Dynamic last resort - generate unique question based on iteration
        var lastResort = GenerateUniqueLastResort(request.QuestionNumber);
        _askedQuestions.Add(lastResort);
        var result = CreateResult(request, lastResort, mustGuess);

        // Emit TaskOutput for last resort too
        onOutput?.Invoke(new TaskOutput
        {
            RequestId = request.RequestId,
            Type = TaskOutputType.Output,
            Content = result.Output
        });
        return result;
    }

    private async Task<string> CallLlmAsync(
        GameRequest request, int maxQuestions, int remaining, bool mustGuess,
        CancellationToken cancellationToken)
    {
        // Use SDK's ConversationHistoryBuilder
        var historyBuilder = new ConversationHistoryBuilder(HistoryFormatOptions.QnA);
        historyBuilder.AddTurns(
            request.History,
            h => h.Number,
            h => h.Question,
            h => h.Answer,
            h => h.Clarification);

        var historyContext = historyBuilder.Build();

        var userPrompt = $"""
            Question {request.QuestionNumber} of {maxQuestions} ({remaining} remaining)
            {(mustGuess ? "THIS IS YOUR LAST CHANCE - MAKE A GUESS!" : "")}

            {historyContext}

            Based on the above history, ask your next strategic question.
            IMPORTANT: Do NOT repeat any question from the history above.
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = LlmClientFactory.CreateCompletionOptions(
            _settings.Llm,
            maxTokensOverride: _agent.Llm?.MaxOutputTokens ?? 300,
            temperatureOverride: _agent.Llm?.Temperature);

        var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        return response.Value.Content.Count > 0 ? response.Value.Content[0].Text ?? "" : "";
    }

    private static GameResult ParseResponse(string requestId, string content, bool mustGuess)
    {
        // Use SDK's LlmResponseParser
        var question = LlmResponseParser.ExtractProperty(content, "question")
                    ?? LlmResponseParser.ExtractQuestion(content)
                    ?? content.Trim();

        var isGuess = LlmResponseParser.ExtractBoolProperty(content, "is_guess") || mustGuess;
        var confidence = LlmResponseParser.ExtractDoubleProperty(content, "confidence", 0.5);
        var reasoning = LlmResponseParser.ExtractProperty(content, "reasoning");

        return new GameResult
        {
            RequestId = requestId,
            Success = true,
            Output = content,
            Content = question,
            IsGuess = isGuess,
            Confidence = confidence,
            Reasoning = reasoning
        };
    }

    private string GenerateUniqueLastResort(int questionNumber)
    {
        // Pool of diverse last-resort questions that won't repeat
        var lastResortPool = new[]
        {
            "Is it found in nature?",
            "Is it bigger than a car?",
            "Can it be found in Africa?",
            "Is it dangerous to humans?",
            "Is it commonly seen in zoos?",
            "Does it have four legs?",
            "Is it a carnivore?",
            "Can it climb trees?",
            "Is it active during daytime?",
            "Does it live in groups?",
            "Is it native to a warm climate?",
            "Can it run fast?",
            "Does it make loud sounds?",
            "Is it often depicted in children's books?",
            "Is it endangered?",
            "Does it have distinctive markings?",
            "Is it featured in documentaries?",
            "Can it be found on multiple continents?",
            "Is it related to prehistoric animals?",
            "Does it migrate seasonally?"
        };

        // Find first unused question from pool
        foreach (var q in lastResortPool)
        {
            if (!_askedQuestions.Contains(q))
                return q;
        }

        // If all pool questions used, generate numbered fallback
        return $"Is it something unique in category {questionNumber}?";
    }

    private static GameResult CreateResult(GameRequest request, string content, bool isGuess) => new()
    {
        RequestId = request.RequestId,
        Success = !isGuess, // Fallback questions are not "successful" LLM calls
        Output = $@"{{""question"": ""{content}"", ""is_guess"": {isGuess.ToString().ToLower()}}}",
        Content = content,
        IsGuess = isGuess,
        Reasoning = isGuess ? "Fallback guess" : "Fallback question"
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// AI-powered answer oracle using SDK utilities
/// </summary>
public class AiAnswerOracle : IOracleVerifier
{
    private readonly ChatClient _chatClient;
    private readonly AgentDefinition _agent;
    private readonly OrchestratorSettings _settings;
    private readonly GameState _state;
    private readonly string _systemPrompt;

    public bool IsConfigured => !string.IsNullOrEmpty(_state.Secret);

    public AiAnswerOracle(OrchestratorSettings settings, AgentDefinition agent, GameState state)
    {
        _settings = settings;
        _agent = agent;
        _state = state;
        _chatClient = LlmClientFactory.CreateChatClient(settings);
        _systemPrompt = PromptTemplateBuilder.BuildAgentPrompt(agent);
    }

    public async Task<string> GenerateSecretAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("Generate a single word or short phrase for a 20 Questions game. Must be specific and guessable. OUTPUT: Just the word, nothing else."),
                new UserChatMessage("Generate a secret:")
            };

            var options = LlmClientFactory.CreateCompletionOptions(_settings.Llm, maxTokensOverride: 50, temperatureOverride: 1.0f);
            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text ?? "" : "";

            return string.IsNullOrEmpty(content) ? "elephant" : content.Trim().Trim('"', '\'');
        }
        catch { return "elephant"; }
    }

    public async Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var isGuess = LlmResponseParser.ExtractBoolProperty(executionOutput, "is_guess");
        var question = LlmResponseParser.ExtractProperty(executionOutput, "question") ?? executionOutput.Trim();

        return isGuess
            ? await ValidateGuessAsync(question, cancellationToken)
            : await AnswerQuestionAsync(question, cancellationToken);
    }

    private async Task<OracleVerdict> AnswerQuestionAsync(string question, CancellationToken cancellationToken)
    {
        var contextPrompt = $"SECRET WORD: \"{_state.Secret}\"\n\n{_systemPrompt}";

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(contextPrompt),
                new UserChatMessage($"Question: {question}")
            };

            var options = LlmClientFactory.CreateCompletionOptions(
                _settings.Llm,
                maxTokensOverride: _agent.Llm?.MaxOutputTokens ?? 200,
                temperatureOverride: _agent.Llm?.Temperature ?? 0.3f);

            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text ?? "" : "";

            var answer = LlmResponseParser.ExtractProperty(content, "answer") ?? ParseSimpleAnswer(content);
            var explanation = LlmResponseParser.ExtractProperty(content, "explanation");

            _state.AddQA(question, answer, explanation);

            return new OracleVerdict
            {
                IsComplete = false,
                CanContinue = true,
                Analysis = answer + (explanation != null ? $" ({explanation})" : ""),
                Confidence = 0.5
            };
        }
        catch (Exception ex)
        {
            _state.AddQA(question, "Maybe", $"Error: {ex.Message}");
            return new OracleVerdict { IsComplete = false, CanContinue = true, Analysis = "Maybe", Confidence = 0.3 };
        }
    }

    private async Task<OracleVerdict> ValidateGuessAsync(string guess, CancellationToken cancellationToken)
    {
        if (string.Equals(guess.Trim(), _state.Secret?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _state.RecordGuess(true, guess);
            return OracleVerdict.GoalAchieved($"Correct! The answer was: {_state.Secret}", confidence: 1.0);
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"Check if this guess matches the secret \"{_state.Secret}\". Accept synonyms. OUTPUT JSON: {{\"is_correct\": true/false}}"),
                new UserChatMessage($"Guess: {guess}")
            };

            var options = LlmClientFactory.CreateCompletionOptions(_settings.Llm, temperatureOverride: 0.1f);
            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text ?? "" : "";

            var isCorrect = content.Contains("true", StringComparison.OrdinalIgnoreCase);
            _state.RecordGuess(isCorrect, guess);

            return isCorrect
                ? OracleVerdict.GoalAchieved($"Correct! The answer was: {_state.Secret}", confidence: 1.0)
                : OracleVerdict.ContinueToNextIteration("Incorrect guess", confidence: 0.3);
        }
        catch
        {
            _state.RecordGuess(false, guess);
            return OracleVerdict.ContinueToNextIteration("Could not validate guess", confidence: 0.3);
        }
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
        => $"Verify: {executionOutput}";

    private static string ParseSimpleAnswer(string content)
    {
        var lower = content.ToLowerInvariant();
        if (LlmResponseParser.IsYesAnswer(lower)) return "Yes";
        if (LlmResponseParser.IsNoAnswer(lower)) return "No";
        return "Maybe";
    }
}

// ============================================================================
// Human Executors - Console-based interaction
// ============================================================================

/// <summary>
/// Human question input via console
/// </summary>
public class HumanQuestionExecutor : ITaskExecutor<GameRequest, GameResult>
{
    private readonly GameDefinition _config;

    public HumanQuestionExecutor(GameDefinition config) => _config = config;

    public Task<GameResult> ExecuteAsync(
        GameRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        var remaining = _config.Rules.MaxQuestions - request.QuestionNumber + 1;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n{_config.Prompts.QuestionsRemaining.Replace("{remaining}", remaining.ToString())}");
        Console.ResetColor();

        if (request.History.Count > 0)
        {
            Console.WriteLine(_config.Prompts.HistoryLabel);
            foreach (var qa in request.History.TakeLast(5))
            {
                var clarification = string.IsNullOrEmpty(qa.Clarification) ? "" : $" ({qa.Clarification})";
                Console.WriteLine($"   Q{qa.Number}: {qa.Question} → {qa.Answer}{clarification}");
            }
        }

        string content;
        bool isGuess;

        while (true)
        {
            Console.WriteLine($"\n{_config.Prompts.InputHint}");
            Console.WriteLine($"   {_config.Prompts.YesNoHint}");
            Console.Write(_config.Prompts.YourMove);

            var input = Console.ReadLine() ?? "";
            isGuess = input.StartsWith("guess:", StringComparison.OrdinalIgnoreCase);
            content = isGuess ? input[6..].Trim() : input.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"⚠️  {_config.Validation.Messages.Empty}");
                Console.ResetColor();
                continue;
            }

            if (isGuess || ValidateYesNoQuestion(content))
                break;
        }

        if (!isGuess && !content.EndsWith("?"))
            content += "?";

        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Type = TaskOutputType.Output, Content = content });

        return Task.FromResult(new GameResult
        {
            RequestId = request.RequestId,
            Success = true,
            Output = content,
            Content = content,
            IsGuess = isGuess,
            Reasoning = "Human input"
        });
    }

    private bool ValidateYesNoQuestion(string question)
    {
        var lower = question.ToLowerInvariant();

        foreach (var pattern in _config.Validation.InvalidPatterns.All)
        {
            if (lower.StartsWith(pattern) || lower.Contains($" {pattern}"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  {_config.Validation.Messages.OpenEnded}");
                Console.WriteLine($"   {_config.Validation.Messages.Examples}");
                Console.ResetColor();
                return false;
            }
        }

        return true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Human answer oracle via console
/// </summary>
public class HumanAnswerOracle : IOracleVerifier
{
    private readonly GameState _state;
    private readonly GameDefinition _config;

    public bool IsConfigured => !string.IsNullOrEmpty(_state.Secret);

    public HumanAnswerOracle(GameState state, GameDefinition config)
    {
        _state = state;
        _config = config;
    }

    public Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var isGuess = LlmResponseParser.ExtractBoolProperty(executionOutput, "is_guess");
        var content = LlmResponseParser.ExtractProperty(executionOutput, "question") ?? executionOutput.Trim();

        return Task.FromResult(isGuess ? ValidateGuess(content) : AnswerQuestion(content));
    }

    private OracleVerdict AnswerQuestion(string question)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n{_config.Prompts.AiAsks.Replace("{question}", question)}");
        Console.ResetColor();
        Console.WriteLine($"   {_config.Prompts.YourSecret.Replace("{secret}", _state.Secret ?? "")}");
        Console.Write($"   {_config.Prompts.AnswerPrompt}");

        var input = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "M";
        var answer = input switch
        {
            "Y" or "YES" => "Yes",
            "N" or "NO" => "No",
            _ => "Maybe"
        };

        string? clarification = null;
        if (answer == "Maybe")
        {
            Console.Write($"   {_config.Prompts.Clarification}");
            clarification = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(clarification)) clarification = null;
        }

        _state.AddQA(question, answer, clarification);

        return new OracleVerdict
        {
            IsComplete = false,
            CanContinue = true,
            Analysis = answer + (clarification != null ? $" ({clarification})" : ""),
            Confidence = 0.5,
            NextPromptSuggestion = "Continue"
        };
    }

    private OracleVerdict ValidateGuess(string guess)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n{_config.Prompts.AiGuesses.Replace("{guess}", guess)}");
        Console.ResetColor();
        Console.WriteLine($"   {_config.Prompts.YourSecret.Replace("{secret}", _state.Secret ?? "")}");
        Console.Write($"   {_config.Prompts.CorrectPrompt}");

        var input = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "N";
        var isCorrect = input is "Y" or "YES";

        _state.RecordGuess(isCorrect, guess);

        return isCorrect
            ? new OracleVerdict { IsComplete = true, CanContinue = false, Analysis = $"🎉 AI wins! The answer was: {_state.Secret}", Confidence = 1.0 }
            : new OracleVerdict { IsComplete = false, CanContinue = _state.GuessAttempts < 3, Analysis = "❌ Incorrect guess", Confidence = 0.3, NextPromptSuggestion = "Continue" };
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
        => $"Human validation for: {executionOutput}";
}
