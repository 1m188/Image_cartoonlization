//! 卡通化管线编排模块。
//!
//! 将各子模块串联成完整的照片卡通化处理流程:
//! 加载 → 饱和度调整 → 双边滤波(N×) → 灰度化 → 边缘检测 → 描边叠加 → 保存。

use crate::bilateral;
use crate::data::ImageData;
use crate::edge;
use crate::lab;
use crate::saturation;
use std::time::{Duration, Instant};

/// 卡通化处理的所有可调参数，默认值与 MATLAB 原版一致。
pub struct Params {
    /// 边缘检测阈值（默认 0.02，范围 0.0~1.0，越小边缘越多）
    pub edge_thresh: f64,
    /// 饱和度增益（默认 2.0，1.0=不变，>1 增强，<1 降低）
    pub sat_scalar: f64,
    /// 双边滤波窗口半径（默认 10，窗口大小 = 2*radius+1）
    pub radius: usize,
    /// 空域标准差 σ_d（默认 3.0，越大空间越模糊）
    pub sigma_d: f64,
    /// 色域标准差 σ_r（默认 0.1，越大颜色越模糊）
    pub sigma_r: f64,
    /// 双边滤波迭代次数（默认 1，越大越模糊）
    pub loop_num: usize,
}

impl Default for Params {
    fn default() -> Self {
        Params {
            edge_thresh: 0.02,
            sat_scalar: 2.0,
            radius: 10,
            sigma_d: 3.0,
            sigma_r: 0.1,
            loop_num: 1,
        }
    }
}

/// 单个处理步骤的结果信息。
pub struct StepResult {
    /// 步骤名称（如 "saturation", "bilateral #1"）
    pub name: String,
    /// 该步骤耗时
    pub duration: Duration,
    /// 步骤细节（如参数值、边缘百分比）
    pub detail: String,
}

/// 执行完整的照片卡通化流程。
///
/// 处理顺序:
/// 1. 饱和度调整
/// 2. 第一次双边滤波
/// 3. 额外的双边滤波迭代（若 loop_num > 1）
/// 4. 对最终滤波结果做灰度化 + Sobel 边缘检测
/// 5. 边缘描边叠加（边缘像素 → 黑色）
/// 6. 最终钳制到 [0,1]
///
/// 返回处理后的图像和各步骤的耗时统计。
pub fn cartoonize(img: &ImageData, p: &Params) -> Result<(ImageData, Vec<StepResult>), String> {
    let mut steps = Vec::new();
    let total_start = Instant::now();

    // 步骤 1: 饱和度调整
    let t0 = Instant::now();
    let mut img = saturation::adjust_saturation(img, p.sat_scalar);
    steps.push(StepResult {
        name: "saturation".to_string(),
        duration: t0.elapsed(),
        detail: format!("scalar={:.2}", p.sat_scalar),
    });

    // 步骤 2: 第一次双边滤波
    let t1 = Instant::now();
    img = bilateral::bilateral_filter(&img, p.radius, p.sigma_d, p.sigma_r);
    steps.push(StepResult {
        name: "bilateral #1".to_string(),
        duration: t1.elapsed(),
        detail: format!(
            "sigma_d={:.2} sigma_r={:.2} radius={}",
            p.sigma_d, p.sigma_r, p.radius
        ),
    });

    // 步骤 3: 额外的双边滤波迭代（若 loop_num > 1）
    for i in 2..=p.loop_num {
        let t = Instant::now();
        img = bilateral::bilateral_filter(&img, p.radius, p.sigma_d, p.sigma_r);
        steps.push(StepResult {
            name: format!("bilateral #{}", i),
            duration: t.elapsed(),
            detail: format!(
                "sigma_d={:.2} sigma_r={:.2} radius={}",
                p.sigma_d, p.sigma_r, p.radius
            ),
        });
    }

    // 步骤 4: 边缘检测（基于最终滤波结果的灰度图）
    let t2 = Instant::now();
    let gray = lab::rgb_to_gray(&img);
    let edge_mask = edge::detect_edges(&gray, p.edge_thresh);

    let mut edge_count = 0;
    for y in 0..edge_mask.height {
        for x in 0..edge_mask.width {
            if edge_mask.get(y, x, 0) > 0.5 {
                edge_count += 1;
            }
        }
    }
    let total_pixels = edge_mask.width * edge_mask.height;
    let edge_pct = edge_count as f64 / total_pixels.max(1) as f64 * 100.0;

    steps.push(StepResult {
        name: "edge detection".to_string(),
        duration: t2.elapsed(),
        detail: format!(
            "sobel threshold={:.4} edges={:.1}%",
            p.edge_thresh, edge_pct
        ),
    });

    // 步骤 5: 边缘叠加
    let t4 = Instant::now();
    img = edge::overlay_edges(&img, &edge_mask);
    steps.push(StepResult {
        name: "edge overlay".to_string(),
        duration: t4.elapsed(),
        detail: format!("{} 边缘像素已叠加", edge_count),
    });

    // 步骤 6: 最终钳制（防止浮点漂移越界）
    for v in img.data.iter_mut() {
        *v = v.clamp(0.0, 1.0);
    }

    let total_dur = total_start.elapsed();

    steps.push(StepResult {
        name: "total".to_string(),
        duration: total_dur,
        detail: String::new(),
    });

    Ok((img, steps))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::io;

    #[test]
    fn test_cartoonize_default_params_dimensions() {
        let test_path = std::path::PathBuf::from("../test.jpg");
        let img = io::load_image(&test_path).expect("加载 test.jpg");
        let (result, _steps) = cartoonize(&img, &Params::default()).expect("卡通化");
        assert_eq!(result.width, img.width);
        assert_eq!(result.height, img.height);
        assert_eq!(result.channels, 3);
        // 所有值应在 [0, 1] 范围内
        for v in &result.data {
            assert!(*v >= 0.0 && *v <= 1.0, "值 {} 超出范围", v);
        }
    }

    #[test]
    fn test_cartoonize_returns_steps() {
        let test_path = std::path::PathBuf::from("../test.jpg");
        let img = io::load_image(&test_path).expect("加载 test.jpg");
        let (_result, steps) = cartoonize(&img, &Params::default()).expect("卡通化");
        assert!(steps.len() >= 4); // 至少包含: 饱和度、双边滤波、边缘、叠加、总计
        let names: Vec<&str> = steps.iter().map(|s| s.name.as_str()).collect();
        assert!(names.contains(&"saturation"));
        assert!(names.contains(&"edge detection"));
        assert!(names.contains(&"edge overlay"));
        assert!(names.contains(&"total"));
    }
}
