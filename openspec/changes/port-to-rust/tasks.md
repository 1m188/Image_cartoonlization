## 1. Project Scaffolding

- [x] 1.1 Initialize Cargo project at `rs/` with dependencies: `image 0.25`, `clap 4` (derive), `rayon 1.10`
- [x] 1.2 Configure `[dev-dependencies]` with `tempfile 3` for CLI tests
- [x] 1.3 Create `src/data.rs` with `ImageData { width, height, channels, data: Vec<f64> }` and index helpers
- [x] 1.4 Write TDD tests for ImageData construction, index calculation, and indexing
- [x] 1.5 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 1.6 MPLR code review: check correctness, style, edge cases, logic errors — fix all issues before proceeding

## 2. Image I/O

- [x] 2.1 Write failing tests: load `test.jpg` → verify width 2812 height 1280 channels 3; nonexistent file → error; GIF → error
- [x] 2.2 Implement `src/io.rs` with `load_image(path) -> Result<ImageData>` using `image` crate
- [x] 2.3 Write failing tests: save-to-PNG then reload → pixels match; save JPEG → valid file; save to `.bmp` → error
- [x] 2.4 Implement `save_image(path, img)` using `image` crate (JPEG quality 95)
- [x] 2.5 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 2.6 MPLR code review — fix all issues before proceeding

## 3. Color Space Conversion

- [x] 3.1 Write failing tests: white→Lab(100,0,0), black→Lab(0,0,0), red→positive a*, round-trip identity, gray conversion
- [x] 3.2 Implement `src/lab.rs` with sRGB↔Lab conversion (sRGB→Linear→XYZ→Lab chain, D65 white point) and `rgb_to_gray()`
- [x] 3.3 Verify all conversion matrix constants match Go reference `go/cartoon/lab.go`
- [x] 3.4 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 3.5 MPLR code review — fix all issues before proceeding

## 4. Saturation Adjustment

- [x] 4.1 Write failing tests: scalar=1→identity; scalar=0→grayscale(all channels equal); scalar=2→variance increases; out-of-range→clamped
- [x] 4.2 Implement `src/saturation.rs` with formula `(1-s)*gray + s*color`
- [x] 4.3 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 4.4 MPLR code review — fix all issues before proceeding

## 5. Edge Detection

- [x] 5.1 Write failing tests: uniform image→no edges; synthetic gradient→edges at boundary; lower threshold→more edges
- [x] 5.2 Implement `src/edge.rs` `detect_edges()` with 3×3 Sobel kernels
- [x] 5.3 Write overylay tests: all-1 mask→black image; all-0 mask→identity; mixed mask→correct composite
- [x] 5.4 Implement `src/edge.rs` `overlay_edges()`
- [x] 5.5 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 5.6 MPLR code review — fix all issues before proceeding

## 6. Bilateral Filter

- [x] 6.1 Write failing tests: radius=0→identity; large sigma_r→gaussian-like blur; dimensions preserved; workers=1 == workers=N
- [x] 6.2 Implement `src/bilateral.rs` with dispatcher routing to color or grayscale path
- [x] 6.3 Implement color bilateral in CIELab space with sigma_r *= 100 matching MATLAB/Go behavior
- [x] 6.4 Implement grayscale bilateral filter
- [x] 6.5 Add rayon parallelization with per-row `par_chunks_mut`
- [x] 6.6 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 6.7 MPLR code review — fix all issues before proceeding

## 7. Pipeline Integration

- [x] 7.1 Write failing tests: `test.jpg` with default params → same dimensions, values in [0,1]; returns timing metadata
- [x] 7.2 Implement `src/cartoon.rs` with `Params` struct and `cartoonize()` pipeline orchestrator
- [x] 7.3 Wire pipeline: saturation → bilateral(N×) → grayscale → edge detect → overlay → final clamp
- [x] 7.4 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 7.5 MPLR code review — fix all issues before proceeding

## 8. CLI Interface

- [x] 8.1 Write failing tests: `--help` prints usage; missing `-i` exits non-zero; valid args parse; `.bmp` output → error
- [x] 8.2 Implement `src/main.rs` with clap derive struct matching all Go port parameters
- [x] 8.3 Add parameter range validation with clear error messages
- [x] 8.4 Wire `main.rs` through IO → cartoonize → IO
- [x] 8.5 Add verbose mode (`-v`) output with per-stage timing
- [x] 8.6 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test`
- [x] 8.7 MPLR code review — fix all issues before proceeding

## 9. Final Verification

- [x] 9.1 Run `cargo build --release` — zero warnings
- [x] 9.2 Process `test.jpg` with default parameters and verify output file exists and is valid
- [x] 9.3 Run full CLI parameter matrix: edge-thresh 0.005/0.05, sat 1/3, loop 1/3, etc.
- [x] 9.4 Verify: `cargo fmt --check && cargo clippy -- -D warnings && cargo test` — all green

## 10. Global Code Review Loop

- [x] 10.1 Comprehensive MPLR review of entire codebase: check for bugs, logic errors, contradictions, edge cases, performance issues, and spec compliance
- [x] 10.2 If issues found: fix them (eprintln, final clamp, sat validation, spec tolerance), then return to 10.1
- [x] 10.3 No further issues found: review loop complete, project done
