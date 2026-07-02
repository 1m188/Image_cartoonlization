// ============================================================================
// 文件名:   ImageData.cs
// 功能描述:  定义图像数据的核心数据结构 ImageData，
//           采用一维 float[] 数组存储像素值，通道交错排列（RGBRGB...）。
//           像素值归一化到 [0.0, 1.0] 范围。
//
// 数据布局:
//   单个像素 (x, y) 的通道 c 位于索引:
//     index = (y * width + x) * channels + c
//
//   其中 width 为图像宽度（像素数），channels 为通道数（1=灰度, 3=RGB）。
//
// 依赖关系:  无外部依赖
// ============================================================================

using System.Diagnostics.CodeAnalysis;

namespace ImageCartoonlization;

/// <summary>
/// 图像数据结构体，内部使用一维 float 数组以通道交错方式存储像素值。
/// </summary>
[SuppressMessage("Design", "CA1819:Properties should not return arrays",
    Justification = "Data 属性需要在管线中直接访问以进行高性能逐像素操作，返回数组引用是设计选择")]
public struct ImageData : IEquatable<ImageData>
{
    /// <summary>图像宽度（像素数）</summary>
    public int Width { get; }

    /// <summary>图像高度（像素数）</summary>
    public int Height { get; }

    /// <summary>通道数（1 为灰度，3 为 RGB）</summary>
    public int Channels { get; }

    /// <summary>
    /// 像素数据，一维 float 数组，长度 = width * height * channels。
    /// 像素值范围 [0.0, 1.0]。
    /// </summary>
    public float[] Data { get; }

    /// <summary>
    /// 构造指定尺寸和通道数的 ImageData，所有像素初始化为 0。
    /// </summary>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="channels">通道数</param>
    public ImageData(int width, int height, int channels)
    {
        Width = width;
        Height = height;
        Channels = channels;
        Data = new float[width * height * channels];
    }

    /// <summary>
    /// 构造指定尺寸和通道数的 ImageData，使用提供的数组作为数据。
    /// </summary>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="channels">通道数</param>
    /// <param name="data">像素数据数组</param>
    public ImageData(int width, int height, int channels, float[] data)
    {
        Width = width;
        Height = height;
        Channels = channels;
        Data = data;
    }

    /// <summary>
    /// 获取指定位置的像素通道值。
    /// </summary>
    /// <param name="y">行索引（0 起始）</param>
    /// <param name="x">列索引（0 起始）</param>
    /// <param name="c">通道索引（0 起始）</param>
    /// <returns>像素通道值</returns>
    public readonly float GetPixel(int y, int x, int c)
    {
        return Data[(y * Width + x) * Channels + c];
    }

    /// <summary>
    /// 设置指定位置的像素通道值。
    /// </summary>
    /// <param name="y">行索引（0 起始）</param>
    /// <param name="x">列索引（0 起始）</param>
    /// <param name="c">通道索引（0 起始）</param>
    /// <param name="value">要设置的像素值</param>
    public void SetPixel(int y, int x, int c, float value)
    {
        Data[(y * Width + x) * Channels + c] = value;
    }

    /// <summary>
    /// 获取总像素数。
    /// </summary>
    public readonly int PixelCount => Width * Height;

    /// <summary>
    /// 获取数据数组的总长度。
    /// </summary>
    public readonly int DataLength => Width * Height * Channels;

    /// <inheritdoc />
    public readonly bool Equals(ImageData other)
    {
        return Width == other.Width &&
               Height == other.Height &&
               Channels == other.Channels &&
               Data.AsSpan().SequenceEqual(other.Data.AsSpan());
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj)
    {
        return obj is ImageData other && Equals(other);
    }

    /// <inheritdoc />
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Width, Height, Channels);
    }

    public static bool operator ==(ImageData left, ImageData right) => left.Equals(right);
    public static bool operator !=(ImageData left, ImageData right) => !left.Equals(right);
}
