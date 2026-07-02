// ============================================================================
// 文件名:   BilateralFilterTests.cs
// 功能描述:  BilateralFilter 模块的单元测试。
//           测试双边滤波的恒等性、平滑效果、尺寸保持和并行一致性。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class BilateralFilterTests
{
    [Fact]
    public void Apply_RadiusZero_Identity()
    {
        var img = new ImageData(4, 4, 3);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                img.SetPixel(y, x, 0, 0.2f);
                img.SetPixel(y, x, 1, 0.5f);
                img.SetPixel(y, x, 2, 0.8f);
            }
        }

        var result = BilateralFilter.Apply(img, 0, 3.0f, 0.1f);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(0.2f, result.GetPixel(y, x, 0), 1e-5f);
                Assert.Equal(0.5f, result.GetPixel(y, x, 1), 1e-5f);
                Assert.Equal(0.8f, result.GetPixel(y, x, 2), 1e-5f);
            }
        }
    }

    [Fact]
    public void Apply_DimensionsPreserved()
    {
        var img = new ImageData(10, 8, 3);
        var result = BilateralFilter.Apply(img, 2, 3.0f, 0.1f);

        Assert.Equal(10, result.Width);
        Assert.Equal(8, result.Height);
        Assert.Equal(3, result.Channels);
    }

    [Fact]
    public void Apply_LargeSigmaR_BlursImage()
    {
        var img = new ImageData(5, 5, 3);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                img.SetPixel(y, x, 0, x < 2 ? 0.0f : 1.0f);
                img.SetPixel(y, x, 1, 0.5f);
                img.SetPixel(y, x, 2, 0.5f);
            }
        }

        var result = BilateralFilter.Apply(img, 2, 3.0f, 1000.0f);

        // 大 sigma_r 下，边缘应被平滑
        var leftPixel = result.GetPixel(2, 0, 0);
        var rightPixel = result.GetPixel(2, 4, 0);
        Assert.True(Math.Abs(leftPixel - rightPixel) < Math.Abs(0.0f - 1.0f),
            "大 sigma_r 下左右像素差值应减小（平滑效果）");
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "测试中使用 Random 产生测试数据，无安全性需求")]
    public void Apply_ParallelConsistency()
    {
        var img = new ImageData(8, 6, 3);
        var rng = new Random(42);
        for (var i = 0; i < img.Data.Length; i++)
        {
            img.Data[i] = (float)rng.NextDouble();
        }

        var result1 = BilateralFilter.Apply(img, 2, 3.0f, 0.1f, 1);
        var resultN = BilateralFilter.Apply(img, 2, 3.0f, 0.1f, 4);

        for (var i = 0; i < img.Data.Length; i++)
        {
            Assert.Equal(result1.Data[i], resultN.Data[i], 1e-5f);
        }
    }

    [Fact]
    public void Apply_GrayscaleImage_PreservesSingleChannel()
    {
        var gray = new ImageData(4, 4, 1);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                gray.SetPixel(y, x, 0, (float)(x + y) / 6f);
            }
        }

        var result = BilateralFilter.Apply(gray, 2, 3.0f, 0.1f);

        Assert.Equal(1, result.Channels);
        Assert.Equal(4, result.Width);
        Assert.Equal(4, result.Height);
    }

    [Fact]
    public void Apply_SigmaDZero_ReturnsIdentity()
    {
        var img = new ImageData(4, 4, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;

        var result = BilateralFilter.Apply(img, 2, 0.0f, 0.1f);

        for (var i = 0; i < img.Data.Length; i++)
        {
            Assert.Equal(img.Data[i], result.Data[i], 1e-6f);
        }
    }

    [Fact]
    public void Apply_SigmaRZero_ReturnsIdentity()
    {
        var img = new ImageData(4, 4, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;

        var result = BilateralFilter.Apply(img, 2, 3.0f, 0.0f);

        for (var i = 0; i < img.Data.Length; i++)
        {
            Assert.Equal(img.Data[i], result.Data[i], 1e-6f);
        }
    }

    [Fact]
    public void Constructor_ZeroDimensions_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(0, 0, 3));
        Assert.Contains("宽度", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_GrayscaleImage_ActuallySmooths()
    {
        var gray = new ImageData(7, 7, 1);
        for (var y = 0; y < 7; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                gray.SetPixel(y, x, 0, x < 3 ? 0.0f : 1.0f);
            }
        }

        var result = BilateralFilter.Apply(gray, 3, 3.0f, 1000.0f);

        // 大 sigma_r 下灰度双边滤波应产生平滑效果，原始 0/1 值会偏离极端
        var left = result.GetPixel(3, 1, 0);
        var right = result.GetPixel(3, 5, 0);
        Assert.True(left > 0.0f, $"左侧像素应 > 0: got {left:F4}");
        Assert.True(right < 1.0f, $"右侧像素应 < 1: got {right:F4}");
        Assert.True(left < right, $"左侧像素应 < 右侧像素: left={left:F4} right={right:F4}");
    }
}
