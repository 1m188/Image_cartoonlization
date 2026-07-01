# Image_cartoonlization_rs

根据 [Image_cartoonlization](../) 项目做的 Rust 移植版。

## 编译

```bash
cd rs
cargo build --release
```

## 用法

```bash
./target/release/image-cartoonlization -i ../test.jpg -o cartoon.png
```

### 参数说明

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-i <路径>` | 必填 | 输入图片路径（JPEG / PNG） |
| `-o <路径>` | `cartoon.png` | 输出图片路径 |
| `--edge-thresh` | `0.02` | 边缘检测阈值（0.0~1.0，越小边缘越多） |
| `--sat` | `2.0` | 饱和度增益（1.0=不变） |
| `--radius` | `10` | 双边滤波窗口半径（1~50） |
| `--sigma-d` | `3.0` | 空域标准差 |
| `--sigma-r` | `0.1` | 色域标准差 |
| `--loop` | `1` | 双边滤波迭代次数（1~10） |
| `--workers` | `0` | 并行线程数（0=自动） |
| `-v` | 否 | 详细输出模式 |

### 示例

```bash
# 基本用法
image-cartoonlization -i photo.jpg -o cartoon.png

# 增加边缘密度
image-cartoonlization -i photo.jpg --edge-thresh 0.005

# 更模糊的效果
image-cartoonlization -i photo.jpg --radius 15 --loop 3

# 鲜艳 + 弱描边
image-cartoonlization -i photo.jpg --sat 3 --edge-thresh 0.05
```

## 处理流程

```
加载 → 饱和度调整 → 双边滤波（CIELab 空间，保边平滑）
    → Sobel 边缘检测 → 黑色描边叠加 → 保存
```

## 测试

```bash
cargo test
```

## 技术栈

- `image 0.25` — JPEG/PNG 编解码
- `clap 4` — 命令行参数解析
- `rayon 1.10` — 并行计算（双边滤波按行并行）
