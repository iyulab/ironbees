// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Core.Workflow;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

public class WorkflowTemplateExceptionsTests
{
    [Fact]
    public void WorkflowTemplateNotFoundException_SetsProperties()
    {
        // Arrange
        var templateName = "my-template";
        var searchedPaths = new[] { "/path/one", "/path/two" };

        // Act
        var exception = new WorkflowTemplateNotFoundException(templateName, searchedPaths);

        // Assert
        Assert.Equal("my-template", exception.TemplateName);
        Assert.Equal(2, exception.SearchedPaths.Count);
        Assert.Contains("my-template", exception.Message);
        Assert.Contains("/path/one", exception.Message);
    }

    [Fact]
    public void WorkflowTemplateResolutionException_WithUnresolvedParams_SetsProperties()
    {
        // Arrange
        var templateName = "template";
        var unresolvedParams = new[] { "missing.param1", "missing.param2" };

        // Act
        var exception = new WorkflowTemplateResolutionException(templateName, unresolvedParams);

        // Assert
        Assert.Equal("template", exception.TemplateName);
        Assert.Equal(2, exception.UnresolvedParameters.Count);
        Assert.Contains("missing.param1", exception.Message);
    }

    [Fact]
    public void WorkflowTemplateResolutionException_WithInnerException_SetsProperties()
    {
        // Arrange
        var inner = new InvalidOperationException("inner error");

        // Act
        var exception = new WorkflowTemplateResolutionException("template", "parse failed", inner);

        // Assert
        Assert.Equal("template", exception.TemplateName);
        Assert.Empty(exception.UnresolvedParameters);
        Assert.Contains("parse failed", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void WorkflowTemplateValidationResult_Success_IsValid()
    {
        // Act
        var result = WorkflowTemplateValidationResult.Success(
            "template",
            ["goal.id", "goal.name"]);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("template", result.TemplateName);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void WorkflowTemplateValidationResult_Failure_IsNotValid()
    {
        // Act
        var result = WorkflowTemplateValidationResult.Failure(
            "template",
            ["Error 1", "Error 2"]);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("template", result.TemplateName);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void WorkflowTemplateValidationResult_WithWarnings_IsStillValid()
    {
        // Act
        var result = new WorkflowTemplateValidationResult
        {
            TemplateName = "template",
            Warnings = ["Warning 1"]
        };

        // Assert
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
    }
}
