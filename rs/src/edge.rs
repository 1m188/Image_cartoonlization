//! 边缘检测与叠加模块。
//!
//! 使用 3×3 Sobel 算子检测灰度图中的边缘，然后将边缘掩码
//! 叠加到模糊图像上形成黑色描边效果。

use crate::data::ImageData;

/// 使用 Sobel 算子检测边缘。
///
/// 对灰度图做 3×3 Sobel 水平/垂直卷积，取梯度幅值 G = √(Gx² + Gy²)。
/// 若 G > threshold 则标记为边缘像素（值 1.0），否则为 0.0。
///
/// 边界一列/一行不做处理（默认为非边缘），当图像小于 3×3 时返回全零掩码。
pub fn detect_edges(gray: &ImageData, threshold: f64) -> ImageData {
    let height = gray.height;
    let width = gray.width;
    let mut data = vec![0.0f64; width * height];

    // Sobel 核（Y 轴指向下方，与 Go 参考一致）
    let sobel_x: [[f64; 3]; 3] = [[-1.0, 0.0, 1.0], [-2.0, 0.0, 2.0], [-1.0, 0.0, 1.0]];
    let sobel_y: [[f64; 3]; 3] = [[1.0, 2.0, 1.0], [0.0, 0.0, 0.0], [-1.0, -2.0, -1.0]];

    for y in 1..height.saturating_sub(1) {
        for x in 1..width.saturating_sub(1) {
            let mut gx = 0.0;
            let mut gy = 0.0;
            for di in 0..3 {
                for dj in 0..3 {
                    let val = gray.get(y + di - 1, x + dj - 1, 0);
                    gx += val * sobel_x[di][dj];
                    gy += val * sobel_y[di][dj];
                }
            }
            let g = (gx * gx + gy * gy).sqrt();
            if g > threshold {
                data[y * width + x] = 1.0;
            }
        }
    }

    ImageData {
        width,
        height,
        channels: 1,
        data,
    }
}

/// 将边缘掩码叠加到模糊图像上。
///
/// 掩码值为 1.0 的像素变为黑色 (0,0,0)，
/// 其余像素保留模糊值并钳制到 [0,1]。
///
/// 等价于 MATLAB 中的 `img - img .* edge_mask`。
pub fn overlay_edges(blurred: &ImageData, edge_mask: &ImageData) -> ImageData {
    let height = blurred.height;
    let width = blurred.width;
    let mut data = vec![0.0f64; width * height * 3];

    for y in 0..height {
        for x in 0..width {
            let base = (y * width + x) * 3;
            if edge_mask.get(y, x, 0) > 0.5 {
                // 边缘像素 → 黑色
                data[base] = 0.0;
                data[base + 1] = 0.0;
                data[base + 2] = 0.0;
            } else {
                // 非边缘像素 → 保留模糊值
                data[base] = blurred.get(y, x, 0).clamp(0.0, 1.0);
                data[base + 1] = blurred.get(y, x, 1).clamp(0.0, 1.0);
                data[base + 2] = blurred.get(y, x, 2).clamp(0.0, 1.0);
            }
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
    fn test_uniform_image_no_edges() {
        let mut gray = ImageData::new(5, 5, 1);
        for y in 0..5 {
            for x in 0..5 {
                gray.set(y, x, 0, 0.5);
            }
        }
        let edges = detect_edges(&gray, 0.1);
        for y in 1..4 {
            for x in 1..4 {
                assert_eq!(edges.get(y, x, 0), 0.0);
            }
        }
    }

    #[test]
    fn test_sharp_boundary_detected() {
        let mut gray = ImageData::new(5, 5, 1);
        for y in 0..5 {
            for x in 0..5 {
                gray.set(y, x, 0, if x < 2 { 0.0 } else { 1.0 });
            }
        }
        let edges = detect_edges(&gray, 0.3);
        // 边缘应出现在 x=2 处（黑白交界）
        let mut edge_count = 0;
        for y in 1..4 {
            for x in 1..4 {
                if edges.get(y, x, 0) > 0.5 {
                    edge_count += 1;
                }
            }
        }
        assert!(edge_count > 0, "边界处未检测到边缘");
    }

    #[test]
    fn test_lower_threshold_more_edges() {
        let mut gray = ImageData::new(5, 5, 1);
        for y in 0..5 {
            for x in 0..5 {
                gray.set(y, x, 0, (y * 5 + x) as f64 / 25.0);
            }
        }
        let count = |t: f64| -> usize {
            let edges = detect_edges(&gray, t);
            let mut n = 0;
            for y in 0..5 {
                for x in 0..5 {
                    if edges.get(y, x, 0) > 0.5 {
                        n += 1;
                    }
                }
            }
            n
        };
        assert!(count(0.01) >= count(0.5));
    }

    #[test]
    fn test_overlay_all_edges_black() {
        let blurred = ImageData::new(3, 3, 3);
        let mut mask = ImageData::new(3, 3, 1);
        for y in 0..3 {
            for x in 0..3 {
                mask.set(y, x, 0, 1.0);
            }
        }
        let result = overlay_edges(&blurred, &mask);
        for y in 0..3 {
            for x in 0..3 {
                for c in 0..3 {
                    assert_eq!(result.get(y, x, c), 0.0);
                }
            }
        }
    }

    #[test]
    fn test_overlay_no_edges_identity() {
        let mut blurred = ImageData::new(3, 3, 3);
        for y in 0..3 {
            for x in 0..3 {
                blurred.set(y, x, 0, 0.7);
                blurred.set(y, x, 1, 0.5);
                blurred.set(y, x, 2, 0.3);
            }
        }
        let mask = ImageData::new(3, 3, 1); // 全零 = 无边
        let result = overlay_edges(&blurred, &mask);
        for y in 0..3 {
            for x in 0..3 {
                for c in 0..3 {
                    assert_eq!(result.get(y, x, c), blurred.get(y, x, c));
                }
            }
        }
    }

    #[test]
    fn test_overlay_mixed() {
        let mut blurred = ImageData::new(2, 2, 3);
        for y in 0..2 {
            for x in 0..2 {
                blurred.set(y, x, 0, 0.9);
                blurred.set(y, x, 1, 0.8);
                blurred.set(y, x, 2, 0.6);
            }
        }
        let mut mask = ImageData::new(2, 2, 1);
        mask.set(0, 0, 0, 1.0); // 边缘
        mask.set(0, 1, 0, 0.0); // 非边缘
        mask.set(1, 0, 0, 0.0); // 非边缘
        mask.set(1, 1, 0, 1.0); // 边缘

        let result = overlay_edges(&blurred, &mask);
        // 边缘像素应为黑色
        assert_eq!(result.get(0, 0, 0), 0.0);
        assert_eq!(result.get(0, 0, 1), 0.0);
        assert_eq!(result.get(0, 0, 2), 0.0);
        assert_eq!(result.get(1, 1, 0), 0.0);
        assert_eq!(result.get(1, 1, 1), 0.0);
        assert_eq!(result.get(1, 1, 2), 0.0);
        // 非边缘像素应保留原值
        assert_eq!(result.get(0, 1, 0), 0.9);
        assert_eq!(result.get(0, 1, 1), 0.8);
        assert_eq!(result.get(0, 1, 2), 0.6);
    }
}
