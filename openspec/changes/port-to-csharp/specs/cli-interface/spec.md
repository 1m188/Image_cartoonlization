## ADDED Requirements

### Requirement: Parse command-line arguments with manual parsing

命令行参数解析采用手动方式（无外部 CLI 库依赖，确保 AOT 兼容性）。
参数名与 Go/Rust 版本完全一致。
必选参数：`-i <路径>`。
可选参数：`-o <路径>`（默认 `cartoon.png`）、`--edge-thresh`、`--sat`、`--radius`、`--sigma-d`、`--sigma-r`、`--loop`、`--workers`、`-v`、`-h`/`--help`。

#### Scenario: Help flag displays usage
- **WHEN** 传入 `--help` 或 `-h`
- **THEN** SHALL 显示完整中文帮助信息，退出码为 0

#### Scenario: Missing input path errors
- **WHEN** 未提供 `-i` 参数
- **THEN** 程序 SHALL 打印错误信息并以非零码退出

#### Scenario: All parameters accepted
- **WHEN** 提供了所有可选参数的有效值
- **THEN** 程序 SHALL 成功解析所有参数

#### Scenario: Short and long help flags both work
- **WHEN** 传入 `-h` 短标志
- **THEN** 行为 SHALL 与 `--help` 完全一致

### Requirement: Validate parameter ranges

对所有数值参数进行范围校验，违反范围时打印清晰的错误信息。
校验规则：
- `--edge-thresh`: [0.0, 1.0]
- `--sat`: >= 0
- `--radius`: [1, 50]
- `--sigma-d`: > 0
- `--sigma-r`: > 0
- `--loop`: [1, 10]
- `--workers`: >= 0

#### Scenario: Reject edge-thresh out of range
- **WHEN** `--edge-thresh` 为 1.5
- **THEN** 程序 SHALL 打印错误并退出非零

#### Scenario: Reject radius zero
- **WHEN** `--radius` 为 0
- **THEN** 程序 SHALL 打印错误并退出非零

#### Scenario: Reject negative sigma
- **WHEN** `--sigma-d` 为 -1
- **THEN** 程序 SHALL 打印错误并退出非零

#### Scenario: Reject loop out of range
- **WHEN** `--loop` 为 15
- **THEN** 程序 SHALL 打印错误并退出非零

### Requirement: Verbose mode outputs per-stage timing

当 `-v` 标志设置时，打印每个处理步骤的耗时和详情。
输出格式与 Go/Rust 版本一致，包含饱和度调整、双边滤波（含迭代次数）、边缘检测（含边缘百分比）、边缘叠加及总耗时。

#### Scenario: Verbose output on test.jpg
- **WHEN** 以 `-v` 标志处理有效输入图片
- **THEN** 程序 SHALL 打印饱和度调整、双边滤波、边缘检测、边缘叠加和总耗时的详细信息

#### Scenario: Non-verbose mode silent on success
- **WHEN** 未设置 `-v` 标志
- **THEN** 程序 SHALL 仅打印总完成时间

### Requirement: Output format determined by file extension

输出格式由文件扩展名决定：`.jpg`/`.jpeg` → JPEG（质量 95），`.png` → PNG（无损）。

#### Scenario: Output to .jpg produces JPEG
- **WHEN** 输出路径以 `.jpg` 结尾
- **THEN** 输出文件 SHALL 为有效 JPEG

#### Scenario: Unsupported extension errors at CLI
- **WHEN** 输出路径以 `.bmp` 结尾
- **THEN** 程序 SHALL 在参数校验阶段打印错误并以非零码退出

### Requirement: Behave identically to Go/Rust port for same parameters

在相同的输入图片和参数下，C# 版本的输出 SHALL 与 Go/Rust 版本视觉上一致（允许在 uint8 量化误差范围内的浮点差异）。

#### Scenario: Same parameters produce same output dimensions
- **WHEN** 以默认参数在三个端口（Go/Rust/C#）中处理 `test.jpg`
- **THEN** 输出尺寸 SHALL 完全一致
