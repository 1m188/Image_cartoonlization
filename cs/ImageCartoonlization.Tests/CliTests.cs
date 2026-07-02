// ============================================================================
// 文件名:   CliTests.cs
// 功能描述:  命令行接口的单元和集成测试。
//           测试参数解析、范围校验、帮助输出和终端到终端管线。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class CliTests
{
    [Fact]
    public void ParseArgs_Help_LongFlag()
    {
        var result = CliParser.ParseArgs(["--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_Help_ShortFlag()
    {
        var result = CliParser.ParseArgs(["-h"]);

        Assert.True(result.ShowHelp);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_MissingInput_Errors()
    {
        var result = CliParser.ParseArgs([]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_ValidInput_ParsesCorrectly()
    {
        var result = CliParser.ParseArgs([
            "-i", "photo.jpg",
            "-o", "output.png",
            "--edge-thresh", "0.05",
            "--sat", "3.0",
            "--radius", "5",
            "--sigma-d", "5.0",
            "--sigma-r", "0.2",
            "--loop", "3",
            "--workers", "2",
            "-v"
        ]);

        Assert.True(result.IsValid);
        Assert.Equal("photo.jpg", result.InputPath);
        Assert.Equal("output.png", result.OutputPath);
        Assert.Equal(0.05f, result.Params.EdgeThresh);
        Assert.Equal(3.0f, result.Params.SatScalar);
        Assert.Equal(5, result.Params.Radius);
        Assert.Equal(5.0f, result.Params.SigmaD);
        Assert.Equal(0.2f, result.Params.SigmaR);
        Assert.Equal(3, result.Params.LoopNum);
        Assert.Equal(2, result.Params.Workers);
        Assert.True(result.Verbose);
    }

    [Fact]
    public void ParseArgs_DefaultOutputPath()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg"]);

        Assert.True(result.IsValid);
        Assert.Equal("cartoon.png", result.OutputPath);
    }

    [Fact]
    public void ParseArgs_InvalidEdgeThresh_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--edge-thresh", "1.5"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_InvalidRadius_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--radius", "0"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_NegativeSigma_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--sigma-d", "-1"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_InvalidLoopNum_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--loop", "15"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_UnsupportedOutputExtension_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "-o", "output.bmp"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_InputPathLooksLikeFlag_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "--edge-thresh", "0.1"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_InputPathLooksLikeShortFlag_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "-v"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_EdgeThreshMissingValue_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--edge-thresh"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_RadiusMissingValue_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--radius"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_SigmaDMissingValue_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--sigma-d"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_LoopMissingValue_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--loop"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_OutputPathMissingValue_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "-o"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_MultipleFlags_CorrectlySkipValues()
    {
        var result = CliParser.ParseArgs([
            "-i", "photo.jpg",
            "--radius", "5",
            "--sat", "3.0",
            "--loop", "2"
        ]);

        Assert.True(result.IsValid);
        Assert.Equal(5, result.Params.Radius);
        Assert.Equal(3.0f, result.Params.SatScalar);
        Assert.Equal(2, result.Params.LoopNum);
    }

    [Fact]
    public void ParseArgs_OutputPathLooksLikeFlag_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "-o", "--edge-thresh"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ParseArgs_UnknownFlag_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "photo.jpg", "--edge-thres"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("未知参数", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseArgs_DuplicateInput_Errors()
    {
        var result = CliParser.ParseArgs(["-i", "a.jpg", "-i", "b.jpg"]);

        Assert.False(result.IsValid);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("多个 -i", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseArgs_HelpFlagAfterI_DoesNotTriggerHelp()
    {
        var result = CliParser.ParseArgs(["-i", "-h"]);

        Assert.False(result.IsValid);
        Assert.False(result.ShowHelp);
        Assert.NotEqual(0, result.ExitCode);
    }
}
