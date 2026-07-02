## Context

The MATLAB `Image_cartoonlization` project already has a Go port at `go/`. This design documents the Rust port to be placed at `rs/`. The algorithm is well-understood: saturation adjustment, bilateral filtering in CIELab space, Sobel edge detection, and black edge overlay. The Go port serves as the reference implementation for behavior parity.

## Goals / Non-Goals

**Goals:**
- Single Cargo crate at `rs/` with `lib.rs` (core pipeline) + `main.rs` (CLI binary)
- CLI interface parity with the Go port: identical flags, parameter ranges, and help text
- TDD workflow: tests written before implementation, using `test.jpg` as shared fixture
- Flat `Vec<f64>` internal representation with stride indexing for cache-friendly memory layout
- Rayon-powered parallelism for the bilateral filter hot path
- `cargo fmt` + `cargo clippy` as required gates; warnings treated as errors

**Non-Goals:**
- WASM/web target (may come later, CLI first)
- GPU acceleration (CPU-only, like MATLAB and Go)
- Supporting additional image formats beyond JPEG/PNG
- Streaming or progressive processing
- Library crate for external consumption (single binary focus)

## Decisions

### 1. Data Representation: Flat `Vec<f64>` with Manual Indexing

**Chosen:** `Vec<f64>` with `(y * width + x) * channels + c` indexing, wrapped in `ImageData { width, height, channels, data }`.

**Alternatives considered:**
- `Vec<Vec<[f64;3]>>` (Go-style nested): Simple to port but three-level pointer indirection kills cache locality. Rejected.
- `ndarray::Array3<f64>`: Powerful but heavy dependency for a problem that doesn't need linear algebra. Rejected for simplicity.
- Raw `image::RgbImage` (u8): Can't represent intermediate float values [0,1]. Would require constant casting. Rejected.

**Rationale:** A single contiguous allocation is the Rust idiom for performance-critical image processing. Channel-interleaved layout (RGBRGB...) also plays well with SIMD if added later.

### 2. Module Structure: Flat `src/` with Feature Modules

**Chosen:** Eight `.rs` files in `src/`:
| File | Responsibility |
|------|---------------|
| `data.rs` | `ImageData` type, constructor, indexing helpers |
| `io.rs` | Load/save via `image` crate, `ImageData` ↔ file |
| `lab.rs` | sRGB ↔ CIELab conversion, RGB→grayscale |
| `saturation.rs` | `(1-s)*gray + s*color` per-pixel transform |
| `bilateral.rs` | Dispatcher + `bilat_color` + `bilat_gray` with rayon |
| `edge.rs` | Sobel edge detection + edge overlay |
| `cartoon.rs` | `Cartoonize()` pipeline orchestrator + `Params` |
| `main.rs` | clap CLI, calls `cartoonize`, handles I/O |

**Rationale:** Mirrors the Go package structure (one module per concern), proven clean. Each file is ~50-100 lines, testable in isolation.

### 3. Parallelism: Rayon Per-Row Slices

**Chosen:** `rayon::par_chunks_mut(width * 3)` on the output buffer for bilateral filter rows.

**Go equivalent:** Goroutine pool with per-worker row ranges — same strategy, idiomatic to each language.

**Rationale:** Row-level parallelism avoids false sharing (each row is independently computed), doesn't require locks, and `par_chunks_mut` is safe by construction (Rust borrow checker guarantees no overlap).

### 4. Color Conversion: Manual Implementation (Not `palette` Crate)

**Chosen:** Hand-written sRGB→Linear→XYZ→Lab chain with D65 white point constants. Copy the verified matrix from Go `lab.go`.

**Alternatives considered:**
- `palette` crate: Well-tested but adds a dependency for a ~100-line conversion. Also off-by-epsilon differences could break regression tests against Go output.
- **Rationale:** The conversion is simple math, no need for a full color library. Manual implementation guarantees bit-identical results with Go.

### 5. CLI: `clap` Derive Mode

**Chosen:** `#[derive(Parser)]` struct with all flags matching Go's `flag` package.

**Rationale:** `clap` derive is the Rust standard. It generates `--help` automatically, supports `-v` / `--verbose`, and has built-in validation via `#[arg(value_parser = ...)]`.

### 6. Testing Strategy: TDD with Property + Integration Tests

**Chosen:** Three test tiers:
1. **Unit tests** (`#[cfg(test)]` in each module) — pure functions: Lab conversion matrices, Sobel kernel, saturation formula. Tested with known inputs/expected outputs.
2. **Property tests** — invariants: size preservation, value clamping to [0,1], round-trip identity for color conversion.
3. **Integration tests** — `test.jpg` fixture: load, process each pipeline stage, verify output dimensions and properties.

**Rationale:** Property tests catch regressions without needing golden files. Once the pipeline is correct, we can optionally add a hash-based regression test against known-good output.

### 7. TDD Development Flow

Each phase is write-test → write-code → `cargo fmt` → `cargo clippy` → `cargo test`. No code is written without a failing test first.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `image` crate version churn | Pin to `0.25` which is stable and widely used |
| Color conversion float differences vs Go | Use same literal constants from Go `lab.go`; accept ±1e-12 error in tests |
| Bilateral filter O(n²) — slow on large images | Rayon parallelization; profile early; accept that 2812×1280 with radius=10 takes ~seconds |
| `test.jpg` is 2812×1280 — slow for unit tests | Unit tests use small synthetic images (4×4, 3×3). Integration test uses `test.jpg` only once |

## Open Questions

None — all decisions resolved during explore phase.
