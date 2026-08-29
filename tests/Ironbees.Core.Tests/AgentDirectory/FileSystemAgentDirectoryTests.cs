using Ironbees.Core.AgentDirectory;
using Xunit;

namespace Ironbees.Core.Tests.AgentDirectory;

public class FileSystemAgentDirectoryTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _agentPath;
    private readonly FileSystemAgentDirectory _directory;

    public FileSystemAgentDirectoryTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ironbees-tests", Guid.NewGuid().ToString("N"));
        _agentPath = Path.Combine(_testRoot, "test-agent");
        System.IO.Directory.CreateDirectory(_agentPath);
        _directory = new FileSystemAgentDirectory("test-agent", _agentPath);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (System.IO.Directory.Exists(_testRoot))
        {
            System.IO.Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        Assert.Equal("test-agent", _directory.AgentName);
        Assert.Equal(Path.GetFullPath(_agentPath), _directory.RootPath);
    }

    [Fact]
    public void Constructor_ThrowsOnNullAgentName()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileSystemAgentDirectory(null!, _agentPath));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRootPath()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileSystemAgentDirectory("test", null!));
    }

    [Theory]
    [InlineData(AgentSubdirectory.Inbox, "inbox")]
    [InlineData(AgentSubdirectory.Outbox, "outbox")]
    [InlineData(AgentSubdirectory.Memory, "memory")]
    [InlineData(AgentSubdirectory.Workspace, "workspace")]
    [InlineData(AgentSubdirectory.Logs, "logs")]
    public void GetSubdirectoryPath_ReturnsCorrectPath(AgentSubdirectory subdirectory, string expected)
    {
        var path = _directory.GetSubdirectoryPath(subdirectory);
        Assert.EndsWith(expected, path);
    }

    [Fact]
    public async Task EnsureDirectoryStructureAsync_CreatesAllSubdirectories()
    {
        // Act
        var result = await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.True(System.IO.Directory.Exists(_directory.GetSubdirectoryPath(AgentSubdirectory.Inbox)));
        Assert.True(System.IO.Directory.Exists(_directory.GetSubdirectoryPath(AgentSubdirectory.Outbox)));
        Assert.True(System.IO.Directory.Exists(_directory.GetSubdirectoryPath(AgentSubdirectory.Memory)));
        Assert.True(System.IO.Directory.Exists(_directory.GetSubdirectoryPath(AgentSubdirectory.Workspace)));
        Assert.True(System.IO.Directory.Exists(_directory.GetSubdirectoryPath(AgentSubdirectory.Logs)));
    }

    [Fact]
    public async Task EnsureDirectoryStructureAsync_CreatesGitKeepFiles()
    {
        // Act
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Assert
        foreach (var subdirectory in Enum.GetValues<AgentSubdirectory>())
        {
            var gitKeepPath = Path.Combine(_directory.GetSubdirectoryPath(subdirectory), ".gitkeep");
            Assert.True(File.Exists(gitKeepPath));
        }
    }

    [Fact]
    public async Task WriteFileAsync_CreatesFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        var content = "test content";

        // Act
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", content, TestContext.Current.CancellationToken);

        // Assert
        var filePath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Memory), "test.txt");
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteFileAsync_Binary_CreatesFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        var content = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "test.bin", content, TestContext.Current.CancellationToken);

        // Assert
        var filePath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Workspace), "test.bin");
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteFileAsync_ThrowsOnPathTraversal()
    {
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _directory.WriteFileAsync(AgentSubdirectory.Memory, "../../../evil.txt", "content", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsContent()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        var content = "test content";
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", content, TestContext.Current.CancellationToken);

        // Act
        var result = await _directory.ReadFileAsync(AgentSubdirectory.Memory, "test.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsNullForNonexistentFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _directory.ReadFileAsync(AgentSubdirectory.Memory, "nonexistent.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsFileList()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file1.txt", "content1", TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file2.txt", "content2", TestContext.Current.CancellationToken);

        // Act
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Memory, "*.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, files.Count);
        Assert.Contains("file1.txt", files);
        Assert.Contains("file2.txt", files);
    }

    [Fact]
    public async Task ListFilesAsync_ExcludesGitKeep()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Act
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Memory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(".gitkeep", files);
    }

    [Fact]
    public async Task DeleteFileAsync_DeletesFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", "content", TestContext.Current.CancellationToken);

        // Act
        var result = await _directory.DeleteFileAsync(AgentSubdirectory.Memory, "test.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.False(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "test.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalseForNonexistent()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _directory.DeleteFileAsync(AgentSubdirectory.Memory, "nonexistent.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsCorrectResult()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "exists.txt", "content", TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "exists.txt", TestContext.Current.CancellationToken));
        Assert.False(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "nonexistent.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AppendToLogAsync_AppendsWithTimestamp()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);

        // Act
        await _directory.AppendToLogAsync("test.log", "First entry", TestContext.Current.CancellationToken);
        await _directory.AppendToLogAsync("test.log", "Second entry", TestContext.Current.CancellationToken);

        // Assert
        var logContent = await _directory.ReadFileAsync(AgentSubdirectory.Logs, "test.log", TestContext.Current.CancellationToken);
        Assert.NotNull(logContent);
        Assert.Contains("First entry", logContent);
        Assert.Contains("Second entry", logContent);
        Assert.Contains("[", logContent); // Timestamp marker
    }

    [Fact]
    public async Task CleanWorkspaceAsync_RemovesAllFiles()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "temp1.txt", "content1", TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "temp2.txt", "content2", TestContext.Current.CancellationToken);

        // Act
        var deleted = await _directory.CleanWorkspaceAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, deleted);
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Workspace, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(files);
    }

    [Fact]
    public async Task GetDirectoryInfoAsync_ReturnsCorrectInfo()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync(TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file1.txt", "content", TestContext.Current.CancellationToken);
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file2.txt", "more content", TestContext.Current.CancellationToken);

        // Act
        var info = await _directory.GetDirectoryInfoAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("test-agent", info.AgentName);
        Assert.Equal(2, info.FileCountBySubdirectory[AgentSubdirectory.Memory]);
        Assert.True(info.TotalSizeBytes > 0);
    }

    [Fact]
    public async Task CreateAsync_CreatesDirectoryWithStructure()
    {
        // Act
        var directory = await FileSystemAgentDirectory.CreateAsync(_testRoot, "new-agent", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(directory);
        Assert.Equal("new-agent", directory.AgentName);
        Assert.True(System.IO.Directory.Exists(directory.GetSubdirectoryPath(AgentSubdirectory.Inbox)));
    }

    [Fact]
    public void Open_ReturnsNullForNonexistentDirectory()
    {
        var result = FileSystemAgentDirectory.Open(Path.Combine(_testRoot, "nonexistent"));
        Assert.Null(result);
    }
}
