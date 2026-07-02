// ============================================================================
// 文件名:   ImageDataTests.cs
// 功能描述:  ImageData 结构体的单元测试。
//           测试构造、索引计算正确性和边界处理。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class ImageDataTests
{
    [Fact]
    public void Constructor_CreatesCorrectDimensions()
    {
        var img = new ImageData(10, 20, 3);

        Assert.Equal(10, img.Width);
        Assert.Equal(20, img.Height);
        Assert.Equal(3, img.Channels);
    }

    [Fact]
    public void Constructor_InitializesDataToZero()
    {
        var img = new ImageData(5, 5, 3);

        foreach (var v in img.Data)
        {
            Assert.Equal(0f, v);
        }
    }

    [Fact]
    public void Constructor_AllocatesCorrectDataLength()
    {
        var img = new ImageData(10, 20, 3);

        Assert.Equal(10 * 20 * 3, img.Data.Length);
        Assert.Equal(10 * 20 * 3, img.DataLength);
    }

    [Fact]
    public void GetPixel_ReturnsCorrectValue()
    {
        var img = new ImageData(3, 3, 3);
        img.SetPixel(1, 2, 0, 0.5f);
        img.SetPixel(1, 2, 1, 0.6f);
        img.SetPixel(1, 2, 2, 0.7f);

        Assert.Equal(0.5f, img.GetPixel(1, 2, 0));
        Assert.Equal(0.6f, img.GetPixel(1, 2, 1));
        Assert.Equal(0.7f, img.GetPixel(1, 2, 2));
    }

    [Fact]
    public void SetPixel_ModifiesCorrectPosition()
    {
        var img = new ImageData(3, 3, 3);
        img.SetPixel(0, 0, 0, 0.1f);
        img.SetPixel(0, 0, 1, 0.2f);
        img.SetPixel(0, 0, 2, 0.3f);
        img.SetPixel(2, 2, 0, 0.9f);

        Assert.Equal(0.1f, img.GetPixel(0, 0, 0));
        Assert.Equal(0.2f, img.GetPixel(0, 0, 1));
        Assert.Equal(0.3f, img.GetPixel(0, 0, 2));
        Assert.Equal(0.9f, img.GetPixel(2, 2, 0));
    }

    [Fact]
    public void GetPixel_SetPixel_RoundTrip()
    {
        var img = new ImageData(4, 4, 3);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var val = (y * 4 + x) * 3 + c;
                    img.SetPixel(y, x, c, val);
                }
            }
        }

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var expected = (y * 4 + x) * 3 + c;
                    Assert.Equal(expected, img.GetPixel(y, x, c));
                }
            }
        }
    }

    [Fact]
    public void PixelCount_ReturnsCorrectValue()
    {
        var img = new ImageData(10, 20, 3);

        Assert.Equal(200, img.PixelCount);
    }

    [Fact]
    public void Constructor_WithExternalData_UsesProvidedArray()
    {
        var data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f };
        var img = new ImageData(2, 1, 3, data);

        Assert.Equal(data, img.Data);
        Assert.Equal(1f, img.GetPixel(0, 0, 0));
        Assert.Equal(6f, img.GetPixel(0, 1, 2));
    }

    [Fact]
    public void Constructor_ZeroChannels_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(10, 10, 0));
        Assert.Contains("通道数", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_NegativeChannels_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(10, 10, -1));
        Assert.Contains("通道数", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ZeroChannels_WithExternalArray_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(10, 10, 0, Array.Empty<float>()));
    }

    [Fact]
    public void Constructor_ZeroWidth_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(0, 10, 3));
        Assert.Contains("宽度", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ZeroHeight_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(10, 0, 3));
        Assert.Contains("高度", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_NegativeWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(-1, 10, 3));
    }

    [Fact]
    public void Constructor_NegativeHeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageData(10, -1, 3));
    }

    [Fact]
    public void GetPixel_OutOfBounds_Y_Throws()
    {
        var img = new ImageData(3, 3, 3);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            img.GetPixel(3, 0, 0));
        Assert.Contains("越界", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPixel_OutOfBounds_X_Throws()
    {
        var img = new ImageData(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            img.GetPixel(0, 3, 0));
    }

    [Fact]
    public void GetPixel_OutOfBounds_Channel_Throws()
    {
        var img = new ImageData(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            img.GetPixel(0, 0, 3));
    }

    [Fact]
    public void SetPixel_OutOfBounds_Throws()
    {
        var img = new ImageData(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            img.SetPixel(3, 0, 0, 0.5f));
    }

    [Fact]
    public void GetPixel_DefaultStruct_NullData_Throws()
    {
        var img = default(ImageData);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            img.GetPixel(0, 0, 0));
        Assert.Contains("未初始化", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetPixel_DefaultStruct_NullData_Throws()
    {
        var img = default(ImageData);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            img.SetPixel(0, 0, 0, 0.5f));
        Assert.Contains("未初始化", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
