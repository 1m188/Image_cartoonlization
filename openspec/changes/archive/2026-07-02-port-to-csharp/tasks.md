## 1. 项目脚手架

- [x] 1.1 创建 `cs/` 目录，初始化解决方案：`dotnet new sln -n ImageCartoonlization`
- [x] 1.2 创建主项目：`dotnet new console -n ImageCartoonlization --framework net10.0`，配置 AOT (`PublishAot`, `InvariantGlobalization`, `OptimizationPreference=Speed`, `StripSymbols`)
- [x] 1.3 创建测试项目：`dotnet new xunit -n ImageCartoonlization.Tests --framework net10.0`，添加 `Microsoft.NET.Test.Sdk` 和 `xunit.runner.visualstudio` 引用，添加对主项目的项目引用
- [x] 1.4 将测试项目添加到解决方案中
- [x] 1.5 编写 `.editorconfig` 配置代码风格规则（缩进、命名、using 排序）
- [x] 1.6 创建 `ImageData.cs`，定义 `ImageData` 结构体（`width`, `height`, `channels`, `data: float[]`），实现索引辅助方法 `GetPixel(y,x,c)` 和 `SetPixel(y,x,c,value)`
- [x] 1.7 为 ImageData 编写 TDD 测试：构造、索引计算正确性、越界处理
- [x] 1.8 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 1.9 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 2. 图片 I/O

- [x] 2.1 编写失败测试：加载 `test.jpg` → 验证 width=2812 height=1280 channels=3；不存在的文件 → 错误；GIF → 错误
- [x] 2.2 实现 `ImageIO.cs` 中的 `LoadImage(path) -> Result<ImageData>`，通过 SixLabors.ImageSharp 加载 JPEG/PNG，像素值归一化到 [0,1]
- [x] 2.3 编写失败测试：保存为 PNG 再重新加载 → 像素匹配；保存 JPEG → 有效文件；保存 `.bmp` → 错误
- [x] 2.4 实现 `ImageIO.cs` 中的 `SaveImage(path, img)`，JPEG 质量 95，float→uint8 使用四舍五入并钳制
- [x] 2.5 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 2.6 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 3. 色域转换

- [x] 3.1 编写失败测试：白色→Lab(100,0,0)、黑色→Lab(0,0,0)、红色→a* 为正、灰度往返一致性、sRGB→Lab→sRGB 往返精度
- [x] 3.2 实现 `LabColor.cs` 中的 sRGB↔Lab 转换（sRGB→线性→XYZ→Lab 链路，D65 白点），包含 f(t)/fInverse(t) 分段函数
- [x] 3.3 实现 `LabColor.cs` 中的 `RgbToGray()`（ITU-R BT.601 系数）
- [x] 3.4 验证所有转换矩阵常量与 Go 参考实现 `go/cartoon/lab.go` 一致
- [x] 3.5 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 3.6 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 4. 饱和度调整

- [x] 4.1 编写失败测试：scalar=1→恒等变换；scalar=0→所有通道相等（灰度）；scalar=2→通道方差增大；超出范围→钳制
- [x] 4.2 实现 `Saturation.cs` 中的 `AdjustSaturation(img, scalar)`，公式 `(1-s)*gray + s*color`，`gray` 使用 BT.601 系数
- [x] 4.3 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 4.4 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 5. 边缘检测

- [x] 5.1 编写失败测试：均匀图像→无边；合成梯度图→边界处有边；低阈值→更多边
- [x] 5.2 实现 `EdgeDetection.cs` 中的 `DetectEdges(gray, threshold)`，3×3 Sobel 核卷积，梯度幅值二值化
- [x] 5.3 编写边缘叠加测试：mask 全 1→全黑图像；mask 全 0→恒等；混合 mask→正确合成
- [x] 5.4 实现 `EdgeDetection.cs` 中的 `OverlayEdges(blurred, mask)`，边缘像素置黑（0,0,0），非边缘保留并钳制
- [x] 5.5 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 5.6 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 6. 双边滤波

- [x] 6.1 编写失败测试：radius=0→恒等；大 sigma_r→近似高斯模糊；尺寸保持；workers=1 与 workers=N 结果一致
- [x] 6.2 实现 `BilateralFilter.cs` 中的调度器 `Apply()`：根据通道数（1 或 3）路由到灰度或彩色路径
- [x] 6.3 实现彩色双边滤波：sRGB→Lab 转换，在 CIELab 空间中滤波（sigma_r *= 100，与 MATLAB 一致），Lab→sRGB 转换
- [x] 6.4 实现灰度双边滤波：直接在单通道上滤波
- [x] 6.5 添加 `Parallel.For` 行级并行，预计算空间域高斯核
- [x] 6.6 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 6.7 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 7. 管线集成

- [x] 7.1 编写失败测试：`test.jpg` 默认参数 → 相同尺寸、值在 [0,1]；返回分步耗时信息
- [x] 7.2 实现 `CartoonPipeline.cs` 中的 `Params` 记录类型，包含所有参数及其默认值
- [x] 7.3 实现 `Cartoonize(img, params)` 管线编排器：饱和度 → 双边滤波×N → 灰度 → 边缘检测 → 边缘叠加 → 最终钳制
- [x] 7.4 每一步记录耗时和详情信息，统一返回
- [x] 7.5 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 7.6 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 8. CLI 接口

- [x] 8.1 编写失败测试：`--help` 打印用法；缺少 `-i` 非零退出；有效参数解析成功；`.bmp` 输出 → 错误
- [x] 8.2 实现 `Program.cs` 手动参数解析，所有参数名与 Go/Rust 严格一致，含中英文帮助文本
- [x] 8.3 添加参数范围校验及清晰的中文错误信息
- [x] 8.4 将 `Program.cs` 串联完整流程：参数解析 → 加载图片 → 调用 `Cartoonize()` → 保存图片
- [x] 8.5 实现 `-v` 详细输出模式：分步耗时、百分比、参数回显
- [x] 8.6 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/`
- [x] 8.7 代码审查：检查正确性、风格、边界情况、逻辑错误 —— 修复所有问题后再继续

## 9. 最终验证

- [x] 9.1 执行 `dotnet publish -c Release /p:PublishAot=true -o cs/nativeaot`，确认零警告、成功生成原生二进制
- [x] 9.2 以默认参数处理 `test.jpg`，验证输出文件存在且有效可用
- [x] 9.3 运行完整 CLI 参数组合矩阵：edge-thresh 0.005/0.05、sat 1/3、loop 1/3 等，逐一验证
- [x] 9.4 对比 C# 输出与 Go/Rust 输出，验证视觉一致性（允许 uint8 量化误差）
- [x] 9.5 验证：`dotnet format cs/ --verify-no-changes && dotnet build cs/ /warnaserror && dotnet test cs/` — 全部绿色

## 10. 全局代码审查

- [x] 10.1 对全部代码进行全面审查：检查 Bug、逻辑错误、矛盾、边界情况、性能问题、规格符合性
- [x] 10.2 MPLR 审查：发现 6 个问题（2 个 CliParser 逻辑缺陷、2 个 API 防御缺失、重复代码、命名优化），全部修复
- [x] 10.3 补充测试：ImageData 零/负通道校验、BilateralFilter sigma=0 guard、CliParser 缺值/误用、EdgeOverlay 越界值、灰度双边滤波实际效果、<3x3 图像边缘检测
- [x] 10.4 确认无任何问题：审查循环完成，项目结束

## 11. MPLR 审查修复（2026-07-02）

- [x] 11.1 修复 ImageData 构造器：增加 Channels > 0 校验 (`ArgumentOutOfRangeException`)
- [x] 11.2 修复 BilateralFilter.Apply：增加 sigma_d / sigma_r <= 0 防御性返回原图
- [x] 11.3 修复 CliParser：-i 后跟 "-" 开头的值时拒绝（防止 `-i --edge-thresh` 误解析）
- [x] 11.4 修复 CliParser：数值标志（--edge-thresh 等）缺值时明确报错
- [x] 11.5 消除重复代码：Saturation/EdgeDetection/LabColor 中 Clamp01 统一替换为 Math.Clamp
- [x] 11.6 改进 BuildDomainKernel 命名：dx/dy → rowOff/colOff（消除歧义）
- [x] 11.7 补充 12 个新测试用例（总计 74 tests，全部通过，0 警告）

## 12. MPLR 第二轮审查修复（2026-07-02）

- [x] 12.1 (L1) 修复 ImageData 构造器：增加 Width > 0 / Height > 0 校验 (`ArgumentOutOfRangeException`)
- [x] 12.2 (L2) 修复 EdgeDetection.OverlayEdges：增加 blurred 与 edgeMask 尺寸匹配校验
- [x] 12.3 (R1) 完善 ImageData 文档注释：明确说明 struct 值拷贝时 Data 数组引用共享的语义
- [x] 12.4 (R2) 修复 GetPixel/SetPixel：增加 `(uint)idx >= limit` 边界检查，异常类型从 `IndexOutOfRangeException` 改为 `ArgumentOutOfRangeException`（符合 CA2201）
- [x] 12.5 (R3) 修复 LabColor：RgbToLab / LabToRgb / RgbToGray 增加 channels != 3 校验
- [x] 12.6 (P1) 优化 BilateralFilter 内层循环：每个邻域像素仅读取一次（原为 6 次 GetPixel 调用 → 3 次直接 Data 数组访问），消除重复读取
- [x] 12.7 (P2) 优化 BilateralFilter 内层循环：预计算 rowBase / kyBase 避免重复索引计算
- [x] 12.8 (P3) 优化 EdgeDetection：Sobel 核改为 `static readonly` 避免每次调用时重复分配
- [x] 12.9 (P4) 优化 BilateralFilter.BuildDomainKernel：从 `float[][]` 锯齿数组改为 `float[]` 一维数组 + 手动索引
- [x] 12.10 (M1) 优化 Program.cs：查找总耗时步骤从 foreach 线性搜索改为 `steps[^1]` 直接索引
- [x] 12.11 (M2) 规范化 CartoonPipeline：edgeCount 统计改为使用 GetPixel API（而非直接遍历 Data 数组）
- [x] 12.12 (M3) 简化 BuildDomainKernel：移除冗余 `size` 参数，内部直接计算 `kernelSize = 2*w+1`
- [x] 12.13 补充 12 个新测试用例（总计 86 tests，全部通过，0 警告）

## 13. MPLR 第三轮审查修复（2026-07-02）

- [x] 13.1 (L1) 修复 ImageIO.SaveImage：channels 守卫 `< 3` → `!= 3`，防止 >3 通道图像步长错误
- [x] 13.2 (M1) 修复 CliParser：增加 `knownFlags` 集合校验，未知参数（如 `--edge-thres`）明确报错
- [x] 13.3 (L2) 修复 CliParser：检测多个 `-i` 出现时明确报错
- [x] 13.4 (L3) 修复 ImageIO.FloatToByte：增加 `float.IsNaN()` 显式检查
- [x] 13.5 (R1) 修复 ImageData 构造器：`(long)width*height*channels > Array.MaxLength` 溢出检查
- [x] 13.6 (R4) 修复 BilateralFilter.Apply：增加 `float.IsNaN` / `float.IsInfinity` sigma 检查
- [x] 13.7 (P1) 优化 LabColor.RgbToLab / LabToRgb / RgbToGray：改用直接 Data 数组访问
- [x] 13.8 (P2) 优化 EdgeDetection.OverlayEdges：改用直接 Data 数组访问
- [x] 13.9 (P2) 优化 Saturation.AdjustSaturation：改用直接 Data 数组访问
- [x] 13.10 优化 CartoonPipeline.edgeCount：改用直接 Data 数组遍历
- [x] 13.11 验证：`dotnet format --verify-no-changes && dotnet build /warnaserror && dotnet test` — 全部通过

