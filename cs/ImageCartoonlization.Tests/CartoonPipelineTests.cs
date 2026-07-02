// ============================================================================
// 文件名:   CartoonPipelineTests.cs
// 功能描述:  CartoonPipeline 模块的单元和集成测试。
//           测试参数结构、默认值和 Cartoonize 管线完整性。
// ============================================================================

using ImageCartoonlization;

namespace ImageCartoonlization.Tests;

public class CartoonPipelineTests
{
    [Fact]
    public void Params_Default_MatchesSpec()
    {
        var p = Params.Default;

        Assert.Equal(0.02f, p.EdgeThresh);
        Assert.Equal(2.0f, p.SatScalar);
        Assert.Equal(10, p.Radius);
        Assert.Equal(3.0f, p.SigmaD);
        Assert.Equal(0.1f, p.SigmaR);
        Assert.Equal(1, p.LoopNum);
        Assert.Equal(0, p.Workers);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "测试中使用 Random 产生测试数据")]
    public void Cartoonize_DefaultParams_OnSmallImage_ReturnsValidOutput()
    {
        var img = new ImageData(10, 10, 3);
        var rng = new Random(42);
        for (var i = 0; i < img.Data.Length; i++)
        {
            img.Data[i] = (float)rng.NextDouble();
        }
        var p = Params.Default with { Radius = 1 };

        var (result, steps) = CartoonPipeline.Cartoonize(img, p);

        Assert.Equal(img.Width, result.Width);
        Assert.Equal(img.Height, result.Height);
        Assert.Equal(3, result.Channels);

        foreach (var v in result.Data)
        {
            Assert.InRange(v, 0.0f, 1.0f);
        }
    }

    [Fact]
    public void Cartoonize_ReturnsTimingInfo()
    {
        var img = new ImageData(6, 6, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;
        var p = Params.Default with { Radius = 1 };

        var (_, steps) = CartoonPipeline.Cartoonize(img, p);

        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Name == "总处理");
    }

    [Fact]
    public void Cartoonize_MultipleIterations()
    {
        var img = new ImageData(4, 4, 3);
        for (var i = 0; i < img.Data.Length; i++) img.Data[i] = 0.5f;
        var p = Params.Default with { LoopNum = 3 };

        var (result, steps) = CartoonPipeline.Cartoonize(img, p);

        Assert.Equal(4, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(3, result.Channels);
    }
}
