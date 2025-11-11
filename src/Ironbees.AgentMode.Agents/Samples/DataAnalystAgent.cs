using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Agents.Samples;

/// <summary>
/// Example implementation of a data analyst conversational agent.
/// Provides data analysis insights, SQL queries, and statistical guidance.
/// </summary>
/// <remarks>
/// This is a sample implementation demonstrating how to use ConversationalAgent
/// for domain-specific expertise (data science, ML, analytics).
///
/// Example usage:
/// <code>
/// var chatClient = new OpenAIClient(apiKey).AsChatClient("gpt-4");
/// var agent = new DataAnalystAgent(chatClient);
///
/// var response = await agent.RespondAsync(
///     "How do I calculate the correlation between user activity and revenue?"
/// );
/// Console.WriteLine(response);
/// </code>
/// </remarks>
public class DataAnalystAgent : ConversationalAgent
{
    /// <summary>
    /// Default system prompt for data analyst agent.
    /// </summary>
    private const string DefaultSystemPrompt = @"You are an experienced data analyst and data scientist assistant.

Your expertise includes:
- SQL query writing and optimization
- Statistical analysis (descriptive, inferential, predictive)
- Data visualization recommendations
- Python/R code for data analysis (pandas, numpy, scikit-learn)
- Machine learning model selection and evaluation
- A/B testing and experimental design
- Data quality assessment and cleaning strategies

Guidelines:
- Provide clear, actionable analysis recommendations
- Include code examples when relevant (SQL, Python, R)
- Explain statistical concepts in accessible terms
- Suggest appropriate visualizations for data patterns
- Consider data quality and potential biases
- Reference relevant metrics and best practices

Keep explanations concise but thorough, with practical examples.";

    /// <summary>
    /// Initializes a new instance of the DataAnalystAgent with default prompt.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interactions.</param>
    public DataAnalystAgent(IChatClient chatClient)
        : base(chatClient, DefaultSystemPrompt)
    {
    }

    /// <summary>
    /// Initializes a new instance of the DataAnalystAgent with custom prompt.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interactions.</param>
    /// <param name="customSystemPrompt">Custom system prompt for specialized analysis scenarios.</param>
    public DataAnalystAgent(IChatClient chatClient, string customSystemPrompt)
        : base(chatClient, customSystemPrompt)
    {
    }
}
