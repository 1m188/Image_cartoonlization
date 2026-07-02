// ============================================================================
// 文件名:   ImageIOTests.cs
// 功能描述:  ImageIO 模块的单元测试。
//           测试 JPEG/PNG 加载、保存、格式校验和错误处理。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class ImageIOTests
{
    /// <summary>
    /// 获取仓库根目录路径。测试运行时从 bin 目录向上 5 级到达仓库根目录。
    /// </summary>
    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string TestJpgPath => Path.Combine(RepoRoot, "test.jpg");

    [Fact]
    public void LoadImage_ValidJpeg_LoadsCorrectDimensions()
    {
        var img = ImageIO.LoadImage(TestJpgPath);

        Assert.Equal(2812, img.Width);
        Assert.Equal(1280, img.Height);
        Assert.Equal(3, img.Channels);
    }

    [Fact]
    public void LoadImage_ValidJpeg_PixelsInRange()
    {
        var img = ImageIO.LoadImage(TestJpgPath);

        foreach (var v in img.Data)
        {
            Assert.InRange(v, 0.0f, 1.0f);
        }
    }

    [Fact]
    public void LoadImage_NonExistentFile_ReturnsFalse()
    {
        var result = ImageIO.TryLoadImage("nonexistent.jpg", out _);

        Assert.False(result);
    }

    [Fact]
    public void LoadImage_NonExistentFile_ReturnsError()
    {
        var result = ImageIO.TryLoadImage("nonexistent.png", out var img, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void LoadImage_GifFile_ReturnsFalse()
    {
        var result = ImageIO.TryLoadImage("test.gif", out var img, out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void SaveImage_Png_RoundTrip()
    {
        var original = ImageIO.LoadImage(TestJpgPath);
        var tempPath = Path.Combine(Path.GetTempPath(), "test_roundtrip.png");

        try
        {
            ImageIO.SaveImage(tempPath, original);
            var reloaded = ImageIO.LoadImage(tempPath);

            Assert.Equal(original.Width, reloaded.Width);
            Assert.Equal(original.Height, reloaded.Height);
            Assert.Equal(original.Channels, reloaded.Channels);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveImage_Jpeg_ProducesValidFile()
    {
        var img = new ImageData(10, 10, 3);
        for (var i = 0; i < img.Data.Length; i++)
        {
            img.Data[i] = (float)(i % 256) / 255f;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), "test_output.jpg");

        try
        {
            ImageIO.SaveImage(tempPath, img);
            Assert.True(File.Exists(tempPath));

            var reloaded = ImageIO.LoadImage(tempPath);
            Assert.Equal(10, reloaded.Width);
            Assert.Equal(10, reloaded.Height);
            Assert.Equal(3, reloaded.Channels);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveImage_BmpExtension_Throws()
    {
        var img = new ImageData(4, 4, 3);
        var tempPath = Path.Combine(Path.GetTempPath(), "test_output.bmp");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ImageIO.SaveImage(tempPath, img));
        Assert.Contains(".bmp", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
