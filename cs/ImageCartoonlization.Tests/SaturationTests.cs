// ============================================================================
// 文件名:   SaturationTests.cs
// 功能描述:  Saturation 模块的单元测试。
//           测试饱和度调整公式 (1-s)*gray + s*color。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class SaturationTests
{
    [Fact]
    public void AdjustSaturation_ScalarOne_Identity()
    {
        var img = new ImageData(2, 2, 3);
        img.SetPixel(0, 0, 0, 0.2f);
        img.SetPixel(0, 0, 1, 0.5f);
        img.SetPixel(0, 0, 2, 0.8f);
        img.SetPixel(0, 1, 0, 0.1f);
        img.SetPixel(0, 1, 1, 0.3f);
        img.SetPixel(0, 1, 2, 0.6f);

        var result = Saturation.AdjustSaturation(img, 1.0f);

        Assert.Equal(0.2f, result.GetPixel(0, 0, 0), 1e-6f);
        Assert.Equal(0.5f, result.GetPixel(0, 0, 1), 1e-6f);
        Assert.Equal(0.8f, result.GetPixel(0, 0, 2), 1e-6f);
        Assert.Equal(0.1f, result.GetPixel(0, 1, 0), 1e-6f);
        Assert.Equal(0.3f, result.GetPixel(0, 1, 1), 1e-6f);
        Assert.Equal(0.6f, result.GetPixel(0, 1, 2), 1e-6f);
    }

    [Fact]
    public void AdjustSaturation_ScalarZero_Grayscale()
    {
        var img = new ImageData(2, 2, 3);
        img.SetPixel(0, 0, 0, 0.2f);
        img.SetPixel(0, 0, 1, 0.5f);
        img.SetPixel(0, 0, 2, 0.8f);

        var result = Saturation.AdjustSaturation(img, 0.0f);

        var r = result.GetPixel(0, 0, 0);
        var g = result.GetPixel(0, 0, 1);
        var b = result.GetPixel(0, 0, 2);

        Assert.Equal(r, g, 1e-6f);
        Assert.Equal(g, b, 1e-6f);
    }

    [Fact]
    public void AdjustSaturation_ScalarTwo_VarianceIncreases()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.2f);
        img.SetPixel(0, 0, 1, 0.5f);
        img.SetPixel(0, 0, 2, 0.8f);

        var inputVariance = ChannelVariance(img, 0, 0);
        var result = Saturation.AdjustSaturation(img, 2.0f);
        var outputVariance = ChannelVariance(result, 0, 0);

        Assert.True(outputVariance >= inputVariance,
            $"输出方差 ({outputVariance}) 应 >= 输入方差 ({inputVariance})");
    }

    [Fact]
    public void AdjustSaturation_ClampedTo01()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.9f);
        img.SetPixel(0, 0, 1, 0.9f);
        img.SetPixel(0, 0, 2, 0.9f);

        var result = Saturation.AdjustSaturation(img, 5.0f);

        Assert.InRange(result.GetPixel(0, 0, 0), 0.0f, 1.0f);
        Assert.InRange(result.GetPixel(0, 0, 1), 0.0f, 1.0f);
        Assert.InRange(result.GetPixel(0, 0, 2), 0.0f, 1.0f);
    }

    [Fact]
    public void AdjustSaturation_DimensionsPreserved()
    {
        var img = new ImageData(5, 3, 3);
        var result = Saturation.AdjustSaturation(img, 2.0f);

        Assert.Equal(5, result.Width);
        Assert.Equal(3, result.Height);
        Assert.Equal(3, result.Channels);
    }

    [Fact]
    public void AdjustSaturation_WrongChannels_Throws()
    {
        var img = new ImageData(3, 3, 1);
        var ex = Assert.Throws<ArgumentException>(() =>
            Saturation.AdjustSaturation(img, 2.0f));
        Assert.Contains("3 通道", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static float ChannelVariance(ImageData img, int y, int x)
    {
        var r = img.GetPixel(y, x, 0);
        var g = img.GetPixel(y, x, 1);
        var b = img.GetPixel(y, x, 2);
        var mean = (r + g + b) / 3f;
        var rDiff = r - mean;
        var gDiff = g - mean;
        var bDiff = b - mean;
        return (rDiff * rDiff + gDiff * gDiff + bDiff * bDiff) / 3f;
    }
}
