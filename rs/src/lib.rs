//! `image-cartoonlization` — 照片卡通化 Rust 移植版。
//!
//! 本项目从 MATLAB 原版 (`cartoonlize.m`) 移植到 Rust，
//! 参考 Go 移植版 (`go/`)。提供与 Go 版功能对等的 CLI 工具。
//!
//! ## 处理流水线
//!
//! ```text
//! 加载图片 → 饱和度调整 → 双边滤波 (CIELab) → Sobel 边缘检测 → 描边叠加 → 保存
//! ```
//!
//! ## 模块
//!
//! - `data` — 图像数据容器 `ImageData`
//! - `io` — JPEG/PNG 读写
//! - `lab` — sRGB ↔ CIELab 色彩空间转换
//! - `saturation` — 饱和度调整（线性插值）
//! - `edge` — Sobel 边缘检测与描边叠加
//! - `bilateral` — 双边滤波（Rayon 并行）
//! - `cartoon` — 卡通化管线编排

pub mod bilateral;
pub mod cartoon;
pub mod data;
pub mod edge;
pub mod io;
pub mod lab;
pub mod saturation;
