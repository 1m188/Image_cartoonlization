//! 照片卡通化命令行工具。
//!
//! 将照片转换为卡通风格。通过饱和度增强、双边滤波保边平滑、
//! Sobel 边缘检测描边等技术模拟手绘卡通效果。
//!
//! 用法:
//!   image-cartoonlization -i <输入图片> [-o <输出图片>] [选项]
//!
//! 必选参数:
//!   -i <路径>            输入图片路径（支持 JPEG / PNG 格式）
//!
//! 可选参数:
//!   -o <路径>            输出图片路径（默认: cartoon.png）
//!   --edge-thresh <值>   边缘检测阈值（默认: 0.02，范围: 0.0~1.0）
//!   --sat <值>           饱和度增益（默认: 2.0）
//!   --radius <值>        双边滤波窗口半径（默认: 10，范围: 1~50）
//!   --sigma-d <值>       空域标准差（默认: 3.0）
//!   --sigma-r <值>       色域标准差（默认: 0.1）
//!   --loop <值>          双边滤波迭代次数（默认: 1，范围: 1~10）
//!   --workers <值>       并行线程数（默认: 0 = 自动）
//!   -v                   详细输出模式
//!   -h, --help           显示帮助信息

use clap::Parser;
use image_cartoonlization::cartoon::{self, Params};
use image_cartoonlization::io;
use std::path::{Path, PathBuf};
use std::time::Instant;

#[derive(Parser, Debug)]
#[command(
    name = "image-cartoonlization",
    version = "1.0.0",
    about = "照片卡通化 — 从 MATLAB 移植到 Rust"
)]
struct Cli {
    /// 输入图片路径（JPEG 或 PNG）
    #[arg(short = 'i', long, value_hint = clap::ValueHint::FilePath)]
    input: String,

    /// 输出图片路径（默认: cartoon.png）
    #[arg(short = 'o', long, default_value = "cartoon.png")]
    output: String,

    /// 边缘检测阈值（0.0~1.0，越小边缘越多）
    #[arg(long = "edge-thresh", default_value = "0.02")]
    edge_thresh: f64,

    /// 饱和度增益（1.0=不变，>1 增强，<1 降低）
    #[arg(long = "sat", default_value = "2.0")]
    sat: f64,

    /// 双边滤波窗口半径（1~50）
    #[arg(long = "radius", default_value = "10")]
    radius: usize,

    /// 空域标准差 σ_d
    #[arg(long = "sigma-d", default_value = "3.0")]
    sigma_d: f64,

    /// 色域标准差 σ_r
    #[arg(long = "sigma-r", default_value = "0.1")]
    sigma_r: f64,

    /// 双边滤波迭代次数（1~10）
    #[arg(long = "loop", default_value = "1")]
    loop_num: usize,

    /// 并行线程数（0 = 自动使用 CPU 核数）
    #[arg(long = "workers", default_value = "0")]
    workers: usize,

    /// 详细输出模式，打印每步处理耗时
    #[arg(short = 'v', long)]
    verbose: bool,
}

/// 验证参数范围和输出格式。
fn validate(params: &Params, out_path: &Path) -> Result<(), String> {
    if params.edge_thresh < 0.0 || params.edge_thresh > 1.0 {
        return Err("--edge-thresh 必须在 0.0 ~ 1.0 之间".to_string());
    }
    if params.radius < 1 || params.radius > 50 {
        return Err("--radius 必须在 1 ~ 50 之间".to_string());
    }
    if params.sigma_d <= 0.0 {
        return Err("--sigma-d 必须大于 0".to_string());
    }
    if params.sigma_r <= 0.0 {
        return Err("--sigma-r 必须大于 0".to_string());
    }
    if params.loop_num < 1 || params.loop_num > 10 {
        return Err("--loop 必须在 1 ~ 10 之间".to_string());
    }
    let ext = out_path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_lowercase();
    if ext != "jpg" && ext != "jpeg" && ext != "png" {
        return Err(format!(
            "不支持的输出后缀: .{} (仅支持 .jpg, .jpeg, .png)",
            ext
        ));
    }
    Ok(())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let cli = Cli::parse();

    let input_path = PathBuf::from(&cli.input);
    let output_path = PathBuf::from(&cli.output);

    let params = Params {
        edge_thresh: cli.edge_thresh,
        sat_scalar: cli.sat,
        radius: cli.radius,
        sigma_d: cli.sigma_d,
        sigma_r: cli.sigma_r,
        loop_num: cli.loop_num,
    };

    if let Err(e) = validate(&params, &output_path) {
        eprintln!("错误: {}", e);
        std::process::exit(1);
    }

    // 设置 Rayon 线程池大小
    if cli.workers > 0 {
        rayon::ThreadPoolBuilder::new()
            .num_threads(cli.workers)
            .build_global()
            .map_err(|e| format!("设置线程池失败: {}", e))?;
    }

    let load_start = Instant::now();
    let img = io::load_image(&input_path).map_err(|e| format!("加载错误: {}", e))?;
    let load_dur = load_start.elapsed();

    if cli.verbose {
        eprintln!(
            "[加载] {} | {}x{} | {:?}",
            input_path.file_name().unwrap_or_default().to_string_lossy(),
            img.width,
            img.height,
            load_dur
        );
    }

    let (result, steps) = cartoon::cartoonize(&img, &params)?;

    if cli.verbose {
        for step in &steps {
            if step.name == "total" {
                continue;
            }
            eprintln!("[{}] | {:?} | {}", step.name, step.duration, step.detail);
        }
    }

    let save_start = Instant::now();
    io::save_image(&output_path, &result).map_err(|e| format!("保存错误: {}", e))?;
    let save_dur = save_start.elapsed();

    if cli.verbose {
        eprintln!(
            "[保存] {} | {:?}",
            output_path
                .file_name()
                .unwrap_or_default()
                .to_string_lossy(),
            save_dur
        );
    }

    for step in &steps {
        if step.name == "total" {
            println!("总耗时: {:?}", step.duration);
        }
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_cli_help() {
        let result = Cli::try_parse_from(["cartoon", "--help"]);
        assert!(result.is_err()); // --help 会打印并退出，clap 在测试中视为错误
    }

    #[test]
    fn test_cli_missing_input() {
        let result = Cli::try_parse_from(["cartoon"]);
        assert!(result.is_err());
    }

    #[test]
    fn test_cli_valid_args() {
        let cli = Cli::try_parse_from([
            "cartoon",
            "-i",
            "test.jpg",
            "-o",
            "out.png",
            "--edge-thresh",
            "0.1",
            "--sat",
            "1.5",
        ])
        .expect("解析合法参数");
        assert_eq!(cli.input, "test.jpg");
        assert_eq!(cli.output, "out.png");
        assert_eq!(cli.edge_thresh, 0.1);
        assert_eq!(cli.sat, 1.5);
    }

    #[test]
    fn test_validate_unsupported_output() {
        let params = Params::default();
        let result = validate(&params, &PathBuf::from("out.bmp"));
        assert!(result.is_err());
    }

    #[test]
    fn test_validate_edge_thresh_out_of_range() {
        let params = Params {
            edge_thresh: 1.5,
            ..Params::default()
        };
        let result = validate(&params, &PathBuf::from("out.png"));
        assert!(result.is_err());
    }
}
