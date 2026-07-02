// ============================================================================
// 文件名:   Program.cs
// 功能描述:  应用程序入口点。负责命令行参数解析、校验和调用卡通化管线。
//           支持 -v 详细输出模式和 -h/--help 帮助信息。
//
// 依赖关系:  依赖 CliParser、ImageIO、CartoonPipeline 等模块
// ============================================================================

using System.Diagnostics;

namespace ImageCartoonlization;

static class Program
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "SaveImage 可能抛出多种异常（I/O、编码等），全部统一处理为友好错误消息")]
    static int Main(string[] args)
    {
        // ── 解析命令行参数 ─────────────────────────────────
        var result = CliParser.ParseArgs(args);

        // ── 帮助信息 ───────────────────────────────────────
        if (result.ShowHelp)
        {
            CliParser.PrintHelp();
            return 0;
        }

        // ── 错误处理 ───────────────────────────────────────
        if (!result.IsValid)
        {
            Console.Error.WriteLine(result.ErrorMessage);
            return result.ExitCode;
        }

        // ── 加载输入图片 ───────────────────────────────────
        var loadSw = Stopwatch.StartNew();
        if (!ImageIO.TryLoadImage(result.InputPath!, out var img, out var error))
        {
            Console.Error.WriteLine($"[加载] 失败: {error}");
            return 1;
        }
        var loadDur = loadSw.Elapsed;

        if (result.Verbose)
        {
            Console.WriteLine($"[加载] 成功 | {Path.GetFileName(result.InputPath)} | " +
                $"{img.Width}×{img.Height} | {RoundDuration(loadDur)}");
        }

        // ── 执行卡通化处理 ─────────────────────────────────
        var (processed, steps) = CartoonPipeline.Cartoonize(img, result.Params);

        // ── Verbose 输出 ───────────────────────────────────
        if (result.Verbose)
        {
            foreach (var s in steps)
            {
                if (s.Name == "总处理") continue;
                var line = $"[{s.Name}] 成功 | {RoundDuration(s.Duration)}";
                if (!string.IsNullOrEmpty(s.Detail))
                    line += " | " + s.Detail;
                Console.WriteLine(line);
            }
        }

        // ── 保存输出图片 ───────────────────────────────────
        var saveSw = Stopwatch.StartNew();
        try
        {
            ImageIO.SaveImage(result.OutputPath, processed);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[输出] 失败: {ex.Message}");
            return 1;
        }
        var saveDur = saveSw.Elapsed;

        if (result.Verbose)
        {
            Console.WriteLine($"[输出] 成功 | {Path.GetFileName(result.OutputPath)} | " +
                $"{RoundDuration(saveDur)}");
        }

        // ── 总耗时 ─────────────────────────────────────────
        foreach (var s in steps)
        {
            if (s.Name == "总处理")
            {
                Console.WriteLine($"[完成] 总耗时 {RoundDuration(s.Duration)}");
                break;
            }
        }

        return 0;
    }

    /// <summary>
    /// 将 TimeSpan 格式化为毫秒精度。
    /// </summary>
    private static string RoundDuration(TimeSpan d)
    {
        return $"{d.TotalMilliseconds:F0}ms";
    }
}
