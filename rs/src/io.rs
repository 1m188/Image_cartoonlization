//! 图像文件读写模块。
//!
//! 支持 JPEG 和 PNG 格式的加载与保存。
//! 内部使用 `image` crate 进行编解码，像素值统一归一化到 [0.0, 1.0]。

use crate::data::ImageData;
use image::GenericImageView;
use std::path::Path;

/// 从文件加载图片，返回归一化到 [0,1] 的 `ImageData`。
///
/// 支持的格式: JPEG (.jpg / .jpeg)、PNG (.png)。
/// 格式由文件扩展名判断。
pub fn load_image(path: &Path) -> Result<ImageData, String> {
    let ext = path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_lowercase();

    if ext != "jpg" && ext != "jpeg" && ext != "png" {
        return Err(format!(
            "不支持的输入格式: .{} (仅支持 .jpg, .jpeg, .png)",
            ext
        ));
    }

    let img = image::open(path).map_err(|e| format!("无法打开图片: {}", e))?;
    let (width, height) = img.dimensions();
    let width = width as usize;
    let height = height as usize;

    let mut data = vec![0.0f64; width * height * 3];

    for (x, y, pixel) in img.pixels() {
        let rgba = pixel.0;
        let base = (y as usize * width + x as usize) * 3;
        data[base] = rgba[0] as f64 / 255.0;
        data[base + 1] = rgba[1] as f64 / 255.0;
        data[base + 2] = rgba[2] as f64 / 255.0;
    }

    Ok(ImageData {
        width,
        height,
        channels: 3,
        data,
    })
}

/// 将 `ImageData` 保存为图片文件。
///
/// 输出格式由扩展名决定:
/// - `.jpg` / `.jpeg`: JPEG 编码（质量 95）
/// - `.png`: PNG 无损编码
///
/// 像素值从 [0,1] 缩放到 [0,255] 时做四舍五入后钳制。
pub fn save_image(path: &Path, img: &ImageData) -> Result<(), String> {
    if img.channels < 3 {
        return Err(format!(
            "无法保存: 需要 3 通道 RGB 数据，但只有 {} 通道",
            img.channels
        ));
    }
    let ext = path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_lowercase();

    if ext != "jpg" && ext != "jpeg" && ext != "png" {
        return Err(format!(
            "不支持的输出格式: .{} (仅支持 .jpg, .jpeg, .png)",
            ext
        ));
    }

    let mut out_img = image::RgbImage::new(img.width as u32, img.height as u32);
    for y in 0..img.height {
        for x in 0..img.width {
            let r = (img.get(y, x, 0) * 255.0 + 0.5).clamp(0.0, 255.0) as u8;
            let g = (img.get(y, x, 1) * 255.0 + 0.5).clamp(0.0, 255.0) as u8;
            let b = (img.get(y, x, 2) * 255.0 + 0.5).clamp(0.0, 255.0) as u8;
            out_img.put_pixel(x as u32, y as u32, image::Rgb([r, g, b]));
        }
    }

    match ext.as_str() {
        "jpg" | "jpeg" => {
            let mut writer =
                std::fs::File::create(path).map_err(|e| format!("无法创建输出文件: {}", e))?;
            let mut encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut writer, 95);
            encoder
                .encode(
                    &out_img,
                    out_img.width(),
                    out_img.height(),
                    image::ExtendedColorType::Rgb8,
                )
                .map_err(|e| format!("JPEG 编码失败: {}", e))?
        }
        "png" => {
            out_img
                .save(path)
                .map_err(|e| format!("PNG 保存失败: {}", e))?;
        }
        _ => unreachable!(),
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn test_jpg_path() -> PathBuf {
        PathBuf::from("../test.jpg")
    }

    #[test]
    fn test_load_valid_jpeg_dimensions() {
        let img = load_image(&test_jpg_path()).expect("加载 test.jpg");
        assert_eq!(img.width, 2812);
        assert_eq!(img.height, 1280);
        assert_eq!(img.channels, 3);
        assert_eq!(img.data.len(), 2812 * 1280 * 3);
    }

    #[test]
    fn test_load_nonexistent_file_errors() {
        let result = load_image(&PathBuf::from("/nonexistent/path/photo.jpg"));
        assert!(result.is_err());
    }

    #[test]
    fn test_load_unsupported_format_errors() {
        let result = load_image(&PathBuf::from("no_such_file.gif"));
        assert!(result.is_err());
    }

    #[test]
    fn test_save_and_reload_png_roundtrip() {
        let img = load_image(&test_jpg_path()).expect("加载");
        let tmp = PathBuf::from("/tmp/test_roundtrip.png");
        save_image(&tmp, &img).expect("保存 PNG");
        let reloaded = load_image(&tmp).expect("重新加载 PNG");
        let _ = std::fs::remove_file(&tmp);
        assert_eq!(reloaded.width, img.width);
        assert_eq!(reloaded.height, img.height);
        // PNG 是无损的，像素应几乎一致
        for y in 0..img.height {
            for x in 0..img.width {
                for c in 0..3 {
                    let a = img.get(y, x, c);
                    let b = reloaded.get(y, x, c);
                    assert!(
                        (a - b).abs() < 0.01,
                        "PNG 像素 ({},{},{}) {} vs {}",
                        y,
                        x,
                        c,
                        a,
                        b
                    );
                }
            }
        }
    }

    #[test]
    fn test_save_jpeg_creates_jpeg_file() {
        let img = ImageData::new(4, 4, 3);
        let tmp = PathBuf::from("/tmp/test_small.jpg");
        save_image(&tmp, &img).expect("保存 JPEG");
        // 验证可以重新加载
        let reloaded = load_image(&tmp).expect("重新加载 JPEG");
        let _ = std::fs::remove_file(&tmp);
        assert_eq!(reloaded.width, 4);
        assert_eq!(reloaded.height, 4);
    }

    #[test]
    fn test_save_unsupported_extension_errors() {
        let img = ImageData::new(2, 2, 3);
        let result = save_image(&PathBuf::from("/tmp/out.bmp"), &img);
        assert!(result.is_err());
    }

    #[test]
    fn test_save_grayscale_rejected() {
        let img = ImageData::new(2, 2, 1);
        let result = save_image(&PathBuf::from("/tmp/out.png"), &img);
        assert!(result.is_err());
    }
}
