## ADDED Requirements

### Requirement: Load image from JPEG or PNG file

The system SHALL load JPEG and PNG image files into an internal `ImageData` representation
with float64 pixel values normalized to [0.0, 1.0].

#### Scenario: Load valid JPEG file
- **WHEN** loading `test.jpg` (2812x1280 JPEG)
- **THEN** returned ImageData SHALL have width=2812, height=1280, channels=3

#### Scenario: Load valid PNG file
- **WHEN** loading a valid PNG file
- **THEN** returned ImageData SHALL have correct dimensions and channels

#### Scenario: Reject unsupported format
- **WHEN** loading a GIF file
- **THEN** function SHALL return an error

#### Scenario: Reject non-existent file
- **WHEN** loading a path that does not exist
- **THEN** function SHALL return an error

### Requirement: Save image to JPEG or PNG file

The system SHALL save an `ImageData` representation to JPEG or PNG files.
JPEG output SHALL use quality 95. Format SHALL be determined by file extension.

#### Scenario: Save as JPEG produces valid JPEG
- **WHEN** saving ImageData to a `.jpg` path
- **THEN** resulting file SHALL be a valid JPEG that can be reloaded

#### Scenario: Save as PNG produces valid PNG
- **WHEN** saving ImageData to a `.png` path
- **THEN** resulting file SHALL be a valid PNG that can be reloaded

#### Scenario: Save-and-reload round-trip
- **WHEN** an image is saved and then reloaded
- **THEN** reloaded pixels SHALL match original within uint8 quantization error

#### Scenario: Reject unsupported output extension
- **WHEN** saving to a `.bmp` path
- **THEN** function SHALL return an error
