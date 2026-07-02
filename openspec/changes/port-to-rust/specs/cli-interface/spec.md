## ADDED Requirements

### Requirement: Parse command-line arguments with clap

The system SHALL accept all parameters matching the Go port's CLI interface.
Required: `-i <path>`. Optional: `-o <path>`, `--edge-thresh`, `--sat`, `--radius`, `--sigma-d`, `--sigma-r`, `--loop`, `--workers`, `-v`, `-h`/`--help`.

#### Scenario: Help flag displays usage
- **WHEN** `--help` is passed
- **THEN** help text SHALL be displayed and exit code SHALL be 0

#### Scenario: Missing input path errors
- **WHEN** no `-i` argument is provided
- **THEN** program SHALL print error message and exit with non-zero code

#### Scenario: All parameters accepted
- **WHEN** all optional flags are provided with valid values
- **THEN** program SHALL parse them without error

### Requirement: Validate parameter ranges

The system SHALL validate all numeric parameters against their allowed ranges
and print clear error messages for violations.

#### Scenario: Reject edge-thresh out of range
- **WHEN** `--edge-thresh` is 1.5
- **THEN** program SHALL print error and exit non-zero

#### Scenario: Reject radius zero
- **WHEN** `--radius` is 0
- **THEN** program SHALL print error and exit non-zero

#### Scenario: Reject negative sigma
- **WHEN** `--sigma-d` is -1
- **THEN** program SHALL print error and exit non-zero

### Requirement: Verbose mode outputs per-stage timing

The system SHALL print per-stage processing time and details when `-v` flag is set.

#### Scenario: Verbose output on test.jpg
- **WHEN** `-v` is passed with a valid input
- **THEN** program SHALL print lines for saturation, bilateral filter, edge detection, edge overlay, and total time

#### Scenario: Non-verbose mode silent on success
- **WHEN** `-v` is NOT passed
- **THEN** program SHALL only print the total completion time

### Requirement: Output format determined by file extension

The system SHALL save output as JPEG when extension is `.jpg` or `.jpeg`, and as PNG when `.png`.

#### Scenario: Output to .jpg produces JPEG
- **WHEN** output path ends with `.jpg`
- **THEN** output file SHALL be valid JPEG

#### Scenario: Output to unsupported extension errors
- **WHEN** output path ends with `.bmp`
- **THEN** program SHALL print error and exit non-zero
