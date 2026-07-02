// ============================================================================
// 文件名:   Saturation.cs
// 功能描述:  实现饱和度调整算法。
//           使用线性内插/外推公式在颜色和灰度之间进行变换。
//
// 核心公式:
//   result = (1 - scalar) * gray + scalar * color
//
//   其中 gray = 0.299*R + 0.587*G + 0.114*B（ITU-R BT.601 亮度系数）。
//   当 scalar = 1.0 时，输出与输入一致（不变）；
//   当 scalar > 1.0 时，颜色向外推远离灰度，饱和度增强；
//   当 scalar < 1.0 时，颜色向灰度收拢，饱和度降低。
//
// 依赖关系:  无外部依赖
// ============================================================================

namespace ImageCartoonlization;

/// <summary>
/// 饱和度调整工具，对图像的色彩饱和度进行线性变换。
/// </summary>
public static class Saturation
{
    /// <summary>
    /// 调整图像的饱和度。
    /// </summary>
    /// <param name="img">输入的 RGB 图像数据，像素值范围 [0, 1]</param>
    /// <param name="scalar">饱和度增益系数。1.0=不变，>1=增强，<1=降低</param>
    /// <returns>调整后的 RGB 图像数据，值域 [0, 1]</returns>
    public static ImageData AdjustSaturation(ImageData img, float scalar)
    {
        if (img.Channels != 3)
        {
            throw new ArgumentException(
                $"AdjustSaturation 要求 3 通道 RGB 输入，实际通道数为 {img.Channels}",
                nameof(img));
        }

        var height = img.Height;
        var width = img.Width;
        var result = new ImageData(width, height, 3);
        var src = img.Data;
        var dst = result.Data;

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = rowBase + x * 3;
                var r = src[idx];
                var g = src[idx + 1];
                var b = src[idx + 2];

                // 计算灰度值（ITU-R BT.601 系数）
                var gray = 0.299f * r + 0.587f * g + 0.114f * b;

                // 线性插值/外推并钳制
                dst[idx] = Math.Clamp((1f - scalar) * gray + scalar * r, 0f, 1f);
                dst[idx + 1] = Math.Clamp((1f - scalar) * gray + scalar * g, 0f, 1f);
                dst[idx + 2] = Math.Clamp((1f - scalar) * gray + scalar * b, 0f, 1f);
            }
        }
        return result;
    }
}
