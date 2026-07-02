// ============================================================================
// 文件名:   LabColorTests.cs
// 功能描述:  LabColor 模块的单元测试。
//           测试 sRGB ↔ CIELab 双向转换、RGB 转灰度。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class LabColorTests
{
    [Fact]
    public void RgbToGray_White_ReturnsOne()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 1.0f);
        img.SetPixel(0, 0, 1, 1.0f);
        img.SetPixel(0, 0, 2, 1.0f);

        var gray = LabColor.RgbToGray(img);

        Assert.Equal(1, gray.Width);
        Assert.Equal(1, gray.Height);
        Assert.Equal(1, gray.Channels);
        Assert.Equal(1.0f, gray.GetPixel(0, 0, 0), 1e-6f);
    }

    [Fact]
    public void RgbToGray_Black_ReturnsZero()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.0f);
        img.SetPixel(0, 0, 1, 0.0f);
        img.SetPixel(0, 0, 2, 0.0f);

        var gray = LabColor.RgbToGray(img);

        Assert.Equal(0.0f, gray.GetPixel(0, 0, 0), 1e-6f);
    }

    [Fact]
    public void RgbToGray_EqualChannels_ReturnsSameValue()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.5f);
        img.SetPixel(0, 0, 1, 0.5f);
        img.SetPixel(0, 0, 2, 0.5f);

        var gray = LabColor.RgbToGray(img);

        Assert.Equal(0.5f, gray.GetPixel(0, 0, 0), 1e-5f);
    }

    [Fact]
    public void RgbToLab_White_MapsToCorrectLab()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 1.0f);
        img.SetPixel(0, 0, 1, 1.0f);
        img.SetPixel(0, 0, 2, 1.0f);

        var lab = LabColor.RgbToLab(img);

        Assert.Equal(100.0f, lab.GetPixel(0, 0, 0), 0.01f);
        Assert.Equal(0.0f, lab.GetPixel(0, 0, 1), 0.01f);
        Assert.Equal(0.0f, lab.GetPixel(0, 0, 2), 0.01f);
    }

    [Fact]
    public void RgbToLab_Black_MapsToCorrectLab()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.0f);
        img.SetPixel(0, 0, 1, 0.0f);
        img.SetPixel(0, 0, 2, 0.0f);

        var lab = LabColor.RgbToLab(img);

        Assert.Equal(0.0f, lab.GetPixel(0, 0, 0), 0.01f);
        Assert.Equal(0.0f, lab.GetPixel(0, 0, 1), 0.01f);
        Assert.Equal(0.0f, lab.GetPixel(0, 0, 2), 0.01f);
    }

    [Fact]
    public void RgbToLab_Red_HasPositiveA()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 1.0f);
        img.SetPixel(0, 0, 1, 0.0f);
        img.SetPixel(0, 0, 2, 0.0f);

        var lab = LabColor.RgbToLab(img);

        Assert.True(lab.GetPixel(0, 0, 1) > 0, "红色应产生正的 a* 值");
    }

    [Fact]
    public void RgbToLab_LabToRgb_NeutralGray_RoundTrip()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 0.5f);
        img.SetPixel(0, 0, 1, 0.5f);
        img.SetPixel(0, 0, 2, 0.5f);

        var lab = LabColor.RgbToLab(img);
        var result = LabColor.LabToRgb(lab);

        Assert.Equal(0.5f, result.GetPixel(0, 0, 0), 1e-5f);
        Assert.Equal(0.5f, result.GetPixel(0, 0, 1), 1e-5f);
        Assert.Equal(0.5f, result.GetPixel(0, 0, 2), 1e-5f);
    }

    [Fact]
    public void RgbToLab_LabToRgb_PureRed_RoundTrip()
    {
        var img = new ImageData(1, 1, 3);
        img.SetPixel(0, 0, 0, 1.0f);
        img.SetPixel(0, 0, 1, 0.0f);
        img.SetPixel(0, 0, 2, 0.0f);

        var lab = LabColor.RgbToLab(img);
        var result = LabColor.LabToRgb(lab);

        Assert.Equal(1.0f, result.GetPixel(0, 0, 0), 1e-4f);
        Assert.Equal(0.0f, result.GetPixel(0, 0, 1), 1e-4f);
        Assert.Equal(0.0f, result.GetPixel(0, 0, 2), 1e-4f);
    }

    [Fact]
    public void RgbToLab_LabToRgb_ExcessiveLabClamped()
    {
        var labImg = new ImageData(1, 1, 3);
        labImg.SetPixel(0, 0, 0, 200.0f);
        labImg.SetPixel(0, 0, 1, 0.0f);
        labImg.SetPixel(0, 0, 2, 0.0f);

        var result = LabColor.LabToRgb(labImg);

        Assert.InRange(result.GetPixel(0, 0, 0), 0.0f, 1.0f);
        Assert.InRange(result.GetPixel(0, 0, 1), 0.0f, 1.0f);
        Assert.InRange(result.GetPixel(0, 0, 2), 0.0f, 1.0f);
    }
}
