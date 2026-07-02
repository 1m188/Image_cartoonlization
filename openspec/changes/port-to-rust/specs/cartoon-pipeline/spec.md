## ADDED Requirements

### Requirement: Adjust saturation of RGB image

The system SHALL adjust image saturation using linear interpolation between the grayscale and original color values.
Formula: `result = (1 - scalar) * gray + scalar * color`, where `gray = 0.299*R + 0.587*G + 0.114*B`.

#### Scenario: Identity at scalar=1
- **WHEN** saturation scalar is 1.0
- **THEN** output pixels SHALL equal input pixels within floating-point tolerance

#### Scenario: Grayscale at scalar=0
- **WHEN** saturation scalar is 0
- **THEN** all three channels of every output pixel SHALL be equal (R=G=B)

#### Scenario: Enhanced saturation at scalar=2
- **WHEN** saturation scalar is 2.0
- **THEN** per-pixel channel variance SHALL be greater than or equal to input variance

#### Scenario: Output value clamping
- **WHEN** saturation adjustment produces values outside [0.0, 1.0]
- **THEN** output SHALL be clamped to [0.0, 1.0]

### Requirement: Apply bilateral filter to smooth image while preserving edges

The system SHALL apply bilateral filtering with configurable window radius, spatial sigma, and range sigma.
For color images, filtering SHALL be performed in CIELab color space with range sigma multiplied by 100.
The filter SHALL support multiple iterations.

#### Scenario: Window radius zero is identity
- **WHEN** bilateral filter is called with radius=0
- **THEN** output SHALL equal input within floating-point tolerance

#### Scenario: Very large range sigma approximates Gaussian blur
- **WHEN** range sigma is set extremely large (e.g., 1000)
- **THEN** edges SHALL be smoothed similarly to a Gaussian blur of same spatial sigma

#### Scenario: Output dimensions preserved
- **WHEN** bilateral filter is applied to any valid image
- **THEN** output width and height SHALL equal input width and height

#### Scenario: Multiple iterations compound smoothing
- **WHEN** loop_num is 3
- **THEN** bilateral filter SHALL be applied exactly 3 times sequentially

#### Scenario: Parallel execution produces same result
- **WHEN** bilateral filter runs with workers=1 and workers=N
- **THEN** pixel values SHALL be identical within floating-point tolerance

### Requirement: Detect edges using Sobel operator

The system SHALL detect edges in a grayscale image using 3x3 Sobel kernels and a configurable threshold.

#### Scenario: Uniform image has no edges
- **WHEN** a uniform grayscale image (all pixels equal) is processed
- **THEN** no edge pixels SHALL be detected (all mask values = 0)

#### Scenario: Edge at sharp gradient boundary
- **WHEN** a synthetic image with a known gradient boundary is processed
- **THEN** edge pixels SHALL appear at the boundary location with an appropriate threshold

#### Scenario: Lower threshold yields more edges
- **WHEN** threshold decreases from 0.1 to 0.01 on the same image
- **THEN** the number of detected edge pixels SHALL increase

### Requirement: Overlay edge mask as black strokes

The system SHALL overlay an edge mask onto a blurred image, turning edge pixels to black (RGB=0,0,0).

#### Scenario: All-edge mask produces black image
- **WHEN** edge mask is all 1.0
- **THEN** all output pixels SHALL be (0.0, 0.0, 0.0)

#### Scenario: No-edge mask preserves image
- **WHEN** edge mask is all 0.0
- **THEN** output SHALL equal the blurred input within floating-point tolerance

#### Scenario: Mixed mask correctly composites
- **WHEN** some pixels are edge and some are not
- **THEN** edge pixels SHALL be black, non-edge pixels SHALL retain blurred values

### Requirement: End-to-end cartoonization pipeline

The system SHALL provide a `cartoonize` function that executes the full pipeline:
saturation adjustment → bilateral filter → edge detection → edge overlay, accepting all configurable parameters.

#### Scenario: Default parameters produce valid output
- **WHEN** `test.jpg` is processed with default parameters
- **THEN** output SHALL have same dimensions as input and all values in [0.0, 1.0]

#### Scenario: Pipeline returns processing metadata
- **WHEN** cartoonization completes
- **THEN** function SHALL return timing and detail information for each pipeline stage
