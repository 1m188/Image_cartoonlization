## 1. 项目脚手架

- [x] 1.1 在 `rs/` 初始化 Cargo 项目，依赖：`image 0.25`、`clap 4`（derive）、`rayon 1.10`
- [x] 1.2 配置 `[dev-dependencies]`（初始加入 `tempfile 3`，review 阶段发现未使用后移除）
- [x] 1.3 创建 `src/data.rs`，包含 `ImageData { width, height, channels, data: Vec<f64> }` 和索引辅助函数
- [x] 1.4 编写 ImageData 构造、索引计算和索引访问的 TDD 测试
- [x] 1.5 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 1.6 MPLR 代码审查：检查正确性、风格、边界情况、逻辑错误 — 在继续前修复所有问题

## 2. 图像读/写

- [x] 2.1 编写失败测试：加载 `test.jpg` → 验证宽 2812 高 1280 通道 3；不存在文件 → 报错；GIF → 报错
- [x] 2.2 实现 `src/io.rs` 的 `load_image(path) -> Result<ImageData>`（使用 `image` crate）
- [x] 2.3 编写失败测试：保存为 PNG 后重新加载 → 像素匹配；保存 JPEG → 有效文件；保存为 `.bmp` → 报错
- [x] 2.4 实现 `save_image(path, img)`（使用 `image` crate，JPEG 质量 95）
- [x] 2.5 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 2.6 MPLR 代码审查 — 在继续前修复所有问题

## 3. 色彩空间转换

- [x] 3.1 编写失败测试：白色→Lab(100,0,0)、黑色→Lab(0,0,0)、红色→正 a*、往返一致性、灰度转换
- [x] 3.2 实现 `src/lab.rs` 的 sRGB↔Lab 转换（sRGB→线性→XYZ→Lab 链，D65 白点）和 `rgb_to_gray()`
- [x] 3.3 验证所有转换矩阵常量与 Go 参考实现 `go/cartoon/lab.go` 一致
- [x] 3.4 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 3.5 MPLR 代码审查 — 在继续前修复所有问题

## 4. 饱和度调整

- [x] 4.1 编写失败测试：scalar=1→不变；scalar=0→灰度（所有通道相等）；scalar=2→方差增大；越界→钳制
- [x] 4.2 实现 `src/saturation.rs`，公式 `(1-s)*gray + s*color`
- [x] 4.3 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 4.4 MPLR 代码审查 — 在继续前修复所有问题

## 5. 边缘检测

- [x] 5.1 编写失败测试：均匀图像→无边；合成渐变→边界处有边；较低阈值→更多边
- [x] 5.2 实现 `src/edge.rs` 的 `detect_edges()`（3×3 Sobel 核）
- [x] 5.3 编写叠加测试：全 1 掩码→全黑图像；全 0 掩码→不变；混合掩码→正确合成
- [x] 5.4 实现 `src/edge.rs` 的 `overlay_edges()`
- [x] 5.5 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 5.6 MPLR 代码审查 — 在继续前修复所有问题

## 6. 双边滤波

- [x] 6.1 编写失败测试：radius=0→不变；极大 sigma_r→类高斯模糊；尺寸保持；两次运行结果完全一致
- [x] 6.2 实现 `src/bilateral.rs`，分发器路由到彩色或灰度路径
- [x] 6.3 在 CIELab 空间实现彩色双边滤波，sigma_r *= 100，与 MATLAB/Go 行为一致
- [x] 6.4 实现灰度双边滤波
- [x] 6.5 添加 Rayon 每行 `par_chunks_mut` 并行
- [x] 6.6 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 6.7 MPLR 代码审查 — 在继续前修复所有问题

## 7. 管线集成

- [x] 7.1 编写失败测试：`test.jpg` 默认参数 → 相同尺寸、值在 [0,1] 内；返回耗时元数据
- [x] 7.2 实现 `src/cartoon.rs` 的 `Params` 结构体和 `cartoonize()` 管线编排器
- [x] 7.3 串联管线：饱和度 → 双边(N×) → 灰度化 → 边缘检测 → 叠加 → 最终钳制
- [x] 7.4 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 7.5 MPLR 代码审查 — 在继续前修复所有问题

## 8. CLI 界面

- [x] 8.1 编写失败测试：`--help` 打印用法；缺少 `-i` 非零退出；合法参数解析；`.bmp` 输出 → 报错
- [x] 8.2 实现 `src/main.rs`，clap derive 结构体匹配所有 Go 版参数
- [x] 8.3 添加参数范围校验，提供清晰错误信息
- [x] 8.4 串联 `main.rs`：I/O → cartoonize → I/O
- [x] 8.5 添加详细模式（`-v`）输出，显示每阶段耗时
- [x] 8.6 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 8.7 MPLR 代码审查 — 在继续前修复所有问题

## 9. 最终验证

- [x] 9.1 运行 `cargo build --release` — 零警告
- [x] 9.2 用默认参数处理 `test.jpg`，验证输出文件存在且有效
- [x] 9.3 运行 CLI 参数全矩阵：edge-thresh 0.005/0.05、sat 1/3、loop 1/3 等
- [x] 9.4 验证：`cargo fmt --check && cargo clippy -- -D warnings && cargo test` — 全部绿色

## 10. 全局代码审查循环

- [x] 10.1 对整个代码库进行全方位 MPLR 审查：检查错误、逻辑问题、矛盾、边界情况、性能问题和规范合规性
- [x] 10.2 如发现问题：修复（eprintln、最终钳制、sat 校验、规范容差），然后回到 10.1
- [x] 10.3 未发现更多问题：审查循环结束，项目完成
