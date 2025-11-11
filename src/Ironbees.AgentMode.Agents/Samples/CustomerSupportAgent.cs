using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Agents.Samples;

/// <summary>
/// Example implementation of a customer support conversational agent.
/// Provides helpful, empathetic responses to customer inquiries.
/// </summary>
/// <remarks>
/// This is a sample implementation demonstrating how to use ConversationalAgent.
/// In production, customize the system prompt and add domain-specific knowledge.
///
/// Example usage:
/// <code>
/// var chatClient = new OpenAIClient(apiKey).AsChatClient("gpt-4");
/// var agent = new CustomerSupportAgent(chatClient);
///
/// var response = await agent.RespondAsync("How do I reset my password?");
/// Console.WriteLine(response);
/// </code>
/// </remarks>
public class CustomerSupportAgent : ConversationalAgent
{
    /// <summary>
    /// Default system prompt for customer support agent.
    /// </summary>
    private const string DefaultSystemPrompt = @"You are a helpful and empathetic customer support agent for a software company.

Your role is to:
- Provide clear, accurate answers to customer questions
- Be patient and understanding, even with frustrated customers
- Offer step-by-step guidance when needed
- Escalate complex issues when appropriate
- Maintain a professional yet friendly tone

Guidelines:
- Always acknowledge the customer's concern first
- Provide solutions in clear, numbered steps when applicable
- If you don't know something, admit it and offer to escalate
- End responses with an offer to help further

Keep responses concise but thorough.";

    /// <summary>
    /// Initializes a new instance of the CustomerSupportAgent with default prompt.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interactions.</param>
    public CustomerSupportAgent(IChatClient chatClient)
        : base(chatClient, DefaultSystemPrompt)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CustomerSupportAgent with custom prompt.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interactions.</param>
    /// <param name="customSystemPrompt">Custom system prompt for specialized support scenarios.</param>
    public CustomerSupportAgent(IChatClient chatClient, string customSystemPrompt)
        : base(chatClient, customSystemPrompt)
    {
    }
}
