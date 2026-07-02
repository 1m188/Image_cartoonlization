// ============================================================================
// 文件名:   EdgeDetectionTests.cs
// 功能描述:  EdgeDetection 模块的单元测试。
//           测试 Sobel 边缘检测和边缘叠加。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class EdgeDetectionTests
{
    [Fact]
    public void DetectEdges_UniformImage_NoEdges()
    {
        var gray = new ImageData(5, 5, 1);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                gray.SetPixel(y, x, 0, 0.5f);
            }
        }

        var mask = EdgeDetection.DetectEdges(gray, 0.02f);

        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(0.0f, mask.GetPixel(y, x, 0));
            }
        }
    }

    [Fact]
    public void DetectEdges_GradientBoundary_EdgesAtBoundary()
    {
        var gray = new ImageData(6, 6, 1);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                gray.SetPixel(y, x, 0, x < 3 ? 0.0f : 1.0f);
            }
        }

        var mask = EdgeDetection.DetectEdges(gray, 0.1f);

        var edgeCount = CountEdges(mask);
        Assert.True(edgeCount > 0, "边界处应检测到边缘");
    }

    [Fact]
    public void DetectEdges_LowerThreshold_MoreEdges()
    {
        var gray = new ImageData(6, 6, 1);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                gray.SetPixel(y, x, 0, x < 3 ? 0.0f : 1.0f);
            }
        }

        var maskHigh = EdgeDetection.DetectEdges(gray, 0.5f);
        var maskLow = EdgeDetection.DetectEdges(gray, 0.01f);

        var highCount = CountEdges(maskHigh);
        var lowCount = CountEdges(maskLow);

        Assert.True(lowCount >= highCount,
            $"低阈值边缘数 ({lowCount}) 应 >= 高阈值 ({highCount})");
    }

    [Fact]
    public void DetectEdges_DimensionsPreserved()
    {
        var gray = new ImageData(10, 8, 1);
        var mask = EdgeDetection.DetectEdges(gray, 0.02f);

        Assert.Equal(10, mask.Width);
        Assert.Equal(8, mask.Height);
        Assert.Equal(1, mask.Channels);
    }

    [Fact]
    public void OverlayEdges_AllOneMask_BlackImage()
    {
        var img = new ImageData(3, 3, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;
        var mask = new ImageData(3, 3, 1);
        for (var i = 0; i < mask.Data.Length; i++) mask.Data[i] = 1.0f;

        var result = EdgeDetection.OverlayEdges(img, mask);

        for (var i = 0; i < result.Data.Length; i++)
        {
            Assert.Equal(0.0f, result.Data[i]);
        }
    }

    [Fact]
    public void OverlayEdges_AllZeroMask_Identity()
    {
        var img = new ImageData(3, 3, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;
        var mask = new ImageData(3, 3, 1);

        var result = EdgeDetection.OverlayEdges(img, mask);

        for (var i = 0; i < result.Data.Length; i++)
        {
            Assert.Equal(0.5f, result.Data[i], 1e-6f);
        }
    }

    [Fact]
    public void OverlayEdges_MixedMask_CorrectComposite()
    {
        var img = new ImageData(2, 2, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.7f;
        var mask = new ImageData(2, 2, 1);
        mask.SetPixel(0, 0, 0, 1.0f); // 边缘
        // 其他像素为非边缘 (0.0)

        var result = EdgeDetection.OverlayEdges(img, mask);

        // 边缘像素 → 黑色
        Assert.Equal(0.0f, result.GetPixel(0, 0, 0));
        Assert.Equal(0.0f, result.GetPixel(0, 0, 1));
        Assert.Equal(0.0f, result.GetPixel(0, 0, 2));

        // 非边缘像素 → 保留原值
        Assert.Equal(0.7f, result.GetPixel(0, 1, 0), 1e-6f);
        Assert.Equal(0.7f, result.GetPixel(0, 1, 1), 1e-6f);
        Assert.Equal(0.7f, result.GetPixel(0, 1, 2), 1e-6f);
    }

    [Fact]
    public void OverlayEdges_DimensionsPreserved()
    {
        var img = new ImageData(5, 4, 3);
        var mask = new ImageData(5, 4, 1);
        var result = EdgeDetection.OverlayEdges(img, mask);

        Assert.Equal(5, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(3, result.Channels);
    }

    private static int CountEdges(ImageData mask)
    {
        var count = 0;
        for (var i = 0; i < mask.Data.Length; i++)
        {
            if (mask.Data[i] > 0.5f) count++;
        }
        return count;
    }
}
