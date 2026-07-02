// ============================================================================
// 文件名:   LabColor.cs
// 功能描述:  实现 sRGB 色彩空间与 CIELab 色彩空间之间的双向转换，
//           以及 RGB 到灰度的转换。
//           双边滤波在 CIELab 空间中进行，以更好地匹配人眼感知。
//
// 核心算法:
//   1. sRGB → 线性 RGB（去 gamma 校正）
//   2. 线性 RGB → CIE XYZ（D65 参考白点）
//   3. CIE XYZ → CIELab（含 f(t)/fInverse(t) 分段函数）
//   反向转换同理。
//
//   灰度转换使用 ITU-R BT.601 亮度系数：
//     gray = 0.299*R + 0.587*G + 0.114*B
//
// 参考来源:  Go 版本 go/cartoon/lab.go
// 依赖关系:  无外部依赖，纯数学计算
// ============================================================================

using static System.MathF;

namespace ImageCartoonlization;

/// <summary>
/// sRGB 与 CIELab 色彩空间的双向转换工具。
/// </summary>
public static class LabColor
{
    // D65 参考白点的 CIE XYZ 三刺激值。
    private const float Xn = 0.95047f;
    private const float Yn = 1.00000f;
    private const float Zn = 1.08883f;

    // f(t) 分段函数的边界阈值。
    // delta = (6/29)^3 ≈ 0.008856
    private const float Delta = 6.0f / 29.0f;
    private const float DeltaCubed = Delta * Delta * Delta;

    // fInverse(t) 的边界阈值：(6/29)^2 ≈ 0.0428
    private const float DeltaInv = Delta * Delta;

    /// <summary>
    /// f 函数：将线性分量映射为 CIELab 使用的非线性分量。
    /// </summary>
    private static float F(float t)
    {
        if (t > DeltaCubed)
        {
            return Cbrt(t);
        }
        return t / (3.0f * DeltaInv) + 4.0f / 29.0f;
    }

    /// <summary>
    /// f 的逆函数。
    /// </summary>
    private static float FInverse(float t)
    {
        if (t > Delta)
        {
            return t * t * t;
        }
        return 3.0f * DeltaInv * (t - 4.0f / 29.0f);
    }

    /// <summary>
    /// 将 sRGB 分量转为线性分量（去 gamma 校正）。
    /// </summary>
    private static float SRgbToLinear(float c)
    {
        if (c <= 0.04045f)
        {
            return c / 12.92f;
        }
        return Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    /// 将线性分量转为 sRGB 分量（加 gamma 校正）。
    /// </summary>
    private static float LinearToSRgb(float c)
    {
        if (c <= 0.0031308f)
        {
            return 12.92f * c;
        }
        return 1.055f * Pow(c, 1.0f / 2.4f) - 0.055f;
    }

    /// <summary>
    /// 将 sRGB 色彩空间的图片转换到 CIELab 色彩空间。
    /// 输出三个通道依次为 L、a、b。
    /// </summary>
    /// <param name="img">输入的 RGB 图片数据，像素值范围 [0, 1]</param>
    /// <returns>CIELab 色彩空间的图片数据</returns>
    /// <exception cref="ArgumentException">输入图像通道数不为 3</exception>
    public static ImageData RgbToLab(ImageData img)
    {
        if (img.Channels != 3)
        {
            throw new ArgumentException($"RgbToLab 要求 3 通道 RGB 输入，实际通道数为 {img.Channels}", nameof(img));
        }

        var height = img.Height;
        var width = img.Width;
        var lab = new ImageData(width, height, 3);
        var src = img.Data;
        var dst = lab.Data;

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = rowBase + x * 3;
                var r = src[idx];
                var g = src[idx + 1];
                var b = src[idx + 2];

                // sRGB → 线性 RGB
                var rLin = SRgbToLinear(r);
                var gLin = SRgbToLinear(g);
                var bLin = SRgbToLinear(b);

                // 线性 RGB → CIE XYZ
                var xVal = 0.4124564f * rLin + 0.3575761f * gLin + 0.1804375f * bLin;
                var yVal = 0.2126729f * rLin + 0.7151522f * gLin + 0.0721750f * bLin;
                var zVal = 0.0193339f * rLin + 0.1191920f * gLin + 0.9503041f * bLin;

                // CIE XYZ → CIELab
                var l = 116.0f * F(yVal / Yn) - 16.0f;
                var a = 500.0f * (F(xVal / Xn) - F(yVal / Yn));
                var bStar = 200.0f * (F(yVal / Yn) - F(zVal / Zn));

                dst[idx] = l;
                dst[idx + 1] = a;
                dst[idx + 2] = bStar;
            }
        }
        return lab;
    }

    /// <summary>
    /// 将 CIELab 色彩空间的图片转换回 sRGB 色彩空间。
    /// </summary>
    /// <param name="img">输入的 CIELab 图片数据</param>
    /// <returns>sRGB 色彩空间的图片数据，像素值范围 [0, 1]</returns>
    /// <exception cref="ArgumentException">输入图像通道数不为 3</exception>
    public static ImageData LabToRgb(ImageData img)
    {
        if (img.Channels != 3)
        {
            throw new ArgumentException($"LabToRgb 要求 3 通道 CIELab 输入，实际通道数为 {img.Channels}", nameof(img));
        }

        var height = img.Height;
        var width = img.Width;
        var rgb = new ImageData(width, height, 3);
        var src = img.Data;
        var dst = rgb.Data;

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = rowBase + x * 3;
                var l = src[idx];
                var a = src[idx + 1];
                var bStar = src[idx + 2];

                // CIELab → CIE XYZ
                var fy = (l + 16.0f) / 116.0f;
                var fx = fy + a / 500.0f;
                var fz = fy - bStar / 200.0f;

                var xx = Xn * FInverse(fx);
                var yy = Yn * FInverse(fy);
                var zz = Zn * FInverse(fz);

                // CIE XYZ → 线性 RGB
                var rLin = 3.2404542f * xx - 1.5371385f * yy - 0.4985314f * zz;
                var gLin = -0.9692660f * xx + 1.8760108f * yy + 0.0415560f * zz;
                var bLin = 0.0556434f * xx - 0.2040259f * yy + 1.0572252f * zz;

                // 钳制到 [0, 1] 再加 gamma 校正
                dst[idx] = LinearToSRgb(Math.Clamp(rLin, 0f, 1f));
                dst[idx + 1] = LinearToSRgb(Math.Clamp(gLin, 0f, 1f));
                dst[idx + 2] = LinearToSRgb(Math.Clamp(bLin, 0f, 1f));
            }
        }
        return rgb;
    }

    /// <summary>
    /// 将 RGB 图片转换为灰度图。
    /// 使用 ITU-R BT.601 亮度系数。
    /// </summary>
    /// <param name="img">输入的 RGB 图片数据</param>
    /// <returns>单通道灰度图数据</returns>
    /// <exception cref="ArgumentException">输入图像通道数不为 3</exception>
    public static ImageData RgbToGray(ImageData img)
    {
        if (img.Channels != 3)
        {
            throw new ArgumentException($"RgbToGray 要求 3 通道 RGB 输入，实际通道数为 {img.Channels}", nameof(img));
        }

        var height = img.Height;
        var width = img.Width;
        var gray = new ImageData(width, height, 1);
        var src = img.Data;
        var dst = gray.Data;

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = rowBase + x * 3;
                dst[y * width + x] =
                    0.299f * src[idx] +
                    0.587f * src[idx + 1] +
                    0.114f * src[idx + 2];
            }
        }
        return gray;
    }
}
