## ADDED Requirements

### Requirement: Convert sRGB image to CIELab color space

将 sRGB 色彩空间的像素值 [0, 1] 转换为 CIELab 色彩空间。
转换链路：sRGB → 线性 RGB（去 gamma 校正） → CIE XYZ（D65 参考白点） → CIELab。
使用标准 sRGB gamma 传递函数和 CIE 1931 标准观察者矩阵。
所有计算使用 `float`（32位）精度。

#### Scenario: White maps to (100, 0, 0) in Lab
- **WHEN** RGB=(1.0, 1.0, 1.0)
- **THEN** Lab SHALL 近似为 (100.0, 0.0, 0.0)

#### Scenario: Black maps to (0, 0, 0) in Lab
- **WHEN** RGB=(0.0, 0.0, 0.0)
- **THEN** Lab SHALL 近似为 (0.0, 0.0, 0.0)

#### Scenario: Pure red has positive a* value
- **WHEN** RGB=(1.0, 0.0, 0.0)
- **THEN** L SHALL 约为 53，a* SHALL 为正值，b* SHALL 为正值

### Requirement: Convert CIELab image back to sRGB

将 CIELab 色彩空间转换回 sRGB 色彩空间 [0, 1]。
转换链路：CIELab → CIE XYZ → 线性 RGB → sRGB（加 gamma 校正）。
线性 RGB 负值 SHALL 被钳制到 0。

#### Scenario: Round-trip preserves neutral gray
- **WHEN** RGB=(0.5, 0.5, 0.5)
- **THEN** 经过 RGB→Lab→RGB 往返转换后，所有通道 SHALL 在 1e-6 内接近 0.5

#### Scenario: Round-trip preserves saturated colors
- **WHEN** RGB=(1.0, 0.0, 0.0)
- **THEN** 经过 RGB→Lab→RGB 往返转换后，所有通道 SHALL 在 1e-4 内接近原始值（色域边缘颜色在 sRGB→Linear→XYZ→Lab 往返中累积浮点误差）

#### Scenario: Out-of-gamut Lab values are clamped
- **WHEN** Lab 值产生负的线性 RGB 值
- **THEN** 输出 RGB SHALL 被钳制到 [0.0, 1.0]

### Requirement: Convert RGB image to grayscale

将 RGB 图像转换为单通道灰度图。
使用 ITU-R BT.601 亮度系数，公式：`gray = 0.299*R + 0.587*G + 0.114*B`。

#### Scenario: White RGB becomes white gray
- **WHEN** RGB=(1.0, 1.0, 1.0)
- **THEN** 灰度值 SHALL 为 1.0

#### Scenario: Black RGB becomes black gray
- **WHEN** RGB=(0.0, 0.0, 0.0)
- **THEN** 灰度值 SHALL 为 0.0

#### Scenario: Equal RGB channels produce same gray value
- **WHEN** 所有像素 R=G=B
- **THEN** 灰度值 SHALL 等于任意一个输入通道的值
