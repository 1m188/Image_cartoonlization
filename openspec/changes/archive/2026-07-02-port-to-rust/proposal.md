## 缘起

MATLAB 卡通化工具已有 Go 移植版。移植到 Rust 可生成零依赖原生二进制，具备更强的安全保证，通过 Rayon 实现无畏并发，并为将来的 WASM/Web 部署奠定基础。Rust 的所有权模型从根本上消除了 Go GC 试图规避但依然存在的内存安全问题，同时双边滤波这类计算密集型模块可达到 C 级性能。

## 变更内容

- 新增 `rs/` 目录，单 Cargo crate 同时包含库和 CLI 二进制
- CLI 界面与 Go 版本完全对等：所有参数一致（`-i`、`-o`、`--edge-thresh`、`--sat`、`--radius`、`--sigma-d`、`--sigma-r`、`--loop`、`--workers`、`-v`）
- 测试驱动开发：每个模块以 `test.jpg` 为夹具先写测试后实现
- 开发纪律：每次代码变更必须通过 `cargo fmt`、`cargo clippy`、`cargo test`
- 扁平 `Vec<f64>` 数据表示 + 跨步索引，内存布局对缓存友好
- Rayon 并行加速双边滤波

## 能力划分

### 新增能力

- `cartoon-pipeline`：核心卡通化管线 — 饱和度调整、双边滤波、Sobel 边缘检测、描边叠加
- `color-conversion`：基于 D65 白点的 sRGB ↔ CIELab 色彩空间转换
- `image-io`：通过 `image` crate 实现 JPEG/PNG 输入输出
- `cli-interface`：基于 clap 的命令行界面，与 Go 版参数对等

### 修改的能力

无 — 没有既有的 Rust 代码可改。

## 影响

- 仓库根下新增 `rs/` 目录（不修改既有代码）
- 依赖：`image`、`clap`（derive）、`rayon`
- 测试夹具：已有 `test.jpg`（2812×1280，3 通道 JPEG）
- 不修改 MATLAB 或 Go 代码库
