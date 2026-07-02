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

        // 3×3 Sobel 核
        var sobelX = new float[][] {
            [-1, 0, 1],
            [-2, 0, 2],
            [-1, 0, 1]
        };
        var sobelY = new float[][] {
            [1, 2, 1],
            [0, 0, 0],
            [-1, -2, -1]
        };

        // 跳过边界一行/一列以避免越界
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var gx = 0f;
                var gy = 0f;

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var val = gray.GetPixel(y + dy, x + dx, 0);
                        gx += val * sobelX[dy + 1][dx + 1];
                        gy += val * sobelY[dy + 1][dx + 1];
                    }
                }

                var g = Sqrt(gx * gx + gy * gy);
                if (g > threshold)
                {
                    mask.SetPixel(y, x, 0, 1.0f);
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
        var height = blurred.Height;
        var width = blurred.Width;
        var result = new ImageData(width, height, 3);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (edgeMask.GetPixel(y, x, 0) > 0.5f)
                {
                    result.SetPixel(y, x, 0, 0.0f);
                    result.SetPixel(y, x, 1, 0.0f);
                    result.SetPixel(y, x, 2, 0.0f);
                }
                else
                {
                    result.SetPixel(y, x, 0, Clamp01(blurred.GetPixel(y, x, 0)));
                    result.SetPixel(y, x, 1, Clamp01(blurred.GetPixel(y, x, 1)));
                    result.SetPixel(y, x, 2, Clamp01(blurred.GetPixel(y, x, 2)));
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 将值钳制到 [0.0, 1.0] 范围内。
    /// </summary>
    private static float Clamp01(float v)
    {
        if (v < 0) return 0;
        if (v > 1) return 1;
        return v;
    }
}
