// ============================================================================
// 文件名:   EdgeDetection.cs
// 功能描述:  实现基于 Sobel 算子的边缘检测和边缘叠加（黑色描边）。
//
// 核心算法:
//   1. DetectEdges: 对灰度图进行 3×3 Sobel 卷积
//      水平核 Gx:  [-1, 0, 1]    垂直核 Gy:  [ 1, 2, 1]
//                  [-2, 0, 2]                 [ 0, 0, 0]
//                  [-1, 0, 1]                 [-1,-2,-1]
//      梯度幅值 G = sqrt(Gx² + Gy²)
//      当 G > threshold 时标记为边缘像素
//
//   2. OverlayEdges: 将边缘掩码叠加到模糊图像上
//      边缘像素 → 黑色 (0, 0, 0)
//      非边缘像素 → 保留原值并钳制到 [0, 1]
//
// 依赖关系:  无外部依赖
// ============================================================================

using static System.MathF;

namespace ImageCartoonlization;

/// <summary>
/// Sobel 边缘检测和黑色描边叠加。
/// </summary>
public static class EdgeDetection
{
    /// <summary>
    /// 使用 Sobel 算子检测灰度图中的边缘。
    /// 边界像素（四周各一行/列）因无法完整卷积，默认标记为非边缘。
    /// </summary>
    /// <param name="gray">单通道灰度图，像素值任意范围</param>
    /// <param name="threshold">边缘检测阈值，梯度幅值超过此值即标记为边缘</param>
    /// <returns>边缘掩码，边缘像素为 1.0，非边缘为 0.0</returns>
    public static ImageData DetectEdges(ImageData gray, float threshold)
    {
        var height = gray.Height;
        var width = gray.Width;
        var mask = new ImageData(width, height, 1);

        if (height < 3 || width < 3)
        {
            return mask;
        }

        var src = gray.Data;
        var dst = mask.Data;

        // 跳过边界一行/一列以避免越界
        for (var y = 1; y < height - 1; y++)
        {
            var rowBase = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var gx = 0f;
                var gy = 0f;

                var n00 = src[rowBase - width + x - 1];
                var n01 = src[rowBase - width + x];
                var n02 = src[rowBase - width + x + 1];
                var n10 = src[rowBase + x - 1];
                var n11 = src[rowBase + x];
                var n12 = src[rowBase + x + 1];
                var n20 = src[rowBase + width + x - 1];
                var n21 = src[rowBase + width + x];
                var n22 = src[rowBase + width + x + 1];

                // Sobel X: [-1,0,1; -2,0,2; -1,0,1]
                gx = -n00 + n02 - 2f * n10 + 2f * n12 - n20 + n22;

                // Sobel Y: [1,2,1; 0,0,0; -1,-2,-1]
                gy = n00 + 2f * n01 + n02 - n20 - 2f * n21 - n22;

                var g = Sqrt(gx * gx + gy * gy);
                if (g > threshold)
                {
                    dst[rowBase + x] = 1.0f;
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// 将边缘掩码叠加到模糊处理后的图像上。
    /// 边缘像素变为黑色 (0, 0, 0)，非边缘像素保留模糊值并钳制到 [0, 1]。
    /// 与 MATLAB 中 img_blur - img_blur .* edge_mask 完全一致。
    /// </summary>
    /// <param name="blurred">模糊处理后的 RGB 图像</param>
    /// <param name="edgeMask">边缘掩码，边缘像素 > 0.5 视为边缘</param>
    /// <returns>叠加边缘后的 RGB 图像</returns>
    public static ImageData OverlayEdges(ImageData blurred, ImageData edgeMask)
    {
        if (blurred.Channels != 3)
        {
            throw new ArgumentException(
                $"OverlayEdges 要求 3 通道 RGB 输入，实际通道数为 {blurred.Channels}",
                nameof(blurred));
        }
        if (edgeMask.Channels != 1)
        {
            throw new ArgumentException(
                $"OverlayEdges 要求 1 通道掩码输入，实际通道数为 {edgeMask.Channels}",
                nameof(edgeMask));
        }
        if (edgeMask.Width != blurred.Width || edgeMask.Height != blurred.Height)
        {
            throw new ArgumentException(
                $"边缘掩码尺寸 ({edgeMask.Width}×{edgeMask.Height}) 与输入图像 ({blurred.Width}×{blurred.Height}) 不匹配",
                nameof(edgeMask));
        }

        var height = blurred.Height;
        var width = blurred.Width;
        var result = new ImageData(width, height, 3);
        var src = blurred.Data;
        var dst = result.Data;
        var mask = edgeMask.Data;

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = rowBase + x * 3;
                if (mask[y * width + x] > 0.5f)
                {
                    dst[idx] = 0.0f;
                    dst[idx + 1] = 0.0f;
                    dst[idx + 2] = 0.0f;
                }
                else
                {
                    dst[idx] = Math.Clamp(src[idx], 0f, 1f);
                    dst[idx + 1] = Math.Clamp(src[idx + 1], 0f, 1f);
                    dst[idx + 2] = Math.Clamp(src[idx + 2], 0f, 1f);
                }
            }
        }
        return result;
    }
}
