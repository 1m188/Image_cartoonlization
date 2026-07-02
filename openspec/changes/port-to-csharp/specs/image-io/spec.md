## ADDED Requirements

### Requirement: Load image from JPEG or PNG file

通过 SixLabors.ImageSharp 加载 JPEG 和 PNG 图片文件到内部 `ImageData` 结构体。
像素值归一化到 [0.0, 1.0]，数据类型为 `float`（32位）。
支持 8-bit 每通道的 RGB 彩色图像。

#### Scenario: Load valid JPEG file
- **WHEN** 加载 `test.jpg`（2812×1280 JPEG）
- **THEN** 返回的 ImageData SHALL 具有 width=2812, height=1280, channels=3

#### Scenario: Load valid PNG file
- **WHEN** 加载一个有效的 PNG 文件
- **THEN** 返回的 ImageData SHALL 具有正确的尺寸和通道数

#### Scenario: Reject unsupported format
- **WHEN** 加载一个 GIF 文件
- **THEN** 函数 SHALL 返回错误

#### Scenario: Reject non-existent file
- **WHEN** 加载一个不存在的文件路径
- **THEN** 函数 SHALL 返回错误

### Requirement: Save image to JPEG or PNG file

将 `ImageData` 结构体保存为 JPEG 或 PNG 文件。
JPEG 输出使用质量 95，PNG 输出为无损编码。
输出格式由文件扩展名决定（`.jpg`/`.jpeg` → JPEG，`.png` → PNG）。
保存前将 float 像素值转为 uint8，使用四舍五入（+0.5 后截断）并钳制到 [0, 255]。

#### Scenario: Save as JPEG produces valid JPEG
- **WHEN** 将 ImageData 保存到 `.jpg` 路径
- **THEN** 生成的文件 SHALL 是可重新加载的有效 JPEG

#### Scenario: Save as PNG produces valid PNG
- **WHEN** 将 ImageData 保存到 `.png` 路径
- **THEN** 生成的文件 SHALL 是可重新加载的有效 PNG

#### Scenario: Save-and-reload round-trip
- **WHEN** 图像被保存后重新加载
- **THEN** 重新加载的像素 SHALL 在 uint8 量化误差范围内匹配原始值

#### Scenario: Reject unsupported output extension
- **WHEN** 保存到 `.bmp` 路径
- **THEN** 函数 SHALL 返回错误
