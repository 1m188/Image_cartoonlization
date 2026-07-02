## Context

MATLAB `Image_cartoonlization` 项目已有 Go (`go/`) 和 Rust (`rs/`) 两个移植版本。本文档设计 C# (.NET 10) Native AOT 移植，放置在 `cs/` 目录下。算法已充分理解：饱和度调整 → CIELab 空间双边滤波 → Sobel 边缘检测 → 黑色描边叠加。Go 版本作为行为参考实现。

## Goals / Non-Goals

**Goals:**
- 单个 .NET 10 控制台项目（`ImageCartoonlization`）+ xUnit 测试项目（`ImageCartoonlization.Tests`）
- CLI 接口与 Go/Rust 版本完全一致：相同的参数名、参数范围、帮助文本
- TDD 工作流：每个模块先写测试再写实现，代码变更后执行 `dotnet format` → `dotnet build /warnaserror` → `dotnet test`
- 一维 `float[]` 内部表示，`[y * width * channels + x * channels + c]` 索引方式，缓存友好
- `Parallel.For` 实现双边滤波行级并行
- Native AOT 发布：`dotnet publish -c Release /p:PublishAot=true` 生成单一原生二进制
- 所有代码注释和文档采用简体中文

**Non-Goals:**
- GPU 加速（仅 CPU，与 MATLAB/Go/Rust 一致）
- WASM / Web 部署目标
- JPEG/PNG 以外的图片格式支持
- 流式或渐进式处理
- 外部库级可复用性（侧重单一二进制交付）

## Decisions

### 1. 数据表示：一维 `float[]` + 手动索引

**选择：** `float[]` 数组配合 `(y * width + x) * channels + c` 索引，封装在 `ImageData { width, height, channels, data }` 结构体中。

**备选考虑：**
- `float[][]` 锯齿数组：与 Go 的 `[][][]float64` 类似，多次指针跳转破坏缓存局部性。拒绝。
- `float[,]` 多维数组：语法便利但性能不如一维，且不与 `Parallel.For` 的 `Span<float>` 良好兼容。拒绝。
- `double` (64位)：MATLAB/Go/Rust 均用 f64，但对 8-bit 图像处理 `float` 完全够用，内存减半，缓存更优。选择 `float`。

**理由：** 单次连续分配是高性能图像处理的惯用手段。通道交错排列（RGBRGB...）也为日后 SIMD 加速预留空间。`float` (32位) 在 .NET 10 的 `Vector<T>` 中天然友好（256-bit 寄存器可放 8 个 float vs 4 个 double）。

### 2. 模块结构：8 个文件平铺在项目中

**选择：** 8 个 `.cs` 文件在 `cs/ImageCartoonlization/` 下：

| 文件 | 职责 |
|------|------|
| `Program.cs` | CLI 入口、手动参数解析、`-h` 帮助、`-v` 详细输出 |
| `ImageData.cs` | `ImageData` 结构体、构造函数、索引辅助方法 |
| `ImageIO.cs` | 通过 SixLabors.ImageSharp 加载/保存，`ImageData` ↔ 文件 |
| `LabColor.cs` | sRGB ↔ CIELab 转换、`RgbToGray()` |
| `Saturation.cs` | `(1-s)*gray + s*color` 逐像素变换 |
| `BilateralFilter.cs` | 调度器 + 彩色双边滤波（CIELab 空间）+ 灰度双边滤波，含 `Parallel.For` 并行 |
| `EdgeDetection.cs` | Sobel 3×3 边缘检测 + 黑色描边叠加 |
| `CartoonPipeline.cs` | `Cartoonize()` 管线编排器 + `Params` 记录类型 |

**理由：** 镜像 Rust 的模块结构（一个关切一个文件），已验证简洁可用。每个文件 40-180 行，可独立进行单元测试。

### 3. 并行策略：`Parallel.For` 行级并行

**选择：** `System.Threading.Tasks.Parallel.For(0, height, i => { /* 处理第 i 行 */ })`。

**Go 等价：** goroutine 池 + 行范围分配 — 相同策略，语言各自的惯用方式。

**Rust 等价：** `rayon::par_chunks_mut()` — 相同策略。

**理由：** 行级并行避免伪共享（每行独立计算），无需锁，`Parallel.For` 内置于 BCL 无额外依赖。.NET 10 的 `Parallel.For` 已高度优化，内置工作窃取调度。

### 4. 色域转换：手动实现（不使用 `Colourful` 等 NuGet 库）

**选择：** 手写 sRGB→Linear→XYZ→Lab 转换链，使用 D65 白点常量。从 Go `cartoon/lab.go` 复制已验证的转换矩阵。

**备选考虑：**
- `SixLabors.ImageSharp` 内置简单转换：没有完整的 CIELab 支持。
- `Colourful` NuGet 包：功能丰富但增加不必要的依赖，且细微浮点差异可能导致与 Go 回归测试不一致。

**理由：** 转换是 ~100 行的简单数学运算，不需要完整的色彩库。手动实现保证与 Go 输出的位级一致性。

### 5. CLI 解析：手动实现（不使用 `System.CommandLine` 或 `Spectre.Console.Cli`）

**选择：** 使用 `string[] args` 遍历 + 手动解析，完全不依赖 CLI 库。

**Rust/Golang 等价：** Rust 用 `clap` derive，Go 用 `flag` stdlib — C# 选择手动解析。

**理由：** 这是确保 Native AOT 零反射开销的关键决策。`System.CommandLine` 依赖反射进行类型绑定，与 AOT 裁剪策略冲突。手动解析仅需 ~80 行代码，且完全控制 `-h`/`--help` 中文输出的格式，确保与 Go 帮助文本 1:1 一致。

### 6. 图片库：`SixLabors.ImageSharp` v3+

**选择：** `SixLabors.ImageSharp` 3.x 作为唯一外部 NuGet 依赖。

**备选考虑：**
- `SkiaSharp`：功能强大但引入大量原生依赖，与 AOT 自包含二进制理念冲突。
- `System.Drawing.Common`：Linux 需 `libgdiplus`，不跨平台，AOT 不兼容。
- `stb_image` P/Invoke：二进制极小但需分发平台特定原生库。

**理由：** ImageSharp v3+ 完全支持 AOT/裁剪，纯 C# 无原生依赖，API 简洁。是 .NET 生态中 AOT 图片处理的事实标准。

### 7. 测试策略：TDD + 三层测试

**选择：** 三层测试体系：

1. **单元测试**（各模块的 `[Fact]`/`[Theory]`）— 纯函数测试：Lab 转换矩阵、Sobel 核、饱和度公式，使用已知输入/预期输出。
2. **属性测试** — 不变量：尺寸保持、值域钳制 [0,1]、色域转换往返一致性。
3. **集成测试** — 使用 `test.jpg` 夹具：加载、处理各管线阶段、验证输出维度和属性。

**理由：** 属性测试无需黄金文件即可捕获回归。管线正确后，可选添加基于哈希的回归测试验证与已知正确输出的对比。

### 8. TDD 开发顺序

每个阶段顺序：写测试 → 写代码 → `dotnet format` → `dotnet build /warnaserror` → `dotnet test`。在测试失败前不编写任何实现代码。

## Risks / Trade-offs

| 风险 | 缓解措施 |
|------|---------|
| `SixLabors.ImageSharp` 版本变动 | 锁定 3.x 大版本，该版本稳定且广泛使用 |
| 色域转换 float vs double 精度差异 | 使用与 Go `lab.go` 相同的字面常量；测试中使用 1e-4 容忍度（色域边缘）和 1e-6（中间值） |
| 双边滤波 O(n²) — 大图片慢 | `Parallel.For` 并行化；提前进行性能分析；接受 2812×1280、radius=10 约需数秒 |
| `test.jpg` 尺寸大（2812×1280）— 单元测试慢 | 单元测试使用小型合成图像（4×4、3×3）。集成测试仅在管线测试中使用 `test.jpg` |
| 手动 CLI 解析容易出错 | 覆盖所有参数组合的测试；参数校验逻辑与 Go/Rust 严格对齐 |
| AOT 编译后缺乏动态调试能力 | 开发阶段使用普通 JIT 模式进行 TDD；AOT 仅用于最终发布构建 |
