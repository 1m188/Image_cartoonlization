## ADDED Requirements

### Requirement: Convert sRGB image to CIELab color space

The system SHALL convert RGB values [0,1] to CIELab using the standard sRGB→Linear→XYZ→Lab chain with D65 reference white.
The conversion SHALL use the sRGB gamma transfer function and the CIE 1931 standard observer.

#### Scenario: White maps to (100, 0, 0) in Lab
- **WHEN** RGB=(1.0, 1.0, 1.0)
- **THEN** Lab SHALL be approximately (100.0, 0.0, 0.0)

#### Scenario: Black maps to (0, 0, 0) in Lab
- **WHEN** RGB=(0.0, 0.0, 0.0)
- **THEN** Lab SHALL be approximately (0.0, 0.0, 0.0)

#### Scenario: Pure red has positive a* value
- **WHEN** RGB=(1.0, 0.0, 0.0)
- **THEN** L SHALL be approximately 53, a* SHALL be positive, b* SHALL be positive

### Requirement: Convert CIELab image back to sRGB

The system SHALL convert CIELab values back to RGB [0,1] using the inverse Lab→XYZ→Linear→sRGB chain.
Round-trip conversion SHALL be lossy within floating-point tolerance.

#### Scenario: Round-trip preserves neutral gray
- **WHEN** RGB=(0.5, 0.5, 0.5)
- **THEN** after RGB→Lab→RGB, all channels SHALL be within 1e-10 of 0.5

#### Scenario: Round-trip preserves saturated colors
- **WHEN** RGB=(1.0, 0.0, 0.0)
- **THEN** after RGB→Lab→RGB, all channels SHALL be within 1e-4 of original (gamut-edge colors accumulate fp error in the sRGB→Linear→XYZ→Lab roundtrip)

#### Scenario: Out-of-gamut Lab values are clamped
- **WHEN** Lab values produce negative linear RGB
- **THEN** output RGB SHALL be clamped to [0.0, 1.0]

### Requirement: Convert RGB image to grayscale

The system SHALL convert RGB to grayscale using ITU-R BT.601 luminance coefficients.
Formula: `gray = 0.299*R + 0.587*G + 0.114*B`

#### Scenario: White RGB becomes white gray
- **WHEN** RGB=(1.0, 1.0, 1.0)
- **THEN** gray SHALL be 1.0

#### Scenario: Black RGB becomes black gray
- **WHEN** RGB=(0.0, 0.0, 0.0)
- **THEN** gray SHALL be 0.0

#### Scenario: Equal RGB channels produce same gray value
- **WHEN** R=G=B for all pixels
- **THEN** gray value SHALL equal any of the input channels
