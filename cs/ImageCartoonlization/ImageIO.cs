// ============================================================================
// 文件名:   ImageIO.cs
// 功能描述:  提供 JPEG/PNG 图片文件的加载和保存功能。
//           通过 SixLabors.ImageSharp 进行编解码，
//           内部使用 ImageData 结构体表示像素数据（float 归一化到 [0, 1]）。
//
// 支持格式:
//   输入: JPEG (.jpg / .jpeg)、PNG (.png)
//   输出: JPEG (.jpg / .jpeg，质量 95)、PNG (.png，无损)
//
// 依赖关系:  依赖 SixLabors.ImageSharp NuGet 包
// ============================================================================

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageCartoonlization;

/// <summary>
/// 图片文件的输入输出操作。
/// </summary>
public static class ImageIO
{
    /// <summary>
    /// 加载指定路径的 JPEG/PNG 图片文件。
    /// </summary>
    /// <param name="path">图片文件路径</param>
    /// <returns>加载后的图像数据</returns>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    /// <exception cref="InvalidOperationException">格式不支持或解码失败</exception>
    public static ImageData LoadImage(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"图片文件不存在: {path}", path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
        {
            throw new InvalidOperationException(
                $"不支持的图片格式 \"{ext}\"，仅支持 JPEG 和 PNG");
        }

        using var image = Image.Load<Rgb24>(path);
        var width = image.Width;
        var height = image.Height;
        var img = new ImageData(width, height, 3);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowOffset = y * width * 3;
                for (var x = 0; x < width; x++)
                {
                    var pixelOffset = rowOffset + x * 3;
                    img.Data[pixelOffset] = row[x].R / 255f;
                    img.Data[pixelOffset + 1] = row[x].G / 255f;
                    img.Data[pixelOffset + 2] = row[x].B / 255f;
                }
            }
        });

        return img;
    }

    /// <summary>
    /// 尝试加载指定路径的图片文件，返回成功标志和错误信息。
    /// </summary>
    /// <param name="path">图片文件路径</param>
    /// <param name="img">加载成功的图像数据</param>
    /// <param name="error">失败时的错误信息</param>
    /// <returns>加载成功返回 true，否则返回 false</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "TryLoadImage 需要捕获所有可能的异常以提供友好的错误返回")]
    public static bool TryLoadImage(string path, out ImageData img, out string? error)
    {
        try
        {
            img = LoadImage(path);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            img = default;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 尝试加载指定路径的图片文件（简化版，不返回错误详情）。
    /// </summary>
    /// <param name="path">图片文件路径</param>
    /// <param name="img">加载成功的图像数据</param>
    /// <returns>加载成功返回 true，否则返回 false</returns>
    public static bool TryLoadImage(string path, out ImageData img)
    {
        return TryLoadImage(path, out img, out _);
    }

    /// <summary>
    /// 将 ImageData 保存到指定路径的图片文件。
    /// 输出格式由文件扩展名决定：
    ///   .jpg / .jpeg → JPEG（质量 95）
    ///   .png         → PNG（无损）
    /// </summary>
    /// <param name="path">输出文件路径</param>
    /// <param name="img">要保存的图像数据</param>
    /// <exception cref="InvalidOperationException">输出格式不支持</exception>
    public static void SaveImage(string path, ImageData img)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        using var image = new Image<Rgb24>(img.Width, img.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < img.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowOffset = y * img.Width * 3;
                for (var x = 0; x < img.Width; x++)
                {
                    var pixelOffset = rowOffset + x * 3;
                    row[x] = new Rgb24(
                        FloatToByte(img.Data[pixelOffset]),
                        FloatToByte(img.Data[pixelOffset + 1]),
                        FloatToByte(img.Data[pixelOffset + 2])
                    );
                }
            }
        });

        switch (ext)
        {
            case ".jpg":
            case ".jpeg":
                image.Save(path, new JpegEncoder { Quality = 95 });
                break;
            case ".png":
                image.Save(path, new PngEncoder());
                break;
            default:
                throw new InvalidOperationException(
                    $"不支持的输出格式 \"{ext}\"，仅支持 .jpg / .jpeg / .png");
        }
    }

    /// <summary>
    /// 将 float 像素值（[0, 1]）转换为 uint8（[0, 255]），使用四舍五入。
    /// </summary>
    private static byte FloatToByte(float v)
    {
        var val = v * 255f + 0.5f;
        if (val < 0) return 0;
        if (val > 255) return 255;
        return (byte)val;
    }
}
