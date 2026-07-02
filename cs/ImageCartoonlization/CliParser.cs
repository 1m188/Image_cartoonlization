// ============================================================================
// 文件名:   CliParser.cs
// 功能描述:  命令行参数解析器，采用手动解析方式（零外部依赖，AOT 安全）。
//           参数名与 Go/Rust 版本完全一致。
//
// 支持的参数:
//   必选: -i <路径>
//   可选: -o <路径>、--edge-thresh、--sat、--radius、
//         --sigma-d、--sigma-r、--loop、--workers、
//         -v（详细输出）、-h/--help（帮助）
//
// 依赖关系:  无外部依赖
// ============================================================================

namespace ImageCartoonlization;

/// <summary>
/// CLI 参数解析结果。
/// </summary>
public class CliParseResult
{
    /// <summary>是否显示帮助信息</summary>
    public bool ShowHelp { get; init; }

    /// <summary>退出码（0=成功，非0=错误）</summary>
    public int ExitCode { get; init; }

    /// <summary>参数是否有效</summary>
    public bool IsValid { get; init; }

    /// <summary>错误信息（仅在无效时）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>输入图片路径</summary>
    public string? InputPath { get; init; }

    /// <summary>输出图片路径</summary>
    public string OutputPath { get; init; } = "cartoon.png";

    /// <summary>处理参数</summary>
    public Params Params { get; init; } = Params.Default;

    /// <summary>是否启用详细输出模式</summary>
    public bool Verbose { get; init; }
}

/// <summary>
/// 命令行参数解析器。
/// </summary>
public static class CliParser
{
    /// <summary>
    /// 解析命令行参数数组。
    /// </summary>
    /// <param name="args">命令行参数数组</param>
    /// <returns>解析结果</returns>
    public static CliParseResult ParseArgs(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // ── 帮助标志 ─────────────────────────────────────────
        // 仅在 -i 的值位置上包含 -h/--help 时不触发帮助模式
        var inputIdx = Array.IndexOf(args, "-i");
        var helpFlagIdx = args.Length;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is null)
            {
                return Error("错误：命令行参数中包含空值", 1);
            }
            if ((args[i] == "--help" || args[i] == "-h") &&
                (inputIdx < 0 || i != inputIdx + 1))
            {
                helpFlagIdx = i;
                break;
            }
        }
        if (helpFlagIdx < args.Length)
        {
            return new CliParseResult
            {
                ShowHelp = true,
                IsValid = true,
                ExitCode = 0
            };
        }

        // ── 必选参数 -i ─────────────────────────────────────
        if (inputIdx < 0 || inputIdx + 1 >= args.Length)
        {
            return Error("错误：缺少输入图片路径，使用 -i 指定\n使用 -h 查看完整帮助信息", 1);
        }
        var inputPath = args[inputIdx + 1];

        // ── 检查 inputPath 是否疑似参数名 ─────────────────
        if (inputPath.StartsWith('-'))
        {
            return Error(
                $"错误：-i 后的值 \"{inputPath}\" 看起来像参数名，请输入有效的图片路径\n使用 -h 查看完整帮助信息",
                1);
        }

        // ── 检查是否出现了多个 -i ─────────────────────────
        for (var i = inputIdx + 1; i < args.Length; i++)
        {
            if (args[i] == "-i")
            {
                return Error(
                    "错误：检测到多个 -i 参数，只能指定一个输入图片路径\n使用 -h 查看完整帮助信息",
                    1);
            }
        }

        // ── 可选参数默认值 ──────────────────────────────────
        var outputPath = "cartoon.png";
        var p = Params.Default;
        var verbose = false;

        // ── 已知参数集合 ────────────────────────────────────
        var knownFlags = new HashSet<string>
        {
            "-o", "-v", "--edge-thresh", "--sat", "--radius",
            "--sigma-d", "--sigma-r", "--loop", "--workers"
        };

        // ── 解析所有可选参数 ────────────────────────────────
        for (var i = 0; i < args.Length; i++)
        {
            // 跳过 -i 及其紧跟的值（避免将其值误解析为标志）
            if (i == inputIdx || i == inputIdx + 1) continue;

            var arg = args[i];
            if (arg.StartsWith('-') && !knownFlags.Contains(arg))
            {
                return Error(
                    $"错误：未知参数 \"{arg}\"\n使用 -h 查看完整帮助信息",
                    1);
            }

            switch (arg)
            {
                case "-o":
                    if (i + 1 >= args.Length)
                        return Error("错误：-o 缺少输出路径值", 1);
                    outputPath = args[i + 1];
                    if (outputPath.StartsWith('-'))
                    {
                        return Error(
                            $"-o 后的值 \"{outputPath}\" 看起来像参数名，请输入有效的输出路径\n使用 -h 查看完整帮助信息",
                            1);
                    }
                    i++;
                    break;

                case "-v":
                    verbose = true;
                    break;

                case "--edge-thresh":
                    if (i + 1 >= args.Length)
                        return Error("错误：--edge-thresh 缺少值", 1);
                    if (TryParseFloat(args[i + 1], out var et))
                    {
                        if (et < 0 || et > 1)
                            return Error("错误：--edge-thresh 必须在 0.0 ~ 1.0 之间", 1);
                        p = p with { EdgeThresh = et };
                    }
                    else return Error($"错误：--edge-thresh 值 \"{args[i + 1]}\" 不是有效数字", 1);
                    i++;
                    break;

                case "--sat":
                    if (i + 1 >= args.Length)
                        return Error("错误：--sat 缺少值", 1);
                    if (TryParseFloat(args[i + 1], out var sat))
                    {
                        if (sat < 0)
                            return Error("错误：--sat 必须 >= 0", 1);
                        p = p with { SatScalar = sat };
                    }
                    else return Error($"错误：--sat 值 \"{args[i + 1]}\" 不是有效数字", 1);
                    i++;
                    break;

                case "--radius":
                    if (i + 1 >= args.Length)
                        return Error("错误：--radius 缺少值", 1);
                    if (TryParseInt(args[i + 1], out var r))
                    {
                        if (r < 1 || r > 50)
                            return Error("错误：--radius 必须在 1 ~ 50 之间", 1);
                        p = p with { Radius = r };
                    }
                    else return Error($"错误：--radius 值 \"{args[i + 1]}\" 不是有效整数", 1);
                    i++;
                    break;

                case "--sigma-d":
                    if (i + 1 >= args.Length)
                        return Error("错误：--sigma-d 缺少值", 1);
                    if (TryParseFloat(args[i + 1], out var sd))
                    {
                        if (sd <= 0)
                            return Error("错误：--sigma-d 必须 > 0", 1);
                        p = p with { SigmaD = sd };
                    }
                    else return Error($"错误：--sigma-d 值 \"{args[i + 1]}\" 不是有效数字", 1);
                    i++;
                    break;

                case "--sigma-r":
                    if (i + 1 >= args.Length)
                        return Error("错误：--sigma-r 缺少值", 1);
                    if (TryParseFloat(args[i + 1], out var sr))
                    {
                        if (sr <= 0)
                            return Error("错误：--sigma-r 必须 > 0", 1);
                        p = p with { SigmaR = sr };
                    }
                    else return Error($"错误：--sigma-r 值 \"{args[i + 1]}\" 不是有效数字", 1);
                    i++;
                    break;

                case "--loop":
                    if (i + 1 >= args.Length)
                        return Error("错误：--loop 缺少值", 1);
                    if (TryParseInt(args[i + 1], out var l))
                    {
                        if (l < 1 || l > 10)
                            return Error("错误：--loop 必须在 1 ~ 10 之间", 1);
                        p = p with { LoopNum = l };
                    }
                    else return Error($"错误：--loop 值 \"{args[i + 1]}\" 不是有效整数", 1);
                    i++;
                    break;

                case "--workers":
                    if (i + 1 >= args.Length)
                        return Error("错误：--workers 缺少值", 1);
                    if (TryParseInt(args[i + 1], out var w))
                    {
                        if (w < 0)
                            return Error("错误：--workers 必须 >= 0", 1);
                        p = p with { Workers = w };
                    }
                    else return Error($"错误：--workers 值 \"{args[i + 1]}\" 不是有效整数", 1);
                    i++;
                    break;
            }
        }

        // ── 输出文件后缀校验 ────────────────────────────────
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
        {
            return Error($"错误：输出路径 \"{outputPath}\" 后缀不支持，仅支持 .jpg / .jpeg / .png", 1);
        }

        return new CliParseResult
        {
            IsValid = true,
            ExitCode = 0,
            InputPath = inputPath,
            OutputPath = outputPath,
            Params = p,
            Verbose = verbose
        };
    }

    /// <summary>
    /// 打印完整的中文帮助信息。
    /// </summary>
    public static void PrintHelp()
    {
        Console.Write("""
ImageCartoonlization 1.0.0 — 照片卡通化命令行工具

将照片转换为卡通风格。通过饱和度增强、双边滤波保边平滑、边缘检测描边
等技术，模拟手绘卡通效果。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

用法:
  ImageCartoonlization -i <输入图片> [-o <输出图片>] [选项]

必选参数:
  -i <路径>            输入图片路径（支持 JPEG / PNG 格式）

可选参数:
  -o <路径>            输出图片路径
                        默认: cartoon.png
                        后缀 .jpg / .jpeg → JPEG 编码（质量 95）
                        后缀 .png         → PNG 编码（无损）

  --edge-thresh <值>   边缘检测阈值
                        默认: 0.02
                        范围: 0.0 ~ 1.0
                        越小 → 检测到的边缘越多，描边越密
                        越大 → 只保留强边缘，描边越稀疏

  --sat <值>           饱和度增益
                        默认: 2.0
                        1.0 = 不改变饱和度
                        >1 = 增强饱和度（推荐 1.5 ~ 3.0）
                        <1 = 降低饱和度

  --radius <值>        双边滤波窗口半径
                        默认: 10
                        范围: 1 ~ 50
                        窗口实际大小为 (2 × radius + 1) 像素
                        越大 → 平滑范围越大，计算越慢

  --sigma-d <值>       空间域标准差
                        默认: 3.0
                        控制像素空间距离对滤波的影响
                        越大 → 远处像素也参与平均，越模糊

  --sigma-r <值>       颜色域标准差
                        默认: 0.1
                        控制像素颜色差异对滤波的影响
                        越大 → 不同颜色的像素也参与平均，越模糊
                        越小 → 边缘保持越好

  --loop <值>          双边滤波迭代次数
                        默认: 1
                        范围: 1 ~ 10
                        每次迭代都会进一步平滑图像
                        越大 → 越模糊，计算时间成倍增加

  --workers <值>       并行 worker 数量
                        默认: 0（自动使用 CPU 核数）
                        设为 1 禁用并行处理
                        设为 N 使用 N 个并行 worker

  -v                   详细输出模式
                        打印每一步的处理耗时和参数信息

  -h, --help           显示本帮助信息

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

使用示例:

  # 基本用法：将 photo.jpg 转换为 cartoon.png（默认参数）
  ImageCartoonlization -i photo.jpg -o cartoon.png

  # 增强边缘效果：降低阈值，增加描边密度
  ImageCartoonlization -i photo.jpg -o cartoon.jpg --edge-thresh 0.005

  # 更模糊的卡通效果：增大窗口和迭代次数
  ImageCartoonlization -i photo.jpg -o cartoon.png --radius 15 --loop 3

  # 鲜艳色彩 + 弱描边
  ImageCartoonlization -i photo.jpg -o cartoon.png --sat 3 --edge-thresh 0.05

  # 使用单线程 + 详细输出
  ImageCartoonlization -i photo.jpg -o cartoon.png --workers 1 -v

  # 输出为 JPEG 格式
  ImageCartoonlization -i photo.png -o cartoon.jpg

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

支持的图片格式:
  输入: JPEG (.jpg / .jpeg)、PNG (.png)
  输出: JPEG (.jpg / .jpeg)、PNG (.png)

处理流程:
  加载 → 饱和度调整 → 双边滤波（保边平滑）→ Sobel 边缘检测
       → 边缘叠加（黑色描边）→ 保存输出

技术说明:
  本项目从 MATLAB 原版（Image_cartoonlization）移植到 C# (.NET 10 AOT)。
  双边滤波在 CIELab 色彩空间中执行，以更好地匹配人眼感知。
  边缘检测使用 Sobel 算子进行 3×3 卷积，取梯度幅值后二值化。
""");
    }

    private static CliParseResult Error(string message, int exitCode)
    {
        return new CliParseResult
        {
            IsValid = false,
            ExitCode = exitCode,
            ErrorMessage = message
        };
    }

    private static bool TryParseFloat(string s, out float value)
    {
        return float.TryParse(s,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseInt(string s, out int value)
    {
        return int.TryParse(s,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }
}
