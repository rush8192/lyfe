# UI-200 Rendering Benchmark

Status: baseline recorded; full-system thresholds remain `PERF-200`

This benchmark measures the PixiJS scene work owned by the browser client. It compares the exact
one-marker-per-authorized-organism correctness baseline with a graphics-only, one-marker-per-live-
tile density candidate. The aggregate is intentionally visually distinct from an organism marker;
exact counts and organisms remain available through authorized tile inspection.

Run it from `implementation/v1/client` with:

```bash
npm run benchmark:render
```

The command starts a temporary loopback Vite server, launches an isolated headless Chrome profile,
runs three warmups and seven recorded samples per case, prints machine-readable JSON, closes the
browser, and removes the temporary profile. Set `LYFE_CHROME` when Chrome or Chromium is not in a
recognized platform location.

# Baseline

Recorded 2026-09-10 on macOS/Darwin 24.6.0, Apple M4 Pro, 14 logical cores, 48 GiB memory,
arm64, Headless Chrome 152, PixiJS WebGL at `1280 × 720`. Headless Chrome was allowed to use
SwiftShader, so these numbers establish a reproducible comparison on this host, not a production
GPU or broad device-class promise.

The synthetic authorized projection uses the default `32 × 17` world with all 544 tiles live.
Projection generation is outside the timed region. The UI-205 camera keeps a single scene and
repositions 32 column containers at the periodic seam rather than cloning every entity. Each camera
sample crosses that seam, changes screen-space marker scale, and renders ten frames; the reported
value is time per update-and-render.

| Organisms | Presentation | Display objects | Markers | Build median / p95 | First render median / p95 | Camera update median / p95 |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 10,000 | Exact | 20,577 | 10,000 | 20.4 / 22.4 ms | 28.0 / 36.1 ms | 4.36 / 6.24 ms |
| 10,000 | Tile-density aggregate | 1,665 | 544 | 3.5 / 3.9 ms | 6.1 / 7.4 ms | 0.69 / 0.74 ms |
| 50,000 | Exact | 100,577 | 50,000 | 99.9 / 166.6 ms | 174.3 / 224.8 ms | 19.26 / 41.90 ms |
| 50,000 | Tile-density aggregate | 1,665 | 544 | 1.9 / 6.4 ms | 14.9 / 17.7 ms | 1.87 / 2.87 ms |

# Decision

- Keep exact markers as the present correctness baseline. At 10,000 visible organisms, camera
  updates stayed within one 60 Hz frame on the reference host, although a complete scene rebuild
  did not.
- Keep the 32-column periodic scene layout. A full duplicate seam scene exceeded the bounded
  benchmark window after doubling exact display objects; column recycling makes wrap continuity
  effectively constant in world width and keeps one visual object per authorized entity.
- Do not attempt exact distant rendering at 50,000 organisms: both rebuilding and high-percentile
  camera work exceed a frame by a material margin.
- Retain the graphics-only tile-density glyph as the measured aggregate candidate. An earlier
  text-labelled glyph reduced object count but introduced costly text rendering during camera
  updates, so it was rejected before this baseline was recorded.
- `PERF-200` must choose the actual zoom/entity-count switch and update cadence using a real
  10,000-organism opening, projection apply, React notification, pulses, inspection, charts, and
  representative playtest hardware. This UI-only harness must not be used to claim end-to-end
  frame rate, memory, protocol, or simulation capacity.

# Regression contract

The harness schema is `lyfe.ui-render-benchmark.v1`. Future comparisons retain the JSON output and
record browser/host identity. A regression is investigated when the same-host median or p95 changes
materially across repeated runs; CI timing gates wait for dedicated stable runners under `PERF-200`.
