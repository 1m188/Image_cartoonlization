## 背景

MATLAB `Image_cartoonlization` 项目已有 Go 移植版在 `go/` 目录下。本文档记录 Rust 移植版的设计方案，该移植版将放置在 `rs/` 目录。算法已明确：饱和度调整、CIELab 空间双边滤波、Sobel 边缘检测、黑色描边叠加。Go 移植版作为行为对等的参考实现。

## 目标 / 非目标

**目标：**
- `rs/` 下单 Cargo crate，包含 `lib.rs`（核心管线）+ `main.rs`（CLI 二进制）
- CLI 界面与 Go 版完全对等：相同的标志、参数范围和帮助文本
- TDD 工作流：先写测试后实现，以 `test.jpg` 为共享夹具
- 扁平 `Vec<f64>` 内部表示 + 跨步索引，内存布局对缓存友好
- Rayon 并行加速双边滤波热路径
- `cargo fmt` + `cargo clippy` 为必要卡控，警告视为错误

**非目标：**
- WASM/Web 目标（以后再说，CLI 优先）
- GPU 加速（与 MATLAB 和 Go 一致，仅 CPU）
- 额外图片格式支持（仅 JPEG/PNG）
- 流式或渐进式处理
- 供外部消费的库 crate（聚焦单二进制功能）

## 决策

### 1. 数据表示：扁平 `Vec<f64>` + 手动索引

**选定方案：** `Vec<f64>` 配合 `(y * width + x) * channels + c` 索引，封装为 `ImageData { width, height, channels, data }`。

**已考虑的替代方案：**
- `Vec<Vec<[f64;3]>>`（Go 风格嵌套）：移植简单但三级指针间接引用破坏缓存局部性。已否决。
- `ndarray::Array3<f64>`：功能强大但对不需要线性代数的问题而言依赖过重。出于简洁性考量已否决。
- 裸 `image::RgbImage`（u8）：无法表示中间浮点值 [0,1]。需要频繁类型转换。已否决。

**理由：** 单次连续分配是高性能图像处理的 Rust 惯用方式。通道交错布局（RGBRGB...）也有利于将来加入 SIMD 加速。

### 2. 模块结构：扁平 `src/` + 功能模块

**选定方案：** `src/` 下九个 `.rs` 文件：

| 文件 | 职责 |
|------|------|
| `lib.rs` | 库 crate 根，声明所有公开模块 |
| `data.rs` | `ImageData` 类型、构造函数、索引辅助函数 |
| `io.rs` | 通过 `image` crate 读写文件，`ImageData` ↔ 文件 |
| `lab.rs` | sRGB ↔ CIELab 转换、RGB→灰度 |
| `saturation.rs` | `(1-s)*gray + s*color` 逐像素变换 |
| `bilateral.rs` | 分发器 + `bilat_color` + `bilat_gray`（Rayon 并行） |
| `edge.rs` | Sobel 边缘检测 + 描边叠加 |
| `cartoon.rs` | `cartoonize()` 管线编排 + `Params` |
| `main.rs` | clap CLI，调用 `cartoonize`，处理 I/O |

**理由：** 镜像 Go 的包结构（一个模块一个关注点），已证明清晰可用。每个文件约 50–100 行，可独立测试。

### 3. 并行策略：Rayon 按行分块

**选定方案：** 在双边滤波的输出缓冲区上使用 `rayon::par_chunks_mut(width * 3)`。

**Go 等价方案：** 协程池配合每 worker 的行范围 — 相同策略，分别符合各自语言习惯。

**理由：** 行级并行避免了伪共享（每行独立计算），无需锁，且 `par_chunks_mut` 天然安全（Rust 借用检查器保证无重叠）。

### 4. 色彩转换：手动实现（不用 `palette` crate）

**选定方案：** 手写 sRGB→线性→XYZ→Lab 转换链，使用 D65 白点常量。从 Go `lab.go` 复制已验证的矩阵。

**已考虑的替代方案：**
- `palette` crate：测试充分但约 100 行的转换增加一个依赖。且与 Go 输出之间的微小 epsilon 差异可能破坏回归测试。
- **理由：** 转换只是简单数学，不需要完整的色彩库。手动实现保证与 Go 逐位一致。

### 5. CLI：`clap` Derive 模式

**选定方案：** `#[derive(Parser)]` 结构体，所有标志与 Go 的 `flag` 包对应。

**理由：** `clap` derive 是 Rust 标准做法。自动生成 `--help`，支持 `-v` / `--verbose`，内置校验（`#[arg(value_parser = ...)]`）。

### 6. 测试策略：TDD + 属性测试 + 集成测试

**选定方案：** 三层测试体系：
1. **单元测试**（各模块内 `#[cfg(test)]`）— 纯函数：Lab 转换矩阵、Sobel 核、饱和度公式。用已知输入/期望输出测试。
2. **属性测试** — 不变量：尺寸保持、值钳制到 [0,1]、色彩转换往返一致性。
3. **集成测试** — `test.jpg` 夹具：加载、处理各管线阶段、验证输出尺寸和属性。

**理由：** 属性测试无需 golden 文件即可捕获回归。管线正确后，可选择性添加基于哈希值的回归测试对标已知正确输出。

### 7. TDD 开发流程

每阶段遵循：写测试 → 写代码 → `cargo fmt` → `cargo clippy` → `cargo test`。不通过失败测试验证，绝不开始写实现代码。

## 风险 / 取舍

| 风险 | 缓解措施 |
|------|---------|
| `image` crate 版本变动 | 固定到稳定且广泛使用的 `0.25` |
| 与 Go 之间色彩转换浮点差异 | 使用 Go `lab.go` 中相同的字面常量；测试中接受 ±1e-12 误差 |
| 双边滤波 O(n²) — 大图慢 | Rayon 并行；尽早 profiling；接受 2812×1280 radius=10 约需数秒 |
| `test.jpg` 为 2812×1280 — 单元测试太慢 | 单元测试用小尺寸合成图像（4×4、3×3）。集成测试仅用一次 `test.jpg` |

## 待解决问题

无 — 探索阶段所有决策已定。
