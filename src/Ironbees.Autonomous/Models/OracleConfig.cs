namespace Ironbees.Autonomous.Models;

/// <summary>
/// Configuration for oracle verification
/// </summary>
public record OracleConfig
{
    /// <summary>
    /// Model identifier (e.g., "gpt-4", "claude-3-haiku", "llama-3")
    /// </summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>
    /// Maximum tokens for response
    /// </summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// Temperature for generation (0.0 - 1.0)
    /// </summary>
    public double Temperature { get; init; } = 0.3;

    /// <summary>
    /// System prompt for oracle
    /// </summary>
    public string SystemPrompt { get; init; } = DefaultSystemPrompt;

    /// <summary>
    /// User prompt template with placeholders: {original_prompt}, {execution_output}, {context}
    /// </summary>
    public string UserPromptTemplate { get; init; } = DefaultUserPromptTemplate;

    /// <summary>
    /// Timeout for verification request
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable reflection/self-critique in oracle responses
    /// </summary>
    public bool EnableReflection { get; init; } = true;

    /// <summary>
    /// System prompt for reflection-enabled oracle
    /// </summary>
    public string ReflectionSystemPrompt { get; init; } = DefaultReflectionSystemPrompt;

    /// <summary>
    /// User prompt template for reflection mode
    /// </summary>
    public string ReflectionUserPromptTemplate { get; init; } = DefaultReflectionUserPromptTemplate;

    /// <summary>
    /// Default system prompt for oracle
    /// </summary>
    public const string DefaultSystemPrompt = """
        You are an AI execution verifier. Your job is to analyze task execution results and determine if the goal has been achieved.

        Respond ONLY with a JSON object in this exact format:
        {
            "isComplete": boolean,
            "canContinue": boolean,
            "analysis": "brief explanation of your assessment",
            "nextPromptSuggestion": "suggested follow-up prompt if not complete, or null",
            "confidence": 0.0-1.0
        }

        Guidelines:
        - isComplete: true if the original goal appears to be fully achieved
        - canContinue: true if there's meaningful work that could still be done
        - analysis: concise explanation of what was accomplished and what's missing
        - nextPromptSuggestion: specific actionable prompt for continuation, or null if complete
        - confidence: your certainty in this assessment (0.0 = uncertain, 1.0 = certain)
        """;

    /// <summary>
    /// Default user prompt template
    /// </summary>
    public const string DefaultUserPromptTemplate = """
        ## Original Goal/Task:
        {original_prompt}

        ## Execution Output:
        {execution_output}

        Analyze whether the original goal has been achieved based on the execution output.
        """;

    /// <summary>
    /// Default system prompt for reflection-enabled oracle (Reflexion pattern)
    /// </summary>
    public const string DefaultReflectionSystemPrompt = """
        You are an AI execution verifier with self-reflection capabilities.
        Your job is to analyze task execution results, reflect on the approach, and provide actionable feedback.

        Respond ONLY with a JSON object in this exact format:
        {
            "isComplete": boolean,
            "canContinue": boolean,
            "analysis": "explanation of what was accomplished",
            "nextPromptSuggestion": "refined prompt for continuation or null",
            "confidence": 0.0-1.0,
            "reflection": {
                "whatWorkedWell": "aspects of the approach that were effective",
                "whatCouldImprove": "specific areas needing improvement",
                "lessonsLearned": "key insights to apply in future iterations",
                "suggestedStrategy": "recommended approach for next attempt"
            }
        }

        Guidelines:
        - isComplete: true if the original goal appears to be fully achieved
        - canContinue: true if there's meaningful work that could still be done
        - analysis: concise explanation of accomplishments and gaps
        - nextPromptSuggestion: specific, actionable prompt incorporating reflection insights
        - confidence: certainty in this assessment (0.0 = uncertain, 1.0 = certain)
        - reflection: self-critique and learning insights for improvement
        """;

    /// <summary>
    /// Default user prompt template for reflection mode
    /// </summary>
    public const string DefaultReflectionUserPromptTemplate = """
        ## Original Goal/Task:
        {original_prompt}

        ## Execution Context:
        {context}

        ## Current Execution Output:
        {execution_output}

        Analyze the execution output against the original goal.
        Consider the execution context (previous attempts, learnings, feedback).
        Provide both verification verdict and reflection insights.
        """;
}
