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
        var result = await _directory.EnsureDirectoryStructureAsync();

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
        await _directory.EnsureDirectoryStructureAsync();

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
        await _directory.EnsureDirectoryStructureAsync();
        var content = "test content";

        // Act
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", content);

        // Assert
        var filePath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Memory), "test.txt");
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task WriteFileAsync_Binary_CreatesFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        var content = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "test.bin", content);

        // Assert
        var filePath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Workspace), "test.bin");
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllBytesAsync(filePath));
    }

    [Fact]
    public async Task WriteFileAsync_ThrowsOnPathTraversal()
    {
        await _directory.EnsureDirectoryStructureAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _directory.WriteFileAsync(AgentSubdirectory.Memory, "../../../evil.txt", "content"));
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsContent()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        var content = "test content";
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", content);

        // Act
        var result = await _directory.ReadFileAsync(AgentSubdirectory.Memory, "test.txt");

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsNullForNonexistentFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();

        // Act
        var result = await _directory.ReadFileAsync(AgentSubdirectory.Memory, "nonexistent.txt");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsFileList()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file1.txt", "content1");
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file2.txt", "content2");

        // Act
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Memory, "*.txt");

        // Assert
        Assert.Equal(2, files.Count);
        Assert.Contains("file1.txt", files);
        Assert.Contains("file2.txt", files);
    }

    [Fact]
    public async Task ListFilesAsync_ExcludesGitKeep()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();

        // Act
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Memory);

        // Assert
        Assert.DoesNotContain(".gitkeep", files);
    }

    [Fact]
    public async Task DeleteFileAsync_DeletesFile()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "test.txt", "content");

        // Act
        var result = await _directory.DeleteFileAsync(AgentSubdirectory.Memory, "test.txt");

        // Assert
        Assert.True(result);
        Assert.False(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "test.txt"));
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalseForNonexistent()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();

        // Act
        var result = await _directory.DeleteFileAsync(AgentSubdirectory.Memory, "nonexistent.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsCorrectResult()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "exists.txt", "content");

        // Act & Assert
        Assert.True(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "exists.txt"));
        Assert.False(await _directory.FileExistsAsync(AgentSubdirectory.Memory, "nonexistent.txt"));
    }

    [Fact]
    public async Task AppendToLogAsync_AppendsWithTimestamp()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();

        // Act
        await _directory.AppendToLogAsync("test.log", "First entry");
        await _directory.AppendToLogAsync("test.log", "Second entry");

        // Assert
        var logContent = await _directory.ReadFileAsync(AgentSubdirectory.Logs, "test.log");
        Assert.NotNull(logContent);
        Assert.Contains("First entry", logContent);
        Assert.Contains("Second entry", logContent);
        Assert.Contains("[", logContent); // Timestamp marker
    }

    [Fact]
    public async Task CleanWorkspaceAsync_RemovesAllFiles()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "temp1.txt", "content1");
        await _directory.WriteFileAsync(AgentSubdirectory.Workspace, "temp2.txt", "content2");

        // Act
        var deleted = await _directory.CleanWorkspaceAsync();

        // Assert
        Assert.Equal(2, deleted);
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Workspace);
        Assert.Empty(files);
    }

    [Fact]
    public async Task GetDirectoryInfoAsync_ReturnsCorrectInfo()
    {
        // Arrange
        await _directory.EnsureDirectoryStructureAsync();
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file1.txt", "content");
        await _directory.WriteFileAsync(AgentSubdirectory.Memory, "file2.txt", "more content");

        // Act
        var info = await _directory.GetDirectoryInfoAsync();

        // Assert
        Assert.Equal("test-agent", info.AgentName);
        Assert.Equal(2, info.FileCountBySubdirectory[AgentSubdirectory.Memory]);
        Assert.True(info.TotalSizeBytes > 0);
    }

    [Fact]
    public async Task CreateAsync_CreatesDirectoryWithStructure()
    {
        // Act
        var directory = await FileSystemAgentDirectory.CreateAsync(_testRoot, "new-agent");

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
