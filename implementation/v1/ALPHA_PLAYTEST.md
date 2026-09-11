# Local alpha playtest

Status: developer-explorable build; `PERF-200` remains the Stage-B internal-alpha readiness gate

This is the canonical local launch and first-session checklist. The first structured sessions are
deliberately allowed to expose awkward copy and layout. Record those findings before `COPY-200` and
`UI-210`; do not silently turn them into simulation facts or mechanics changes.

# Prerequisites

- Docker Desktop or another Docker Engine with Compose
- Node.js `24.20.x` and npm `11.19.x`
- A Chromium, Firefox, or Safari-class desktop browser; the current automated smoke uses Chromium

Native .NET is not required for playtesting because Compose builds and runs the authoritative host.

# Launch

From `implementation/v1`:

```sh
docker compose up --build -d
cd client
npm ci
npm run build
npm run preview -- --host 127.0.0.1
```

Open <http://127.0.0.1:5173>. The Vite client proxies authoritative requests to the Compose server
on <http://127.0.0.1:5080>. A quick server check is:

```sh
curl --fail http://127.0.0.1:5080/health
```

Use `Ctrl-C` in the client terminal when finished, then stop the server from
`implementation/v1`:

```sh
docker compose stop
```

The named `lyfe-data` volume intentionally retains saved worlds between Compose starts.

Use `npm run dev -- --host 127.0.0.1` instead when actively editing. It has the same proxy and port,
but it also enables React/Vite development instrumentation and hot replacement; the production
preview above is the canonical playtest surface.

The normal player surface intentionally omits projection diagnostics. To inspect the authoritative
lifecycle label, visible-projection count, tick duration, target cadence, and control revision while
developing, start the development client with:

```sh
VITE_LYFE_DEBUG_UI=true npm run dev -- --host 127.0.0.1
```

This flag changes presentation only. It does not enter simulation configuration, saves, protocol
state, hashes, or replay.

# First-session route

1. Create both a Sandbox and a Survival world, varying metabolism and opening emphasis.
2. Step while paused, then run at each speed; confirm the hour and boundary advance coherently.
3. Select known and unknown tiles, inspect a visible organism, and trace resource and pressure
   evidence without assuming hidden conditions.
4. Zoom and pan through the horizontal seam. Enable organism follow, step once, confirm follow is
   retained, then pan or focus a tile and confirm it releases. Zoom alone should retain follow.
5. Save a completed boundary, advance it, reload the save, and confirm the earlier hour and
   population return. Browser-local camera/selection preferences are not part of that save.
6. Save an evolution goal, compare its current-rate estimate with the explicit caveat, and inspect
   prerequisite closure. When affordable, preview and found a descendant, then follow its
   consequence review and chronicle evidence.
7. Stop the server briefly and observe the held-boundary/recovery language. Restart it and use the
   offered recovery action; do not treat an unconfirmed command as safely retryable.

# What to record

For each finding, capture the mode, seed, simulated hour, saved-world ID if applicable, browser and
input device, intended action, observed result, and whether the issue concerns comprehension,
pacing, performance, input/accessibility, copy, or a factual/mechanical defect. Screenshots are
useful; behavioral telemetry is out of scope until a privacy and consent policy exists.

# Current readiness boundary

- Complete: UI-200 player loop, OPENING-210 cohort desynchronization, UI-205 wrapped camera/follow/
  local preferences, client production build and automated tests, solution tests, and a live
  Chromium create/select/follow/step/zoom/pan/save smoke.
- Still required for the Stage-B internal-alpha gate: `PERF-200` end-to-end default-grid profiling
  around 10,000 organisms, including tick, memory, GC, save, projection, wire, apply, and frame work.
- Intentionally after the first structured sessions: `COPY-200` voice/mechanics editorial pass and
  `UI-210` evidence-based interface polish.
- Device follow-up: repeat wheel/trackpad direction, two-finger pinch, touch target, forced-colors,
  reduced-motion, and screen-reader checks on the actual playtest hardware. The keyboard and
  Chromium accessibility-tree baseline is automated/smoke-tested, not a supported-device matrix.
- Known scale limit: exact 50,000-organism rendering exceeds a 60 Hz frame on the reference host;
  the measured tile-density candidate is available, but its switch threshold belongs to `PERF-200`.

# Readiness audit — 2026-09-11

Disposition: **go for developer exploratory playtesting; not yet through the Stage-B representative-
scale gate.** `PERF-200` is the remaining blocker for a declared internal-alpha candidate. Copy is
intentional observation material for the first structured sessions, not a gate for those sessions.

| Check | Result |
| --- | --- |
| Client unit/DOM suite | 25 files, 101 tests passed |
| Client production build | Passed; the existing 500 kB chunk-size advisory remains |
| Complete .NET solution suite | 226 passed: 178 simulation, 36 server, 12 architecture |
| Portable host | Compose configuration, image rebuild, health/client connection, setup, tick, save, and production-preview restart passed |
| UI-205 browser smoke | Chromium accessibility tree and create/select/follow/step/zoom/pan flow passed |
| UI-205 rendering benchmark | 10,000 exact camera update 4.36 ms median / 6.24 ms p95 on the reference M4 Pro; 50,000 exact remains over-frame |
| 64-seed, 720-hour opening matrix | No failure observed, but the optional re-run exceeded a 17-minute bounded audit window and was cancelled; the earlier recorded `384/384` result was not re-certified by this audit |

The long matrix result does not invalidate its existing deterministic fixture record. Its runtime is,
however, additional evidence that representative duration and resource measurements belong in
`PERF-200` rather than in every quick local playtest preflight.

The first playtest finding was accepted and corrected after this audit: generated worlds expose
centered signed grid coordinates, but the initial UI-205 renderer treated them as zero-based canvas
coordinates. Fit/focus, drawing, organism follow, pulses, and inverse hit-testing now share a
normalized presentation origin while inspectors continue to show the authoritative signed values.
A signed-grid regression fixture and a production-browser Fit/Focus visual check pass.

The second camera finding was also accepted: the fixed `1120 CSS px` application shell and short,
side-by-side map made the usable world viewport feel cramped. The active shell now uses responsive
near-edge gutters; on wide screens the camera receives the flexible column beside a bounded
`320–430 CSS px` evidence rail, and its height scales from `560` to `820 CSS px` with the browser.
At `1050 CSS px` and below the evidence rail moves beneath the map instead of narrowing it. PixiJS
measures the resulting canvas box directly, so fit, focus, hit-testing, and anchored zoom use the
same responsive dimensions that the player sees. Production-browser checks pass at the wide and
stacked layout boundary represented by the supported test surface.

The third camera finding consolidated the adjacent rail around watching the simulation. Decorative
foundation/biome copy and the duplicate lifecycle row were removed. The current hour, one
Pause/Resume state-and-action control, Step, Speed, and the complete live/last-known/unknown map key
now remain above the fold beside the map; activity filters and journey evidence follow in a bounded
scrolling rail. Raw lifecycle, visible-projection count, tick duration, cadence, and control revision
are available only through the optional presentation-only debug UI flag documented above.

The fourth exploratory finding established the species-observation flow. The default rail now shows
the controlled species' exact total living count, average health, and occupied-tile count. A direct
map hit on a green controlled organism replaces the rail with that organism's high-level state and
journey evidence; Back returns to the default, and no dropdown, restored preference, or keyboard
shortcut can enter organism inspection. Other species render red, and a direct hit opens only their
species-level observed count, average health, and occupied-tile count. That summary explicitly covers
members on shared live tiles, with hidden population and locations unknown, and is removed when the
species leaves all shared visibility. Browser preferences were versioned forward to retain camera
and tile selection only. A world without a saved camera opens focused on the controlled lineage's
starting tile; Fit remains available for the world overview.

The fifth exploratory finding adds legible volcanic geography. Authorized live tiles now carry
their compiled baseline volcanism and render one to sixteen deterministic, static wireframe vent
clusters behind organisms and remnants. Count rises with normalized activity, while placement is
stable for a tile and therefore does not shimmer across projection refreshes. The compact map key
explains the encoding. Unknown tiles expose no vent art; reduced/remembered volcanic knowledge stays
with `KNOW-200` rather than being inferred from hidden current state.

The sixth exploratory finding removed fast-refresh flicker. The map now owns one persistent Pixi
application and canvas, constructs each authoritative boundary as a complete replacement scene, and
installs it before destroying the prior scene. Retired activity-pulse callbacks are removed at the
same boundary. The original `250 ms` Fast cadence was a provisional, unmeasured bridge value rather
than a demonstrated limit; Fast now targets a bounded `100 ms` per tick and the browser refresh floor
matches it. The host retains no catch-up debt, so expensive ticks reduce achieved speed instead of
queueing an unbounded burst. `PERF-200` still owns representative-scale cadence calibration.

The seventh exploratory finding removed activity-symbol replay during presentation-only map redraws.
Each authoritative pulse event now receives one wall-clock start time keyed by its event identity.
Selection, camera, and filter redraws reuse that age, and expired symbols remain absent; only a new
authoritative event identity begins a new pulse animation.
