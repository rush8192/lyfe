import { create } from "@bufbuild/protobuf";
import { Application, Container } from "pixi.js";
import {
  ActorWorldProjectionSchema,
  type ActorWorldProjection,
} from "../generated/lyfe/v1/projection_pb";
import {
  applyWrappedColumnPositions,
  applyMarkerScale,
  createWrappedColumns,
  drawWorldDensityAggregates,
  drawWorldExact,
} from "./WorldCanvas";
import { TILE_SIZE } from "./worldCamera";

const WIDTH = 32;
const HEIGHT = 17;
const SAMPLE_COUNT = 7;
const WARMUP_COUNT = 3;
const CAMERA_UPDATE_REPETITIONS = 10;
const ENTITY_COUNTS = [10_000, 50_000] as const;

interface Sample {
  readonly buildMs: number;
  readonly firstRenderMs: number;
  readonly cameraUpdateMs: number;
  readonly displayObjects: number;
  readonly markerCount: number;
}

interface BenchmarkCase {
  readonly entities: number;
  readonly mode: "exact" | "tile-density-aggregate";
  readonly medianBuildMs: number;
  readonly p95BuildMs: number;
  readonly medianFirstRenderMs: number;
  readonly p95FirstRenderMs: number;
  readonly medianCameraUpdateMs: number;
  readonly p95CameraUpdateMs: number;
  readonly displayObjects: number;
  readonly markerCount: number;
}

async function run() {
  const application = new Application();
  await application.init({
    width: 1280,
    height: 720,
    antialias: false,
    backgroundAlpha: 0,
    autoStart: false,
    preference: "webgl",
  });
  document.querySelector("#benchmark-canvas")?.appendChild(application.canvas);

  const cases: BenchmarkCase[] = [];
  for (const entities of ENTITY_COUNTS) {
    const world = buildWorld(entities);
    cases.push(measureCase(application, world, entities, "exact"));
    cases.push(measureCase(application, world, entities, "tile-density-aggregate"));
  }

  const result = {
    schema: "lyfe.ui-render-benchmark.v1",
    measuredAtUtc: new Date().toISOString(),
    userAgent: navigator.userAgent,
    renderer: application.renderer.type === 1 ? "WebGL" : `Pixi renderer ${application.renderer.type}`,
    viewport: { width: 1280, height: 720 },
    world: { width: WIDTH, height: HEIGHT, liveTiles: WIDTH * HEIGHT },
    samplesPerCase: SAMPLE_COUNT,
    cases,
  };
  document.querySelector("#benchmark-status")!.textContent = "Complete";
  document.querySelector("#benchmark-results")!.textContent = JSON.stringify(result, null, 2);
  document.title = "LYFE_RENDER_BENCHMARK_COMPLETE";
}

function measureCase(
  application: Application,
  world: ActorWorldProjection,
  entities: number,
  mode: BenchmarkCase["mode"],
): BenchmarkCase {
  const samples: Sample[] = [];
  for (let iteration = 0; iteration < WARMUP_COUNT + SAMPLE_COUNT; iteration++) {
    const layer = new Container();
    const buildStarted = performance.now();
    const wrappedColumns = createWrappedColumns(layer, WIDTH);
    const markers = mode === "exact"
      ? drawWorldExact(layer, world, null, null, null, wrappedColumns)
      : drawWorldDensityAggregates(layer, world, null, wrappedColumns);
    const buildMs = performance.now() - buildStarted;
    application.stage.addChild(layer);

    const renderStarted = performance.now();
    application.render();
    const firstRenderMs = performance.now() - renderStarted;

    const cameraStarted = performance.now();
    for (let update = 0; update < CAMERA_UPDATE_REPETITIONS; update++) {
      applyWrappedColumnPositions(
        wrappedColumns,
        update % 2 === 0 ? 20 : WIDTH * TILE_SIZE - 20,
        WIDTH * TILE_SIZE,
      );
      applyMarkerScale(markers, update % 2 === 0 ? 0.25 : 0.3);
      application.render();
    }
    const cameraUpdateMs = (performance.now() - cameraStarted) / CAMERA_UPDATE_REPETITIONS;
    if (iteration >= WARMUP_COUNT) {
      samples.push({
        buildMs,
        firstRenderMs,
        cameraUpdateMs,
        displayObjects: countDisplayObjects(layer),
        markerCount: markers.length,
      });
    }
    application.stage.removeChild(layer);
    layer.destroy({ children: true });
  }
  return {
    entities,
    mode,
    medianBuildMs: percentile(samples.map((sample) => sample.buildMs), 0.5),
    p95BuildMs: percentile(samples.map((sample) => sample.buildMs), 0.95),
    medianFirstRenderMs: percentile(samples.map((sample) => sample.firstRenderMs), 0.5),
    p95FirstRenderMs: percentile(samples.map((sample) => sample.firstRenderMs), 0.95),
    medianCameraUpdateMs: percentile(samples.map((sample) => sample.cameraUpdateMs), 0.5),
    p95CameraUpdateMs: percentile(samples.map((sample) => sample.cameraUpdateMs), 0.95),
    displayObjects: samples[0].displayObjects,
    markerCount: samples[0].markerCount,
  };
}

function buildWorld(entityCount: number): ActorWorldProjection {
  const tileCount = WIDTH * HEIGHT;
  const baseCount = Math.floor(entityCount / tileCount);
  const remainder = entityCount % tileCount;
  let nextOrganismId = 1n;
  return create(ActorWorldProjectionSchema, {
    worldId: 1n,
    width: WIDTH,
    height: HEIGHT,
    tiles: Array.from({ length: tileCount }, (_, tileId) => {
      const count = baseCount + (tileId < remainder ? 1 : 0);
      return {
        tileId,
        x: tileId % WIDTH,
        y: Math.floor(tileId / WIDTH),
        detail: {
          case: "live" as const,
          value: {
            organisms: Array.from({ length: count }, (_, localIndex) => ({
              organismId: nextOrganismId++,
              speciesId: BigInt(1 + tileId % 7),
              positionXQ: coordinate(tileId, localIndex, 2_654_435_761),
              positionYQ: coordinate(tileId, localIndex, 2_246_822_519),
            })),
          },
        },
      };
    }),
  });
}

function coordinate(tileId: number, localIndex: number, multiplier: number): number {
  return Math.imul(tileId * 1_009 + localIndex + 1, multiplier) >>> 0;
}

function countDisplayObjects(root: Container): number {
  let total = 1;
  for (const child of root.children) {
    total += child instanceof Container ? countDisplayObjects(child) : 1;
  }
  return total;
}

function percentile(values: readonly number[], fraction: number): number {
  const ordered = [...values].sort((left, right) => left - right);
  const index = Math.min(ordered.length - 1, Math.ceil(ordered.length * fraction) - 1);
  return Number(ordered[index].toFixed(3));
}

try {
  await run();
} catch (error: unknown) {
  document.querySelector("#benchmark-status")!.textContent = "Failed";
  document.querySelector("#benchmark-results")!.textContent = error instanceof Error
    ? `${error.name}: ${error.message}\n${error.stack ?? ""}`
    : String(error);
  document.title = "LYFE_RENDER_BENCHMARK_FAILED";
}
