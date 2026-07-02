// ============================================================================
// 文件名:   BilateralFilter.cs
// 功能描述:  实现双边滤波算法（Bilateral Filter）。
//           对彩色图像在 CIELab 色彩空间中进行保边平滑，
//           支持行级并行处理（Parallel.For）。
//
// 核心算法:
//   1. 预计算空间域高斯核（Gaussian domain kernel）
//   2. 彩色路径: RGB → CIELab → 在 Lab 空间中滤波（sigma_r × 100）
//      → 转换回 RGB
//   3. 灰度路径: 直接在单通道上滤波
//   4. 对每个像素 (i, j):
//      提取以它为中心的 (2w+1)×(2w+1) 邻域
//      计算组合权重 = 空间权重 × 颜色权重
//      加权平均计算输出像素
//
// 与 MATLAB 原版一致:
//   - sigma_r 在 CIELab 空间放大 100 倍（匹配 L 通道 [0,100] 范围）
//   - 边界像素使用截断邻域（clamp 到图像边界内）
//
// 依赖关系:  依赖 LabColor 进行色域转换
// ============================================================================

using static System.MathF;

namespace ImageCartoonlization;

/// <summary>
/// 双边滤波处理器。
/// </summary>
public static class BilateralFilter
{
    /// <summary>
    /// 对彩色或灰度图像执行双边滤波。
    /// 彩色图像（3 通道）在 CIELab 空间中滤波。
    /// 灰度图像（1 通道）直接在原空间滤波。
    /// </summary>
    /// <param name="img">输入图像，像素值范围 [0, 1]</param>
    /// <param name="w">窗口半径（窗口大小 = 2w + 1）</param>
    /// <param name="sigmaD">空间域标准差</param>
    /// <param name="sigmaR">颜色域标准差（CIELab 空间会被放大 100 倍）</param>
    /// <param name="workers">并行线程数，0 表示自动（CPU 核数）</param>
    /// <returns>滤波后的图像</returns>
    public static ImageData Apply(ImageData img, int w, float sigmaD, float sigmaR, int workers = 0)
    {
        if (workers <= 0)
        {
            workers = Environment.ProcessorCount;
        }

        if (img.Width == 0 || img.Height == 0)
        {
            return img;
        }

        if (img.Channels == 1)
        {
            return BilatGray(img, w, sigmaD, sigmaR, workers);
        }

        // 彩色路径: RGB → Lab → 双边滤波 → RGB
        var lab = LabColor.RgbToLab(img);
        var labFiltered = BilatColor(lab, w, sigmaD, sigmaR, workers);
        return LabColor.LabToRgb(labFiltered);
    }

    /// <summary>
    /// 对 CIELab 彩色图像执行双边滤波（行级并行）。
    /// </summary>
    private static ImageData BilatColor(ImageData img, int w, float sigmaD, float sigmaR, int workers)
    {
        var height = img.Height;
        var width = img.Width;
        var kernelSize = 2 * w + 1;

        // 预计算空间域高斯核
        var domainKernel = BuildDomainKernel(kernelSize, w, sigmaD);

        // 与 MATLAB 一致：在 CIELab 空间放大 σ_r
        sigmaR *= 100f;

        var result = new ImageData(width, height, 3);
        var sigmaRSq = 2f * sigmaR * sigmaR;

        var rowsPerWorker = (height + workers - 1) / workers;

        Parallel.For(0, workers, wk =>
        {
            var startRow = wk * rowsPerWorker;
            var endRow = Math.Min(startRow + rowsPerWorker, height);
            if (startRow >= height) return;

            for (var y = startRow; y < endRow; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var yMin = Math.Max(y - w, 0);
                    var yMax = Math.Min(y + w, height - 1);
                    var xMin = Math.Max(x - w, 0);
                    var xMax = Math.Min(x + w, width - 1);

                    var curL = img.GetPixel(y, x, 0);
                    var curA = img.GetPixel(y, x, 1);
                    var curB = img.GetPixel(y, x, 2);

                    var sumL = 0f;
                    var sumA = 0f;
                    var sumB = 0f;
                    var sumW = 0f;

                    for (var py = yMin; py <= yMax; py++)
                    {
                        for (var px = xMin; px <= xMax; px++)
                        {
                            var dL = img.GetPixel(py, px, 0) - curL;
                            var dA = img.GetPixel(py, px, 1) - curA;
                            var dB = img.GetPixel(py, px, 2) - curB;
                            var rangeDiff = dL * dL + dA * dA + dB * dB;
                            var rangeW = Exp(-rangeDiff / sigmaRSq);

                            var domainW = domainKernel[py - y + w][px - x + w];
                            var weight = rangeW * domainW;

                            sumL += weight * img.GetPixel(py, px, 0);
                            sumA += weight * img.GetPixel(py, px, 1);
                            sumB += weight * img.GetPixel(py, px, 2);
                            sumW += weight;
                        }
                    }

                    result.SetPixel(y, x, 0, sumL / sumW);
                    result.SetPixel(y, x, 1, sumA / sumW);
                    result.SetPixel(y, x, 2, sumB / sumW);
                }
            }
        });

        return result;
    }

    /// <summary>
    /// 对单通道灰度图像执行双边滤波（行级并行）。
    /// </summary>
    private static ImageData BilatGray(ImageData img, int w, float sigmaD, float sigmaR, int workers)
    {
        var height = img.Height;
        var width = img.Width;
        var kernelSize = 2 * w + 1;

        var domainKernel = BuildDomainKernel(kernelSize, w, sigmaD);
        var sigmaRSq = 2f * sigmaR * sigmaR;

        var result = new ImageData(width, height, 1);
        var rowsPerWorker = (height + workers - 1) / workers;

        Parallel.For(0, workers, wk =>
        {
            var startRow = wk * rowsPerWorker;
            var endRow = Math.Min(startRow + rowsPerWorker, height);
            if (startRow >= height) return;

            for (var y = startRow; y < endRow; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var yMin = Math.Max(y - w, 0);
                    var yMax = Math.Min(y + w, height - 1);
                    var xMin = Math.Max(x - w, 0);
                    var xMax = Math.Min(x + w, width - 1);

                    var curVal = img.GetPixel(y, x, 0);
                    var sumVal = 0f;
                    var sumW = 0f;

                    for (var py = yMin; py <= yMax; py++)
                    {
                        for (var px = xMin; px <= xMax; px++)
                        {
                            var diff = img.GetPixel(py, px, 0) - curVal;
                            var rangeW = Exp(-(diff * diff) / sigmaRSq);
                            var domainW = domainKernel[py - y + w][px - x + w];
                            var weight = rangeW * domainW;

                            sumVal += weight * img.GetPixel(py, px, 0);
                            sumW += weight;
                        }
                    }

                    result.SetPixel(y, x, 0, sumVal / sumW);
                }
            }
        });

        return result;
    }

    /// <summary>
    /// 预计算空间域高斯核。
    /// </summary>
    private static float[][] BuildDomainKernel(int size, int w, float sigmaD)
    {
        var sigmaDSq = 2f * sigmaD * sigmaD;
        var kernel = new float[size][];

        for (var dx = -w; dx <= w; dx++)
        {
            kernel[dx + w] = new float[size];
            for (var dy = -w; dy <= w; dy++)
            {
                kernel[dx + w][dy + w] = Exp(-(float)(dx * dx + dy * dy) / sigmaDSq);
            }
        }

        return kernel;
    }
}
