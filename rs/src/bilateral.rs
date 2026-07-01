//! 双边滤波模块。
//!
//! 实现空域-色域联合加权的保边平滑滤波。
//! - 彩色图: 在 CIELab 色彩空间中执行，σ_r 放大 100 倍以匹配 L∈[0,100]
//! - 灰度图: 直接在单通道上执行
//! - 使用 Rayon 按行并行加速

use crate::data::ImageData;
use crate::lab;

use rayon::prelude::*;

/// 在 CIELab 空间中对彩色图像做双边滤波（行级并行）。
///
/// 与 MATLAB `bfltColor.m` 一致: σ_r 在 Lab 空间乘以 100，
/// 使默认 σ_r=0.1 对应有效 σ_r=10（与 L 通道 [0,100] 范围匹配）。
fn bilat_color(lab: &ImageData, w: usize, sigma_d: f64, sigma_r: f64) -> ImageData {
    let height = lab.height;
    let width = lab.width;
    let kernel_size = 2 * w + 1;

    // 预计算空域高斯核 G(x,y) = exp(-(x²+y²)/(2σ_d²))
    let domain_kernel: Vec<Vec<f64>> = (0..kernel_size)
        .map(|ki| {
            let di = ki as isize - w as isize;
            (0..kernel_size)
                .map(|kj| {
                    let dj = kj as isize - w as isize;
                    (-(di * di + dj * dj) as f64 / (2.0 * sigma_d * sigma_d)).exp()
                })
                .collect()
        })
        .collect();

    // CIELab 空间中的有效 σ_r（与 MATLAB/Go 一致）
    let sigma_r = 100.0 * sigma_r;

    let mut data = vec![0.0f64; width * height * 3];

    // 按行并行处理，每行独立计算
    data.par_chunks_mut(width * 3)
        .enumerate()
        .for_each(|(y, row)| {
            for x in 0..width {
                // 窗口边界（钳制到图像边缘）
                let i_min = y.saturating_sub(w);
                let i_max = (y + w).min(height - 1);
                let j_min = x.saturating_sub(w);
                let j_max = (x + w).min(width - 1);

                let cur_l = lab.get(y, x, 0);
                let cur_a = lab.get(y, x, 1);
                let cur_b = lab.get(y, x, 2);

                let mut sum_l = 0.0;
                let mut sum_a = 0.0;
                let mut sum_b = 0.0;
                let mut sum_w = 0.0;

                for pi in i_min..=i_max {
                    for pj in j_min..=j_max {
                        // 色域权重: exp(-ΔE²/(2σ_r²))，其中 ΔE² = ΔL²+Δa²+Δb²
                        let dl = lab.get(pi, pj, 0) - cur_l;
                        let da = lab.get(pi, pj, 1) - cur_a;
                        let db = lab.get(pi, pj, 2) - cur_b;
                        let range_diff = dl * dl + da * da + db * db;
                        let range_w = (-range_diff / (2.0 * sigma_r * sigma_r)).exp();

                        // 空域权重从预计算核中查表
                        let dw = domain_kernel[pi + w - y][pj + w - x];
                        let weight = range_w * dw;

                        sum_l += weight * lab.get(pi, pj, 0);
                        sum_a += weight * lab.get(pi, pj, 1);
                        sum_b += weight * lab.get(pi, pj, 2);
                        sum_w += weight;
                    }
                }

                let base = x * 3;
                if sum_w > 0.0 {
                    row[base] = sum_l / sum_w;
                    row[base + 1] = sum_a / sum_w;
                    row[base + 2] = sum_b / sum_w;
                } else {
                    row[base] = cur_l;
                    row[base + 1] = cur_a;
                    row[base + 2] = cur_b;
                }
            }
        });

    ImageData {
        width,
        height,
        channels: 3,
        data,
    }
}

/// 对单通道灰度图做双边滤波（行级并行）。
fn bilat_gray(img: &ImageData, w: usize, sigma_d: f64, sigma_r: f64) -> ImageData {
    let height = img.height;
    let width = img.width;
    let kernel_size = 2 * w + 1;

    // 预计算空域高斯核
    let domain_kernel: Vec<Vec<f64>> = (0..kernel_size)
        .map(|ki| {
            let di = ki as isize - w as isize;
            (0..kernel_size)
                .map(|kj| {
                    let dj = kj as isize - w as isize;
                    (-(di * di + dj * dj) as f64 / (2.0 * sigma_d * sigma_d)).exp()
                })
                .collect()
        })
        .collect();

    let mut data = vec![0.0f64; width * height];

    data.par_chunks_mut(width).enumerate().for_each(|(y, row)| {
        for x in 0..width {
            let i_min = y.saturating_sub(w);
            let i_max = (y + w).min(height - 1);
            let j_min = x.saturating_sub(w);
            let j_max = (x + w).min(width - 1);

            let cur_val = img.get(y, x, 0);
            let mut sum_val = 0.0;
            let mut sum_w = 0.0;

            for pi in i_min..=i_max {
                for pj in j_min..=j_max {
                    let diff = img.get(pi, pj, 0) - cur_val;
                    let range_w = (-(diff * diff) / (2.0 * sigma_r * sigma_r)).exp();
                    let dw = domain_kernel[pi + w - y][pj + w - x];
                    let weight = range_w * dw;

                    sum_val += weight * img.get(pi, pj, 0);
                    sum_w += weight;
                }
            }

            if sum_w > 0.0 {
                row[x] = sum_val / sum_w;
            } else {
                row[x] = cur_val;
            }
        }
    });

    ImageData {
        width,
        height,
        channels: 1,
        data,
    }
}

/// 双边滤波器入口。
///
/// 根据通道数自动选择灰度或彩色路径。
/// 彩色路径内部完成 RGB→Lab→滤波→RGB 的全流程。
///
/// 当 σ_d ≤ 0 或 σ_r ≤ 0 时，滤波器退化为直通（返回输入副本），
/// 避免除零产生 NaN。
pub fn bilateral_filter(img: &ImageData, w: usize, sigma_d: f64, sigma_r: f64) -> ImageData {
    // sigma 为零或非法值时退化为直通，避免 0/0 → NaN
    if !(sigma_d.is_finite() && sigma_r.is_finite() && sigma_d > 0.0 && sigma_r > 0.0) {
        return img.clone();
    }
    if img.channels == 1 {
        bilat_gray(img, w, sigma_d, sigma_r)
    } else {
        let lab = lab::rgb_to_lab(img);
        let filtered = bilat_color(&lab, w, sigma_d, sigma_r);
        lab::lab_to_rgb(&filtered)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_radius_zero_identity() {
        let mut img = ImageData::new(3, 3, 3);
        for y in 0..3 {
            for x in 0..3 {
                img.set(y, x, 0, 0.2);
                img.set(y, x, 1, 0.5);
                img.set(y, x, 2, 0.8);
            }
        }
        let result = bilateral_filter(&img, 0, 3.0, 0.1);
        for y in 0..3 {
            for x in 0..3 {
                for c in 0..3 {
                    assert!(
                        (result.get(y, x, c) - img.get(y, x, c)).abs() < 1e-6,
                        "差异 at ({},{},{})",
                        y,
                        x,
                        c
                    );
                }
            }
        }
    }

    #[test]
    fn test_dimensions_preserved() {
        let img = ImageData::new(4, 6, 3);
        let result = bilateral_filter(&img, 2, 3.0, 0.1);
        assert_eq!(result.width, 4);
        assert_eq!(result.height, 6);
        assert_eq!(result.channels, 3);
    }

    #[test]
    fn test_large_sigma_r_smooths() {
        let mut img = ImageData::new(5, 5, 3);
        // 创建锐利边界
        for y in 0..5 {
            for x in 0..5 {
                let v = if x < 2 { 0.2 } else { 0.8 };
                img.set(y, x, 0, v);
                img.set(y, x, 1, v);
                img.set(y, x, 2, v);
            }
        }
        // 极大 σ_r 时行为近似高斯模糊，中心应介于边缘两侧之间
        let result = bilateral_filter(&img, 1, 10.0, 1000.0);
        for c in 0..3 {
            let center = result.get(2, 2, c);
            assert!(center > 0.2 && center < 0.8, "c={} center={}", c, center);
        }
    }

    #[test]
    fn test_grayscale_path() {
        let mut gray = ImageData::new(3, 3, 1);
        for y in 0..3 {
            for x in 0..3 {
                gray.set(y, x, 0, 0.5);
            }
        }
        let result = bilateral_filter(&gray, 1, 3.0, 0.1);
        assert_eq!(result.channels, 1);
        assert_eq!(result.width, 3);
        assert_eq!(result.height, 3);
    }

    #[test]
    fn test_sigma_zero_returns_clone() {
        let mut img = ImageData::new(2, 2, 3);
        img.set(0, 0, 0, 0.7);
        img.set(1, 1, 1, 0.3);
        // σ_r = 0 应触发直通
        let result = bilateral_filter(&img, 5, 3.0, 0.0);
        assert_eq!(result.width, img.width);
        assert_eq!(result.height, img.height);
        assert!((result.get(0, 0, 0) - 0.7).abs() < 1e-10);
        assert!((result.get(1, 1, 1) - 0.3).abs() < 1e-10);
        // σ_d = 0 也应触发直通
        let result2 = bilateral_filter(&img, 5, 0.0, 0.1);
        assert!((result2.get(0, 0, 0) - 0.7).abs() < 1e-10);
        assert!((result2.get(1, 1, 1) - 0.3).abs() < 1e-10);
    }
}
