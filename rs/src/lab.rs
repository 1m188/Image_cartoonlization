//! sRGB ↔ CIELab 色彩空间转换模块。
//!
//! 实现完整的 sRGB → 线性 RGB → CIE XYZ → CIELab 转换链，
//! 以及其逆过程。使用 D65 标准白点。
//! 所有常数与 Go 参考实现 `go/cartoon/lab.go` 保持一致。

use crate::data::ImageData;

/// D65 白点的 CIE XYZ 三刺激值。
const XN: f64 = 0.95047;
const YN: f64 = 1.00000;
const ZN: f64 = 1.08883;

/// CIELab f(t) 分段函数的边界点 δ = 6/29。
const DELTA: f64 = 6.0 / 29.0;
/// δ³ ≈ 0.008856，用于正向 f(t) 的分段判断。
const DELTA_CUBED: f64 = (6.0 / 29.0) * (6.0 / 29.0) * (6.0 / 29.0);
/// δ² ≈ 0.0428，用于反向 f⁻¹(t) 和正向分段的系数项。
/// 与 Go 中 `deltaInv` 命名一致（实际为 δ²）。
const DELTA_INV: f64 = (6.0 / 29.0) * (6.0 / 29.0);

/// CIELab 正向非线性映射 f(t)。
fn f(t: f64) -> f64 {
    if t > DELTA_CUBED {
        t.cbrt()
    } else {
        t / (3.0 * DELTA_INV) + 4.0 / 29.0
    }
}

/// CIELab 反向非线性映射 f⁻¹(t)。
fn f_inverse(t: f64) -> f64 {
    if t > DELTA {
        t * t * t
    } else {
        3.0 * DELTA_INV * (t - 4.0 / 29.0)
    }
}

/// sRGB 分量去 gamma → 线性分量。
fn srgb_to_linear(c: f64) -> f64 {
    if c <= 0.04045 {
        c / 12.92
    } else {
        ((c + 0.055) / 1.055).powf(2.4)
    }
}

/// 线性分量加 gamma → sRGB 分量。
fn linear_to_srgb(c: f64) -> f64 {
    if c <= 0.0031308 {
        12.92 * c
    } else {
        1.055 * c.powf(1.0 / 2.4) - 0.055
    }
}

/// 将值钳制到 [0.0, 1.0]。
fn clamp01(v: f64) -> f64 {
    v.clamp(0.0, 1.0)
}

/// 将 sRGB 图像（值域 [0,1]）转换为 CIELab 色彩空间。
///
/// 转换链: sRGB → 线性 RGB → CIE XYZ (D65) → CIELab
/// 输出 L 通道范围约为 [0, 100]，a* 和 b* 无严格边界。
pub fn rgb_to_lab(img: &ImageData) -> ImageData {
    let height = img.height;
    let width = img.width;
    let mut data = vec![0.0f64; width * height * 3];

    for y in 0..height {
        for x in 0..width {
            let r = srgb_to_linear(img.get(y, x, 0));
            let g = srgb_to_linear(img.get(y, x, 1));
            let b = srgb_to_linear(img.get(y, x, 2));

            // 线性 RGB → XYZ (sRGB 基色，D65 白点)
            let x_val = 0.4124564 * r + 0.3575761 * g + 0.1804375 * b;
            let y_val = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
            let z_val = 0.0193339 * r + 0.1191920 * g + 0.9503041 * b;

            // XYZ → CIELab
            let l = 116.0 * f(y_val / YN) - 16.0;
            let a = 500.0 * (f(x_val / XN) - f(y_val / YN));
            let b_out = 200.0 * (f(y_val / YN) - f(z_val / ZN));

            let base = (y * width + x) * 3;
            data[base] = l;
            data[base + 1] = a;
            data[base + 2] = b_out;
        }
    }

    ImageData {
        width,
        height,
        channels: 3,
        data,
    }
}

/// 将 CIELab 图像转换回 sRGB 色彩空间。
///
/// 转换链: CIELab → CIE XYZ → 线性 RGB → sRGB
/// 变换中线性 RGB 负值或超出 [0,1] 的值会被钳制。
pub fn lab_to_rgb(img: &ImageData) -> ImageData {
    let height = img.height;
    let width = img.width;
    let mut data = vec![0.0f64; width * height * 3];

    for y in 0..height {
        for x in 0..width {
            let l = img.get(y, x, 0);
            let a = img.get(y, x, 1);
            let b = img.get(y, x, 2);

            // CIELab → XYZ
            let fy = (l + 16.0) / 116.0;
            let fx = fy + a / 500.0;
            let fz = fy - b / 200.0;

            let xx = XN * f_inverse(fx);
            let yy = YN * f_inverse(fy);
            let zz = ZN * f_inverse(fz);

            // XYZ → 线性 RGB
            let r_lin = 3.2404542 * xx - 1.5371385 * yy - 0.4985314 * zz;
            let g_lin = -0.9692660 * xx + 1.8760108 * yy + 0.0415560 * zz;
            let b_lin = 0.0556434 * xx - 0.2040259 * yy + 1.0572252 * zz;

            let base = (y * width + x) * 3;
            data[base] = linear_to_srgb(clamp01(r_lin));
            data[base + 1] = linear_to_srgb(clamp01(g_lin));
            data[base + 2] = linear_to_srgb(clamp01(b_lin));
        }
    }

    ImageData {
        width,
        height,
        channels: 3,
        data,
    }
}

/// 将 RGB 图像转为单通道灰度图。
///
/// 使用 ITU-R BT.601 亮度系数:
/// `gray = 0.299*R + 0.587*G + 0.114*B`
pub fn rgb_to_gray(img: &ImageData) -> ImageData {
    let height = img.height;
    let width = img.width;
    let mut data = vec![0.0f64; width * height];

    for y in 0..height {
        for x in 0..width {
            let gray =
                0.299 * img.get(y, x, 0) + 0.587 * img.get(y, x, 1) + 0.114 * img.get(y, x, 2);
            data[y * width + x] = gray;
        }
    }

    ImageData {
        width,
        height,
        channels: 1,
        data,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_white_to_lab() {
        // 白色 RGB=(1,1,1) → Lab ≈ (100, 0, 0)
        let img = ImageData::new(1, 1, 3);
        let mut img = img;
        img.set(0, 0, 0, 1.0);
        img.set(0, 0, 1, 1.0);
        img.set(0, 0, 2, 1.0);
        let lab = rgb_to_lab(&img);
        let l = lab.get(0, 0, 0);
        let a = lab.get(0, 0, 1);
        let b = lab.get(0, 0, 2);
        assert!((l - 100.0).abs() < 0.5, "L={}", l);
        assert!(a.abs() < 0.5, "a={}", a);
        assert!(b.abs() < 0.5, "b={}", b);
    }

    #[test]
    fn test_black_to_lab() {
        let img = ImageData::new(1, 1, 3);
        let lab = rgb_to_lab(&img); // 全零 = 黑色
        assert!(lab.get(0, 0, 0).abs() < 0.1); // L ≈ 0
        assert!(lab.get(0, 0, 1).abs() < 0.1); // a ≈ 0
        assert!(lab.get(0, 0, 2).abs() < 0.1); // b ≈ 0
    }

    #[test]
    fn test_red_has_positive_a() {
        let mut img = ImageData::new(1, 1, 3);
        img.set(0, 0, 0, 1.0); // R=1
        img.set(0, 0, 1, 0.0); // G=0
        img.set(0, 0, 2, 0.0); // B=0
        let lab = rgb_to_lab(&img);
        let a = lab.get(0, 0, 1);
        assert!(a > 0.0, "红色应有正 a* 值，实际 a*={}", a);
    }

    #[test]
    fn test_roundtrip_neutral_gray() {
        let mut img = ImageData::new(1, 1, 3);
        img.set(0, 0, 0, 0.5);
        img.set(0, 0, 1, 0.5);
        img.set(0, 0, 2, 0.5);
        let lab = rgb_to_lab(&img);
        let back = lab_to_rgb(&lab);
        for c in 0..3 {
            assert!(
                (back.get(0, 0, c) - 0.5).abs() < 1e-6,
                "通道 {} 差异 {}",
                c,
                (back.get(0, 0, c) - 0.5).abs()
            );
        }
    }

    #[test]
    fn test_roundtrip_red() {
        let mut img = ImageData::new(1, 1, 3);
        img.set(0, 0, 0, 1.0);
        img.set(0, 0, 1, 0.0);
        img.set(0, 0, 2, 0.0);
        let lab = rgb_to_lab(&img);
        let back = lab_to_rgb(&lab);
        // 色域边界颜色往返精度约 1e-4
        for c in 0..3 {
            assert!(
                (back.get(0, 0, c) - img.get(0, 0, c)).abs() < 1e-4,
                "通道 {} 差异 {}",
                c,
                (back.get(0, 0, c) - img.get(0, 0, c)).abs()
            );
        }
    }

    #[test]
    fn test_rgb_to_gray_white() {
        let mut img = ImageData::new(1, 1, 3);
        img.set(0, 0, 0, 1.0);
        img.set(0, 0, 1, 1.0);
        img.set(0, 0, 2, 1.0);
        let gray = rgb_to_gray(&img);
        assert!((gray.get(0, 0, 0) - 1.0).abs() < 1e-10);
    }

    #[test]
    fn test_rgb_to_gray_black() {
        let img = ImageData::new(1, 1, 3);
        let gray = rgb_to_gray(&img);
        assert!(gray.get(0, 0, 0).abs() < 1e-10);
    }

    #[test]
    fn test_rgb_to_gray_equal_channels() {
        let mut img = ImageData::new(2, 2, 3);
        for y in 0..2 {
            for x in 0..2 {
                let v = 0.3;
                img.set(y, x, 0, v);
                img.set(y, x, 1, v);
                img.set(y, x, 2, v);
            }
        }
        let gray = rgb_to_gray(&img);
        for y in 0..2 {
            for x in 0..2 {
                assert!((gray.get(y, x, 0) - 0.3).abs() < 1e-10);
            }
        }
    }
}
