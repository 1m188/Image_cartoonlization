## ADDED Requirements

### Requirement: Adjust saturation of RGB image

图像饱和度调整对输入 RGB 图像进行饱和度的线性插值或外推。
计算公式：`result = (1 - scalar) * gray + scalar * color`，其中 `gray = 0.299*R + 0.587*G + 0.114*B`。
像素数据类型为 `float`（32位），值域为 [0.0, 1.0]。

#### Scenario: Identity at scalar=1
- **WHEN** 饱和度参数 scalar 为 1.0
- **THEN** 输出像素 SHALL 在浮点误差范围内等于输入像素

#### Scenario: Grayscale at scalar=0
- **WHEN** 饱和度参数 scalar 为 0
- **THEN** 输出像素的三个通道值 SHALL 相等（R=G=B）

#### Scenario: Enhanced saturation at scalar=2
- **WHEN** 饱和度参数 scalar 为 2.0
- **THEN** 每个像素的通道间方差 SHALL 大于或等于输入的通道间方差

#### Scenario: Output value clamping
- **WHEN** 饱和度调整产生的值超出 [0.0, 1.0] 范围
- **THEN** 输出 SHALL 被钳制到 [0.0, 1.0] 区间

### Requirement: Apply bilateral filter to smooth image while preserving edges

双边滤波对彩色图像在 CIELab 色彩空间中进行保边平滑处理。
对于彩色图像（3 通道），先将 sRGB 转换为 CIELab，在 Lab 空间中滤波（sigma_r 乘以 100），再转换回 sRGB。
对于单通道灰度图像，直接在灰度空间滤波。
滤波支持 `Parallel.For` 行级并行处理，支持多次迭代。

#### Scenario: Window radius zero is identity
- **WHEN** 窗口半径 radius 为 0
- **THEN** 输出 SHALL 在浮点误差范围内等于输入

#### Scenario: Very large range sigma approximates Gaussian blur
- **WHEN** 颜色标准差 sigma_r 设置为极大值（如 1000）
- **THEN** 边缘 SHALL 被平滑，效果近似于使用相同空间标准差的高斯模糊

#### Scenario: Output dimensions preserved
- **WHEN** 对任意有效图像应用双边滤波
- **THEN** 输出的宽度和高度 SHALL 等于输入的宽度和高度

#### Scenario: Multiple iterations compound smoothing
- **WHEN** loop_num 为 3
- **THEN** 双边滤波 SHALL 被依次应用恰好 3 次

#### Scenario: Parallel execution produces same result
- **WHEN** 以 workers=1 和 workers=N（N>1）运行双边滤波
- **THEN** 像素值 SHALL 在浮点误差范围内完全一致

### Requirement: Detect edges using Sobel operator

对灰度图使用 3x3 Sobel 核进行边缘检测。梯度幅值 `G = sqrt(Gx² + Gy²)`。
当 G 超过阈值时标记为边缘像素（值为 1.0），否则为非边缘像素（值为 0.0）。
边界像素（四周各一行/列）因无法完整卷积，默认标记为非边缘。

#### Scenario: Uniform image has no edges
- **WHEN** 输入为均匀灰度图像（所有像素值相等）
- **THEN** SHALL 不检测到任何边缘像素（所有 mask 值 = 0）

#### Scenario: Edge at sharp gradient boundary
- **WHEN** 输入为已知梯度边界的合成图像
- **THEN** 在合适的阈值下，边缘像素 SHALL 出现在边界位置

#### Scenario: Lower threshold yields more edges
- **WHEN** 在同一图像上阈值从 0.1 降低到 0.01
- **THEN** 检测到的边缘像素数量 SHALL 增加

### Requirement: Overlay edge mask as black strokes

将边缘掩码叠加到模糊处理后的图像上，边缘像素变为黑色（RGB=0,0,0），非边缘像素保持模糊值并钳制到 [0.0, 1.0]。

#### Scenario: All-edge mask produces black image
- **WHEN** 边缘掩码全部为 1.0
- **THEN** 所有输出像素 SHALL 为 (0.0, 0.0, 0.0)

#### Scenario: No-edge mask preserves image
- **WHEN** 边缘掩码全部为 0.0
- **THEN** 输出 SHALL 在浮点误差范围内等于模糊输入

#### Scenario: Mixed mask correctly composites
- **WHEN** 部分像素是边缘，部分不是
- **THEN** 边缘像素 SHALL 为黑色，非边缘像素 SHALL 保留模糊值

### Requirement: End-to-end cartoonization pipeline

提供 `Cartoonize()` 方法执行完整管线：
饱和度调整 → 双边滤波（首次） → 边缘检测 → 额外双边滤波迭代（如有） → 边缘叠加 → 最终值域钳制。
接受 `Params` 结构体传入所有可配置参数，返回处理后的图像和分步执行信息。

#### Scenario: Default parameters produce valid output
- **WHEN** 以默认参数处理 `test.jpg`
- **THEN** 输出 SHALL 具有与输入相同的尺寸且所有值在 [0.0, 1.0] 范围内

#### Scenario: Pipeline returns processing metadata
- **WHEN** 卡通化处理完成
- **THEN** 函数 SHALL 返回每个处理步骤的耗时和详细信息

#### Scenario: Final clamping ensures valid output
- **WHEN** 边缘叠加后像素值可能超出 [0.0, 1.0]
- **THEN** 最终输出 SHALL 被钳制到 [0.0, 1.0] 区间
