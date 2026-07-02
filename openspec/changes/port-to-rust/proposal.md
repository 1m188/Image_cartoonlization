## Why

The MATLAB cartoonization tool already has a Go port. Porting to Rust adds a zero-dependency native binary with stronger safety guarantees, fearless concurrency via Rayon, and lays groundwork for future WASM/web deployment. Rust's ownership model eliminates the memory safety concerns that Go's GC avoids but still retains — while delivering C-level performance for the compute-heavy bilateral filter.

## What Changes

- New `rs/` directory with a single Cargo crate containing both library and CLI binary
- CLI interface parity with the Go version: all same parameters (`-i`, `-o`, `--edge-thresh`, `--sat`, `--radius`, `--sigma-d`, `--sigma-r`, `--loop`, `--workers`, `-v`)
- Test-Driven Development: every module is tested before and after implementation using `test.jpg` as fixture
- Development discipline: `cargo fmt`, `cargo clippy`, `cargo test` enforced on every code change
- Flat `Vec<f64>` data representation with stride indexing for cache-friendly memory layout
- Rayon-powered parallel bilateral filter

## Capabilities

### New Capabilities

- `cartoon-pipeline`: Core cartoonization pipeline — saturation adjustment, bilateral filtering, Sobel edge detection, edge overlay
- `color-conversion`: sRGB ↔ CIELab color space conversion with D65 white point
- `image-io`: Load JPEG/PNG input, save JPEG/PNG output via the `image` crate
- `cli-interface`: Command-line interface with clap, parity with Go port's parameters

### Modified Capabilities

None — no existing Rust code to modify.

## Impact

- New directory `rs/` at repo root (no existing code modified)
- Dependencies: `image`, `clap` (derive), `rayon`
- Test fixture: existing `test.jpg` (2812×1280, 3-channel JPEG)
- No changes to MATLAB or Go codebases
