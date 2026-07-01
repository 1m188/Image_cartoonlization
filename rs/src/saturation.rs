//! 饱和度调整模块。
//!
//! 使用线性内插/外推公式在原始颜色和灰度之间调整饱和度:
//! `result = (1 - scalar) * gray + scalar * color`
//!
//! - scalar = 1.0: 不变
//! - scalar > 1.0: 色移远离灰色 → 饱和度增强
//! - scalar < 1.0: 色移靠近灰色 → 饱和度降低
//! - scalar = 0.0: 完全灰度化

use crate::data::ImageData;

/// 调整图像的饱和度。
///
/// 对每个像素先计算 BT.601 灰度值，然后在灰度与原始色之间做线性插值。
/// 结果自动钳制到 [0.0, 1.0]。
///
/// # Panics
/// 若输入非 3 通道或 scalar 为非法值会 panic。
pub fn adjust_saturation(img: &ImageData, scalar: f64) -> ImageData {
    assert_eq!(img.channels, 3, "adjust_saturation 要求 3 通道 RGB 输入");
    assert!(scalar.is_finite(), "scalar 必须是有限值");
    let height = img.height;
    let width = img.width;
    let mut data = vec![0.0f64; width * height * 3];

    for y in 0..height {
        for x in 0..width {
            // ITU-R BT.601 亮度
            let gray =
                0.299 * img.get(y, x, 0) + 0.587 * img.get(y, x, 1) + 0.114 * img.get(y, x, 2);
            let base = (y * width + x) * 3;
            data[base] = ((1.0 - scalar) * gray + scalar * img.get(y, x, 0)).clamp(0.0, 1.0);
            data[base + 1] = ((1.0 - scalar) * gray + scalar * img.get(y, x, 1)).clamp(0.0, 1.0);
            data[base + 2] = ((1.0 - scalar) * gray + scalar * img.get(y, x, 2)).clamp(0.0, 1.0);
        }
    }

    ImageData {
        width,
        height,
        channels: 3,
        data,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_scalar_one_is_identity() {
        let mut img = ImageData::new(2, 2, 3);
        for y in 0..2 {
            for x in 0..2 {
                img.set(y, x, 0, 0.2);
                img.set(y, x, 1, 0.5);
                img.set(y, x, 2, 0.8);
            }
        }
        let result = adjust_saturation(&img, 1.0);
        for y in 0..2 {
            for x in 0..2 {
                for c in 0..3 {
                    assert!(
                        (result.get(y, x, c) - img.get(y, x, c)).abs() < 1e-10,
                        "像素 ({},{},{}) 不一致",
                        y,
                        x,
                        c
                    );
                }
            }
        }
    }

    #[test]
    fn test_scalar_zero_grayscale() {
        let mut img = ImageData::new(2, 2, 3);
        img.set(0, 0, 0, 0.2);
        img.set(0, 0, 1, 0.5);
        img.set(0, 0, 2, 0.8);
        let result = adjust_saturation(&img, 0.0);
        for y in 0..2 {
            for x in 0..2 {
                let r = result.get(y, x, 0);
                let g = result.get(y, x, 1);
                let b = result.get(y, x, 2);
                assert!((r - g).abs() < 1e-10, "R≠G");
                assert!((g - b).abs() < 1e-10, "G≠B");
            }
        }
    }

    #[test]
    fn test_scalar_two_increases_variance() {
        let mut img = ImageData::new(3, 3, 3);
        for y in 0..3 {
            for x in 0..3 {
                img.set(y, x, 0, (y * 3 + x) as f64 / 9.0);
                img.set(y, x, 1, 0.5);
                img.set(y, x, 2, 0.3);
            }
        }
        let result = adjust_saturation(&img, 2.0);
        for y in 0..3 {
            for x in 0..3 {
                let mut orig_var = 0.0;
                let mut res_var = 0.0;
                let orig_mean = (img.get(y, x, 0) + img.get(y, x, 1) + img.get(y, x, 2)) / 3.0;
                let res_mean =
                    (result.get(y, x, 0) + result.get(y, x, 1) + result.get(y, x, 2)) / 3.0;
                for c in 0..3 {
                    orig_var += (img.get(y, x, c) - orig_mean).powi(2);
                    res_var += (result.get(y, x, c) - res_mean).powi(2);
                }
                assert!(res_var >= orig_var, "({},{}) 处方差应增加", y, x);
            }
        }
    }

    #[test]
    fn test_output_clamped_to_zero_one() {
        let mut img = ImageData::new(2, 2, 3);
        for y in 0..2 {
            for x in 0..2 {
                img.set(y, x, 0, 1.0);
                img.set(y, x, 1, 1.0);
                img.set(y, x, 2, 1.0);
            }
        }
        let result = adjust_saturation(&img, 5.0);
        for y in 0..2 {
            for x in 0..2 {
                for c in 0..3 {
                    let v = result.get(y, x, c);
                    assert!((0.0..=1.0).contains(&v), "值 {} 超出范围", v);
                }
            }
        }
    }
}
