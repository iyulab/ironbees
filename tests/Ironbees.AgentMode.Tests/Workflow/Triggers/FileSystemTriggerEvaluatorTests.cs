using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Workflow.Triggers;

namespace Ironbees.AgentMode.Tests.Workflow.Triggers;

public class FileSystemTriggerEvaluatorTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemTriggerEvaluatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"trigger-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private TriggerEvaluationContext CreateContext() => new()
    {
        WorkingDirectory = _tempDir
    };

    // --- FileExistsTriggerEvaluator ---

    [Fact]
    public async Task FileExists_FilePresent_ShouldReturnTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var evaluator = new FileExistsTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "test.txt" };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.True(result);
    }

    [Fact]
    public async Task FileExists_FileMissing_ShouldReturnFalse()
    {
        var evaluator = new FileExistsTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "nonexistent.txt" };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.False(result);
    }

    [Fact]
    public async Task FileExists_AbsolutePath_ShouldWork()
    {
        var filePath = Path.Combine(_tempDir, "absolute.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var evaluator = new FileExistsTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = filePath };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.True(result);
    }

    [Fact]
    public async Task FileExists_NullPath_ShouldThrow()
    {
        var evaluator = new FileExistsTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = null };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            evaluator.EvaluateAsync(trigger, CreateContext()));
    }

    [Fact]
    public void FileExists_TriggerType_ShouldBeFileExists()
    {
        var evaluator = new FileExistsTriggerEvaluator();
        Assert.Equal(TriggerType.FileExists, evaluator.TriggerType);
    }

    // --- DirectoryNotEmptyTriggerEvaluator ---

    [Fact]
    public async Task DirectoryNotEmpty_WithFiles_ShouldReturnTrue()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "content");

        var evaluator = new DirectoryNotEmptyTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.DirectoryNotEmpty, Path = "sub" };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.True(result);
    }

    [Fact]
    public async Task DirectoryNotEmpty_Empty_ShouldReturnFalse()
    {
        var subDir = Path.Combine(_tempDir, "empty-sub");
        Directory.CreateDirectory(subDir);

        var evaluator = new DirectoryNotEmptyTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.DirectoryNotEmpty, Path = "empty-sub" };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.False(result);
    }

    [Fact]
    public async Task DirectoryNotEmpty_NonExistentDir_ShouldReturnFalse()
    {
        var evaluator = new DirectoryNotEmptyTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.DirectoryNotEmpty, Path = "missing-dir" };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.False(result);
    }

    [Fact]
    public async Task DirectoryNotEmpty_NullPath_ShouldThrow()
    {
        var evaluator = new DirectoryNotEmptyTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.DirectoryNotEmpty, Path = null };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            evaluator.EvaluateAsync(trigger, CreateContext()));
    }

    [Fact]
    public void DirectoryNotEmpty_TriggerType_ShouldBeDirectoryNotEmpty()
    {
        var evaluator = new DirectoryNotEmptyTriggerEvaluator();
        Assert.Equal(TriggerType.DirectoryNotEmpty, evaluator.TriggerType);
    }

    // --- ImmediateTriggerEvaluator ---

    [Fact]
    public async Task Immediate_ShouldAlwaysReturnTrue()
    {
        var evaluator = new ImmediateTriggerEvaluator();
        var trigger = new TriggerDefinition { Type = TriggerType.Immediate };

        var result = await evaluator.EvaluateAsync(trigger, CreateContext());

        Assert.True(result);
    }

    [Fact]
    public void Immediate_TriggerType_ShouldBeImmediate()
    {
        var evaluator = new ImmediateTriggerEvaluator();
        Assert.Equal(TriggerType.Immediate, evaluator.TriggerType);
    }

    // --- TriggerEvaluatorFactory ---

    [Fact]
    public void Factory_DefaultConstructor_ShouldHaveAllBuiltInTypes()
    {
        var factory = new TriggerEvaluatorFactory();

        Assert.IsType<FileExistsTriggerEvaluator>(factory.GetEvaluator(TriggerType.FileExists));
        Assert.IsType<DirectoryNotEmptyTriggerEvaluator>(factory.GetEvaluator(TriggerType.DirectoryNotEmpty));
        Assert.IsType<ImmediateTriggerEvaluator>(factory.GetEvaluator(TriggerType.Immediate));
    }

    [Fact]
    public void Factory_UnsupportedType_ShouldThrow()
    {
        var factory = new TriggerEvaluatorFactory();

        Assert.Throws<NotSupportedException>(() =>
            factory.GetEvaluator(TriggerType.Expression));
    }

    [Fact]
    public void Factory_CustomEvaluators_ShouldWork()
    {
        var customEvaluator = new ImmediateTriggerEvaluator();
        var factory = new TriggerEvaluatorFactory([customEvaluator]);

        var result = factory.GetEvaluator(TriggerType.Immediate);

        Assert.Same(customEvaluator, result);
    }

    [Fact]
    public void Factory_CustomEvaluators_MissingType_ShouldThrow()
    {
        // Factory with only Immediate evaluator
        var factory = new TriggerEvaluatorFactory([new ImmediateTriggerEvaluator()]);

        Assert.Throws<NotSupportedException>(() =>
            factory.GetEvaluator(TriggerType.FileExists));
    }
}
