using Ironbees.Core.Embeddings;

namespace Ironbees.Core.Tests.Embeddings;

public class ModelDownloaderTests
{
    [Fact]
    public void DefaultCacheDirectory_ShouldBeInUserProfile()
    {
        var cacheDir = ModelDownloader.DefaultCacheDirectory;

        Assert.Contains(".ironbees", cacheDir);
        Assert.Contains("models", cacheDir);
    }

    [Fact]
    public void GetModelPath_ReturnsCorrectPath()
    {
        var downloader = new ModelDownloader();
        var modelPath = downloader.GetModelPath("all-MiniLM-L6-v2");

        Assert.Contains("all-MiniLM-L6-v2", modelPath);
    }

    [Fact]
    public void ClearAllCache_CreatesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ironbees-test-" + Guid.NewGuid());
        var downloader = new ModelDownloader(tempDir);

        downloader.ClearAllCache();

        Assert.True(Directory.Exists(tempDir));

        // Cleanup
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
