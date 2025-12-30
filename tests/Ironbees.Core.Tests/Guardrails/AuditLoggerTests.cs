// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class AuditLoggerTests
{
    [Fact]
    public async Task NullAuditLogger_LogInputValidationAsync_CompletesSuccessfully()
    {
        // Arrange
        var logger = NullAuditLogger.Instance;
        var entry = new GuardrailAuditEntry
        {
            GuardrailName = "TestGuardrail",
            Direction = ValidationDirection.Input,
            Result = GuardrailResult.Allowed("TestGuardrail")
        };

        // Act & Assert - should complete without throwing
        await logger.LogInputValidationAsync(entry);
    }

    [Fact]
    public async Task NullAuditLogger_LogOutputValidationAsync_CompletesSuccessfully()
    {
        // Arrange
        var logger = NullAuditLogger.Instance;
        var entry = new GuardrailAuditEntry
        {
            GuardrailName = "TestGuardrail",
            Direction = ValidationDirection.Output,
            Result = GuardrailResult.Allowed("TestGuardrail")
        };

        // Act & Assert - should complete without throwing
        await logger.LogOutputValidationAsync(entry);
    }

    [Fact]
    public void NullAuditLogger_Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = NullAuditLogger.Instance;
        var instance2 = NullAuditLogger.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GuardrailAuditEntry_DefaultValues_AreSet()
    {
        // Arrange & Act
        var entry = new GuardrailAuditEntry
        {
            GuardrailName = "TestGuardrail",
            Direction = ValidationDirection.Input,
            Result = GuardrailResult.Allowed("TestGuardrail")
        };

        // Assert
        Assert.NotEmpty(entry.Id);
        Assert.True(entry.Timestamp <= DateTimeOffset.UtcNow);
        Assert.Empty(entry.Metadata);
    }

    [Fact]
    public void GuardrailAuditEntry_AllProperties_CanBeSet()
    {
        // Arrange
        var result = GuardrailResult.Blocked("TestGuardrail", "Test reason");
        var metadata = new Dictionary<string, object> { ["Key"] = "Value" };

        // Act
        var entry = new GuardrailAuditEntry
        {
            Id = "custom-id",
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            GuardrailName = "TestGuardrail",
            Direction = ValidationDirection.Output,
            Result = result,
            ContentPreview = "Test content...",
            ContentLength = 100,
            CorrelationId = "correlation-123",
            UserId = "user-456",
            AgentId = "agent-789",
            Metadata = metadata,
            DurationMs = 42.5
        };

        // Assert
        Assert.Equal("custom-id", entry.Id);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal("TestGuardrail", entry.GuardrailName);
        Assert.Equal(ValidationDirection.Output, entry.Direction);
        Assert.Same(result, entry.Result);
        Assert.Equal("Test content...", entry.ContentPreview);
        Assert.Equal(100, entry.ContentLength);
        Assert.Equal("correlation-123", entry.CorrelationId);
        Assert.Equal("user-456", entry.UserId);
        Assert.Equal("agent-789", entry.AgentId);
        Assert.Same(metadata, entry.Metadata);
        Assert.Equal(42.5, entry.DurationMs);
    }

    [Fact]
    public void ValidationDirection_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)ValidationDirection.Input);
        Assert.Equal(1, (int)ValidationDirection.Output);
    }
}
