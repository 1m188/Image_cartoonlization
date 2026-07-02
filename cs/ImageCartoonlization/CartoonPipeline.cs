// ============================================================================
// 文件名:   CartoonPipeline.cs
// 功能描述:  卡通化管线编排器，串联所有处理步骤。
//           包含参数定义（Params 记录类型）和 Cartoonize() 主函数。
//
// 处理流程:
//   1. 饱和度调整
//   2. 双边滤波 × N（首次滤波后进行边缘检测）
//   3. RGB 转灰度
//   4. Sobel 边缘检测
//   5. 黑色描边叠加
//   6. 最终值域钳制到 [0, 1]
//   每一步记录耗时和详情信息。
//
// 依赖关系:  依赖 Saturation、BilateralFilter、LabColor、EdgeDetection
// ============================================================================

using System.Diagnostics;

namespace ImageCartoonlization;

/// <summary>
/// 卡通化处理的参数定义。
/// </summary>
public record Params
{
    /// <summary>边缘检测阈值（默认 0.02，范围 0.0~1.0，越小边缘越多）</summary>
    public float EdgeThresh { get; init; } = 0.02f;

    /// <summary>饱和度增益（默认 2.0，1.0=不变，>1 增强，<1 减弱）</summary>
    public float SatScalar { get; init; } = 2.0f;

    /// <summary>双边滤波窗口半径（默认 10，窗口大小 = 2*Radius+1）</summary>
    public int Radius { get; init; } = 10;

    /// <summary>空间域标准差 σ_d（默认 3.0，越大空间越模糊）</summary>
    public float SigmaD { get; init; } = 3.0f;

    /// <summary>颜色域标准差 σ_r（默认 0.1，越大颜色越模糊）</summary>
    public float SigmaR { get; init; } = 0.1f;

    /// <summary>双边滤波迭代次数（默认 1，越大越模糊）</summary>
    public int LoopNum { get; init; } = 1;

    /// <summary>并行线程数（0 表示使用 CPU 核数）</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily",
        Justification = "workers=0 表示自动检测，显式标注有语义价值")]
    public int Workers { get; init; } = 0;

    /// <summary>返回一组与 MATLAB 原版一致的默认参数。</summary>
    public static Params Default => new();
}

/// <summary>
/// 单个处理步骤的执行结果。
/// </summary>
public record StepResult
{
    /// <summary>步骤名称</summary>
    public string Name { get; init; } = "";

    /// <summary>步骤耗时</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>附加详情信息</summary>
    public string Detail { get; init; } = "";
}

/// <summary>
/// 卡通化管线编排器。
/// </summary>
public static class CartoonPipeline
{
    /// <summary>
    /// 对输入图像执行完整的卡通化管线处理。
    /// </summary>
    /// <param name="img">输入的 RGB 图像数据</param>
    /// <param name="p">处理参数</param>
    /// <returns>处理后的图像和各步骤执行信息</returns>
    public static (ImageData Image, List<StepResult> Steps) Cartoonize(
        ImageData img, Params p)
    {
        ArgumentNullException.ThrowIfNull(p);

        var steps = new List<StepResult>();
        var totalSw = Stopwatch.StartNew();

        // ── 步骤 1：饱和度调整 ─────────────────────────────
        var t0 = Stopwatch.StartNew();
        img = Saturation.AdjustSaturation(img, p.SatScalar);
        steps.Add(new StepResult
        {
            Name = "饱和度调整",
            Duration = t0.Elapsed,
            Detail = $"s={p.SatScalar:F2}"
        });

        // ── 步骤 2：第一次双边滤波 ─────────────────────────
        var t1 = Stopwatch.StartNew();
        img = BilateralFilter.Apply(img, p.Radius, p.SigmaD, p.SigmaR, p.Workers);
        steps.Add(new StepResult
        {
            Name = "双边滤波 #1",
            Duration = t1.Elapsed,
            Detail = $"σ_d={p.SigmaD:F2} σ_r={p.SigmaR:F2} radius={p.Radius} workers={p.Workers}"
        });

        // ── 步骤 3：边缘检测 ───────────────────────────────
        var t2 = Stopwatch.StartNew();
        var gray = LabColor.RgbToGray(img);
        var edgeMask = EdgeDetection.DetectEdges(gray, p.EdgeThresh);

        var edgeCount = 0;
        var totalPixels = edgeMask.PixelCount;
        var maskData = edgeMask.Data;
        for (var i = 0; i < maskData.Length; i++)
        {
            if (maskData[i] > 0.5f) edgeCount++;
        }
        var edgePct = (float)edgeCount / totalPixels * 100f;

        steps.Add(new StepResult
        {
            Name = "边缘检测",
            Duration = t2.Elapsed,
            Detail = $"sobel threshold={p.EdgeThresh:F4} edges={edgePct:F1}%"
        });

        // ── 步骤 4：额外双边滤波迭代 ───────────────────────
        for (var i = 2; i <= p.LoopNum; i++)
        {
            var t3 = Stopwatch.StartNew();
            img = BilateralFilter.Apply(img, p.Radius, p.SigmaD, p.SigmaR, p.Workers);
            steps.Add(new StepResult
            {
                Name = $"双边滤波 #{i}",
                Duration = t3.Elapsed,
                Detail = $"σ_d={p.SigmaD:F2} σ_r={p.SigmaR:F2} radius={p.Radius} workers={p.Workers}"
            });
        }

        // ── 步骤 5：边缘叠加 ───────────────────────────────
        var t4 = Stopwatch.StartNew();
        img = EdgeDetection.OverlayEdges(img, edgeMask);
        steps.Add(new StepResult
        {
            Name = "边缘叠加",
            Duration = t4.Elapsed,
            Detail = $"{edgeCount} 边缘像素已叠加"
        });

        // ── 步骤 6：最终钳制 ───────────────────────────────
        for (var i = 0; i < img.Data.Length; i++)
        {
            var v = img.Data[i];
            img.Data[i] = float.IsNaN(v) ? 0f : Math.Clamp(v, 0f, 1f);
        }

        steps.Add(new StepResult
        {
            Name = "总处理",
            Duration = totalSw.Elapsed
        });

        return (img, steps);
    }
}
