## Why

MATLAB 卡通化工具已有 Go 和 Rust 两个移植版本。增加 C# (.NET 10) Native AOT 移植，为目标平台的 .NET 生态用户提供零依赖的原生二进制工具。Native AOT 编译产出的单一原生 ELF 二进制无需安装 .NET 运行时，冷启动极快（毫秒级），同时享受 C# 的类型安全、Roslyn 静态分析和成熟的并行模型（`Parallel.For`）。这是三个端口中最"轻量"的目标——仅一个外部 NuGet 依赖（ImageSharp）。

## What Changes

- 新增 `cs/` 目录，包含一个 .NET 10 控制台项目（`ImageCartoonlization`）和一个 xUnit 测试项目（`ImageCartoonlization.Tests`）
- CLI 接口与 Go/Rust 版本完全一致：所有参数名（`-i`, `-o`, `--edge-thresh`, `--sat`, `--radius`, `--sigma-d`, `--sigma-r`, `--loop`, `--workers`, `-v`）和参数范围均相同
- TDD 开发流程：每个模块先写测试再写实现，每次代码变更后执行 `dotnet format` → `dotnet build /warnaserror` → `dotnet test`
- Native AOT 发布：通过 `dotnet publish -c Release /p:PublishAot=true` 生成单一原生二进制
- 数据使用 `float` (32-bit) 一维数组存储，`[y * width * channels + x * channels + channel]` 索引方式
- `Parallel.For` 实现双边滤波行级并行
- 所有代码注释和文档采用简体中文

## Capabilities

### New Capabilities

- `cartoon-pipeline`: 核心卡通化管线——饱和度调整、双边滤波（CIELab 空间）、Sobel 边缘检测、黑色描边叠加
- `color-conversion`: sRGB ↔ CIELab 色彩空间双向转换（D65 白点），RGB 转灰度
- `image-io`: 通过 SixLabors.ImageSharp 加载 JPEG/PNG 输入、保存 JPEG/PNG 输出
- `cli-interface`: 命令行接口，采用手动参数解析（AOT 兼容），与 Go/Rust 版本参数完全一致

### Modified Capabilities

无——没有需要修改的现有 C# 代码。

## Impact

- 新增目录 `cs/`（不修改任何现有 MATLAB/Go/Rust 代码）
- 依赖项：`SixLabors.ImageSharp` (>= 3.1.0) — 唯一外部 NuGet 包
- 开发依赖：`xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`
- 测试夹具：复用现有 `test.jpg`（2812×1280，3 通道 JPEG）
- AOT 构建产物：单一原生 ELF 二进制（无运行时依赖）
