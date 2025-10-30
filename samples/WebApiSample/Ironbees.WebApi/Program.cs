using DotNetEnv;
using Ironbees.Core;
using Ironbees.Samples.Shared;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Load .env file
var envPath = Path.Combine("..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("✅ Loaded .env file");
}

// Get configuration from environment
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");
}

// Add services to the container
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ironbees Web API",
        Version = "v1.0",
        Description = "Multi-agent orchestration API powered by Ironbees framework and OpenAI",
        Contact = new OpenApiContact
        {
            Name = "Ironbees Project",
            Url = new Uri("https://github.com/yourusername/ironbees")
        }
    });

    // Include XML comments for better documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Ironbees services
var agentsPath = Path.Combine("..", "..", "..", "agents");
builder.Services.AddSingleton<IAgentLoader>(sp => new FileSystemAgentLoader());
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddSingleton<IAgentSelector>(sp =>
    new KeywordAgentSelector(minimumConfidenceThreshold: 0.3));
builder.Services.AddSingleton<ILLMFrameworkAdapter>(sp =>
    new OpenAIAdapter(apiKey, model));
builder.Services.AddSingleton<IAgentOrchestrator>(sp =>
    new AgentOrchestrator(
        sp.GetRequiredService<IAgentLoader>(),
        sp.GetRequiredService<IAgentRegistry>(),
        sp.GetRequiredService<ILLMFrameworkAdapter>(),
        sp.GetRequiredService<IAgentSelector>(),
        agentsPath));

var app = builder.Build();

// Load agents on startup
var orchestrator = app.Services.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

var agentCount = orchestrator.ListAgents().Count;
Console.WriteLine($"✅ Loaded {agentCount} agents");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ironbees API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}

app.UseCors();
app.MapControllers();

Console.WriteLine($"🚀 Ironbees Web API running on http://localhost:5000");
Console.WriteLine($"📚 Swagger documentation: http://localhost:5000");
Console.WriteLine($"🤖 Using model: {model}");

app.Run();
