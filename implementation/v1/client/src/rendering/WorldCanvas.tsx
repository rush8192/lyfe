import { Application, Container, Graphics, Text } from "pixi.js";
import { useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react";
import {
  OrganismJourneyEventFamily,
  type ActorWorldProjection,
  type OrganismJourneyEvent,
  type OrganismProjection,
  type RemnantProjection,
  type TileProjection,
} from "../generated/lyfe/v1/projection_pb";
import {
  TILE_SIZE,
  cameraLevel,
  clampCamera,
  fitCamera,
  focusTileCamera,
  panCamera,
  restoreCamera,
  screenToWorld,
  wrappedTileCoordinatesAt,
  worldToScreenWrappedX,
  zoomCameraAt,
  type CameraState,
  type Point,
  type ViewportSize,
  type WorldExtent,
} from "./worldCamera";
import { selectActivityPulseEvents } from "../components/ActivityJourneyPanel";
import {
  browserWorldViewPreferenceStorage,
  readWorldViewPreferences,
  updateWorldViewPreferences,
} from "../state/worldViewPreferences";

const UINT32_SCALE = 0xffff_ffff;
const VOLCANISM_SCALE = 1_000_000;
const VOLCANIC_VENT_STEP_Q = 62_500;
const MAX_VOLCANIC_VENTS = 16;
type LiveTileProjection = TileProjection & {
  detail: Extract<TileProjection["detail"], { case: "live" }>;
};

interface WorldCanvasProps {
  readonly world: ActorWorldProjection | null;
  readonly enabledEventFamilies: ReadonlySet<OrganismJourneyEventFamily>;
  readonly selectedTileId?: number | null;
  readonly selectedOrganismId?: bigint | null;
  readonly selectedRemnantId?: bigint | null;
  readonly selectedSpeciesId?: bigint | null;
  readonly focusRequestRevision?: number;
  readonly followingOrganism?: boolean;
  readonly onSelectTile?: (tileId: number) => void;
  readonly onSelectOrganism?: (organismId: bigint, tileId: number) => void;
  readonly onSelectRemnant?: (remnantId: bigint, tileId: number) => void;
  readonly onFollowingOrganismChange?: (following: boolean) => void;
}

interface RenderState {
  readonly layer: Container;
  readonly wrappedColumns: readonly WrappedColumn[];
  readonly markers: readonly Container[];
  readonly viewport: ViewportSize;
  readonly extent: WorldExtent;
  readonly fittedZoom: number;
}

export interface WrappedColumn {
  readonly node: Container;
  readonly baseX: number;
}

interface CameraSummary {
  readonly level: "World" | "Region" | "Tile";
  readonly relativeZoom: number;
}

export function WorldCanvas({
  world,
  enabledEventFamilies,
  selectedTileId = null,
  selectedOrganismId = null,
  selectedRemnantId = null,
  selectedSpeciesId = null,
  focusRequestRevision = 0,
  followingOrganism = false,
  onSelectTile,
  onSelectOrganism,
  onSelectRemnant,
  onFollowingOrganismChange,
}: WorldCanvasProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const applicationRef = useRef<Application | null>(null);
  const cameraRef = useRef<CameraState | null>(null);
  const renderRef = useRef<RenderState | null>(null);
  const pulseCleanupRef = useRef<() => void>(() => undefined);
  const pulseStartedAtRef = useRef<Map<bigint, number>>(new Map());
  const pulseWorldIdRef = useRef<bigint | null>(null);
  const previousWorldRef = useRef<bigint | null>(null);
  const appliedFocusRevisionRef = useRef(0);
  const lastActivationRef = useRef<{ readonly tileId: number; readonly at: number } | null>(null);
  const followingRef = useRef(followingOrganism);
  const onSelectTileRef = useRef(onSelectTile);
  const onSelectOrganismRef = useRef(onSelectOrganism);
  const onSelectRemnantRef = useRef(onSelectRemnant);
  const onFollowingOrganismChangeRef = useRef(onFollowingOrganismChange);
  const worldRef = useRef(world);
  followingRef.current = followingOrganism;
  onSelectTileRef.current = onSelectTile;
  onSelectOrganismRef.current = onSelectOrganism;
  onSelectRemnantRef.current = onSelectRemnant;
  onFollowingOrganismChangeRef.current = onFollowingOrganismChange;
  worldRef.current = world;
  const [rendererRevision, setRendererRevision] = useState(0);
  const [viewportRevision, setViewportRevision] = useState(0);
  const [cameraSummary, setCameraSummary] = useState<CameraSummary>({
    level: "World",
    relativeZoom: 1,
  });

  const selectedTile = useMemo(
    () => world?.tiles.find((tile) => tile.tileId === selectedTileId) ?? null,
    [selectedTileId, world],
  );
  const selectedOrganism = useMemo(
    () => world === null || selectedOrganismId === null
      ? null
      : findLiveOrganism(world, selectedOrganismId),
    [selectedOrganismId, world],
  );
  const selectedTileRef = useRef(selectedTile);
  const selectedOrganismRef = useRef(selectedOrganism);
  selectedTileRef.current = selectedTile;
  selectedOrganismRef.current = selectedOrganism;

  function commitCamera(camera: CameraState, persist = true) {
    cameraRef.current = camera;
    const render = renderRef.current;
    if (render === null) return;
    applyCamera(render, camera);
    setCameraSummary({
      level: cameraLevel(camera, render.fittedZoom),
      relativeZoom: camera.zoom / render.fittedZoom,
    });
    const currentWorld = worldRef.current;
    if (persist && currentWorld !== null) {
      const storage = browserWorldViewPreferenceStorage();
      if (storage !== null) {
        updateWorldViewPreferences(storage, currentWorld.worldId, {
          camera: {
            centerXFraction: camera.centerX / render.extent.width,
            centerYFraction: camera.centerY / render.extent.height,
            relativeZoom: camera.zoom / render.fittedZoom,
          },
        });
      }
    }
  }

  function releaseFollow() {
    if (!followingRef.current) return;
    followingRef.current = false;
    onFollowingOrganismChangeRef.current?.(false);
  }

  function adjustZoom(factor: number, anchor?: Point) {
    const camera = cameraRef.current;
    const render = renderRef.current;
    if (camera === null || render === null) return;
    commitCamera(zoomCameraAt(
      camera,
      factor,
      anchor ?? { x: render.viewport.width / 2, y: render.viewport.height / 2 },
      render.extent,
      render.viewport,
      render.fittedZoom,
    ));
  }

  function fitWorld() {
    const render = renderRef.current;
    if (render === null) return;
    releaseFollow();
    commitCamera(fitCamera(render.extent, render.viewport));
  }

  function focusSelectedTile() {
    const render = renderRef.current;
    const camera = cameraRef.current;
    const currentTile = selectedTileRef.current;
    const currentWorld = worldRef.current;
    if (render === null || camera === null || currentTile === null || currentWorld === null) return;
    releaseFollow();
    commitCamera(focusTileCamera(
      camera,
      presentationTilePosition(currentTile, worldPresentationOrigin(currentWorld)),
      render.extent,
      render.viewport,
      render.fittedZoom,
    ));
  }

  function toggleFollow() {
    const currentOrganism = selectedOrganismRef.current;
    if (currentOrganism === null) return;
    const next = !followingRef.current;
    followingRef.current = next;
    onFollowingOrganismChangeRef.current?.(next);
    if (!next) return;
    const render = renderRef.current;
    const camera = cameraRef.current;
    if (render === null || camera === null) return;
    commitCamera(clampCamera({
      ...camera,
      centerX: currentOrganism.point.x,
      centerY: currentOrganism.point.y,
    }, render.extent, render.viewport, render.fittedZoom));
  }

  function selectCenterTile() {
    const render = renderRef.current;
    const currentWorld = worldRef.current;
    if (currentWorld === null || render === null) return;
    selectAtScreenPoint(
      center(render.viewport),
      currentWorld,
      cameraRef.current,
      render.viewport,
      onSelectTileRef.current,
    );
  }

  useEffect(() => {
    const target = hostRef.current;
    if (target === null) return;
    const stableTarget: HTMLDivElement = target;
    const application = new Application();
    let disposed = false;
    let initialized = false;
    let resizeObserver: ResizeObserver | null = null;

    async function initialize() {
      await application.init({
        antialias: true,
        backgroundAlpha: 0,
        resizeTo: stableTarget,
      });
      initialized = true;
      if (disposed) {
        application.destroy(true);
        return;
      }

      applicationRef.current = application;
      const pointers = new Map<number, Point>();
      let movement = 0;
      let cameraChanged = false;
      let suppressSelection = false;
      let pinchStart: { readonly midpoint: Point; readonly distance: number } | null = null;
      const canvas = application.canvas;
      canvas.className = "world-canvas-surface";
      canvas.setAttribute("aria-hidden", "true");

      const pointerPosition = (event: PointerEvent | MouseEvent | WheelEvent): Point => {
        const bounds = canvas.getBoundingClientRect();
        const viewport = renderRef.current?.viewport ?? {
          width: Math.max(320, stableTarget.clientWidth),
          height: Math.max(1, stableTarget.clientHeight),
        };
        return {
          x: (event.clientX - bounds.left) * (viewport.width / bounds.width),
          y: (event.clientY - bounds.top) * (viewport.height / bounds.height),
        };
      };
      const onPointerDown = (event: PointerEvent) => {
        stableTarget.focus({ preventScroll: true });
        canvas.setPointerCapture(event.pointerId);
        pointers.set(event.pointerId, pointerPosition(event));
        movement = 0;
        if (pointers.size === 1) cameraChanged = false;
        suppressSelection = pointers.size > 1;
        if (pointers.size === 2) {
          const points = [...pointers.values()];
          pinchStart = { midpoint: midpoint(points[0], points[1]), distance: distance(points[0], points[1]) };
        }
      };
      const onPointerMove = (event: PointerEvent) => {
        const previous = pointers.get(event.pointerId);
        if (previous === undefined) return;
        const next = pointerPosition(event);
        const priorPointers = [...pointers.values()];
        pointers.set(event.pointerId, next);
        const nextPointers = [...pointers.values()];
        const render = renderRef.current;
        const current = cameraRef.current;
        if (render === null || current === null) return;

        if (nextPointers.length === 1) {
          const delta = { x: next.x - previous.x, y: next.y - previous.y };
          movement += Math.hypot(delta.x, delta.y);
          if (Math.hypot(delta.x, delta.y) > 0) releaseFollow();
          cameraChanged = true;
          commitCamera(panCamera(
            current,
            delta,
            render.extent,
            render.viewport,
            render.fittedZoom,
          ), false);
        } else if (nextPointers.length === 2 && priorPointers.length === 2) {
          suppressSelection = true;
          const priorMidpoint = midpoint(priorPointers[0], priorPointers[1]);
          const nextMidpoint = midpoint(nextPointers[0], nextPointers[1]);
          const priorDistance = distance(priorPointers[0], priorPointers[1]);
          const nextDistance = distance(nextPointers[0], nextPointers[1]);
          if (pinchStart !== null && pinchGestureIncludesPan(
            pinchStart.midpoint,
            pinchStart.distance,
            nextMidpoint,
            nextDistance,
          )) releaseFollow();
          cameraChanged = true;
          let changed = panCamera(
            current,
            { x: nextMidpoint.x - priorMidpoint.x, y: nextMidpoint.y - priorMidpoint.y },
            render.extent,
            render.viewport,
            render.fittedZoom,
          );
          if (priorDistance > 0) {
            changed = zoomCameraAt(
              changed,
              nextDistance / priorDistance,
              nextMidpoint,
              render.extent,
              render.viewport,
              render.fittedZoom,
            );
          }
          commitCamera(changed, false);
        }
      };
      const onPointerUp = (event: PointerEvent) => {
        const point = pointers.get(event.pointerId);
        pointers.delete(event.pointerId);
        if (pointers.size < 2) pinchStart = null;
        const currentWorld = worldRef.current;
        const render = renderRef.current;
        if (point !== undefined && movement < 5 && !suppressSelection &&
            currentWorld !== null && render !== null) {
          const tile = selectAtScreenPoint(
            point,
            currentWorld,
            cameraRef.current,
            render.viewport,
            onSelectTileRef.current,
            onSelectOrganismRef.current,
            onSelectRemnantRef.current,
          );
          const now = performance.now();
          const previousActivation = lastActivationRef.current;
          if (tile !== null && previousActivation?.tileId === tile.tileId &&
              now - previousActivation.at <= 400) {
            const camera = cameraRef.current;
            if (camera !== null) {
              commitCamera(focusTileCamera(
                camera,
                presentationTilePosition(tile, worldPresentationOrigin(currentWorld)),
                render.extent,
                render.viewport,
                render.fittedZoom,
              ));
              releaseFollow();
            }
          }
          lastActivationRef.current = tile === null ? null : { tileId: tile.tileId, at: now };
        }
        const camera = cameraRef.current;
        if (camera !== null && cameraChanged && pointers.size === 0) commitCamera(camera);
        if (pointers.size === 0) suppressSelection = false;
      };
      const onWheel = (event: WheelEvent) => {
        event.preventDefault();
        adjustZoom(Math.exp(-event.deltaY * 0.0015), pointerPosition(event));
      };
      const onDoubleClick = (event: MouseEvent) => {
        const currentWorld = worldRef.current;
        const camera = cameraRef.current;
        const render = renderRef.current;
        if (currentWorld === null || camera === null || render === null) return;
        const coordinates = wrappedTileCoordinatesAt(
          screenToWorld(pointerPosition(event), camera, render.viewport),
          currentWorld.width,
          currentWorld.height,
        );
        const origin = worldPresentationOrigin(currentWorld);
        const tile = coordinates === null
          ? undefined
          : currentWorld.tiles.find((candidate) =>
              candidate.x === coordinates.x + origin.x &&
              candidate.y === coordinates.y + origin.y);
        if (tile === undefined) return;
        onSelectTileRef.current?.(tile.tileId);
        releaseFollow();
        commitCamera(focusTileCamera(
          camera,
          presentationTilePosition(tile, origin),
          render.extent,
          render.viewport,
          render.fittedZoom,
        ));
      };

      canvas.addEventListener("pointerdown", onPointerDown);
      canvas.addEventListener("pointermove", onPointerMove);
      canvas.addEventListener("pointerup", onPointerUp);
      canvas.addEventListener("pointercancel", onPointerUp);
      canvas.addEventListener("wheel", onWheel, { passive: false });
      canvas.addEventListener("dblclick", onDoubleClick);
      stableTarget.appendChild(canvas);
      if (typeof ResizeObserver !== "undefined") {
        resizeObserver = new ResizeObserver(() =>
          setViewportRevision((revision) => revision + 1));
        resizeObserver.observe(stableTarget);
      }
      setRendererRevision((revision) => revision + 1);
    }

    void initialize();
    return () => {
      disposed = true;
      resizeObserver?.disconnect();
      pulseCleanupRef.current();
      pulseCleanupRef.current = () => undefined;
      applicationRef.current = null;
      renderRef.current = null;
      if (initialized) application.destroy(true, { children: true });
    };
  }, []);

  useEffect(() => {
    const application = applicationRef.current;
    const target = hostRef.current;
    if (application === null || target === null) return;
    const viewport = {
      width: Math.max(320, target.clientWidth),
      height: Math.max(1, target.clientHeight),
    };
    const previousRender = renderRef.current;
    const previousPulseCleanup = pulseCleanupRef.current;
    const layer = new Container();
    let nextRender: RenderState;
    let nextPulseCleanup: () => void = () => undefined;

    if (world === null) {
      layer.addChild(new Graphics()
        .roundRect(28, 28, Math.max(80, viewport.width - 56), 308, 24)
        .fill({ color: 0x102d35, alpha: 0.78 })
        .stroke({ color: 0x47766f, width: 2, alpha: 0.4 }));
      nextRender = {
        layer,
        wrappedColumns: [],
        markers: [],
        viewport,
        extent: viewport,
        fittedZoom: 1,
      };
      cameraRef.current = null;
      previousWorldRef.current = null;
      pulseStartedAtRef.current.clear();
      pulseWorldIdRef.current = null;
    } else {
      const extent = { width: world.width * TILE_SIZE, height: world.height * TILE_SIZE };
      const fitted = fitCamera(extent, viewport);
      const worldChanged = previousWorldRef.current !== world.worldId;
      const storage = browserWorldViewPreferenceStorage();
      const stored = storage === null
        ? null
        : readWorldViewPreferences(storage, world.worldId)?.camera;
      let camera = worldChanged || cameraRef.current === null
        ? stored === undefined || stored === null
          ? defaultWorldCamera(world, viewport)
          : restoreCamera(stored, extent, viewport)
        : clampCamera(cameraRef.current, extent, viewport, fitted.zoom);
      previousWorldRef.current = world.worldId;
      if (pulseWorldIdRef.current !== world.worldId) {
        pulseStartedAtRef.current.clear();
        pulseWorldIdRef.current = world.worldId;
      }
      reconcileActivityPulseStarts(
        pulseStartedAtRef.current,
        world.activityPulseEvents,
        performance.now(),
      );

      if (focusRequestRevision > appliedFocusRevisionRef.current && selectedTile !== null) {
        camera = focusTileCamera(
          camera,
          presentationTilePosition(selectedTile, worldPresentationOrigin(world)),
          extent,
          viewport,
          fitted.zoom,
        );
        appliedFocusRevisionRef.current = focusRequestRevision;
        releaseFollow();
      } else if (followingRef.current && selectedOrganism !== null) {
        camera = clampCamera({
          ...camera,
          centerX: selectedOrganism.point.x,
          centerY: selectedOrganism.point.y,
        }, extent, viewport, fitted.zoom);
      }

      const wrappedColumns = createWrappedColumns(layer, world.width);
      const markers = drawWorldExact(
        layer,
        world,
        selectedTileId,
        selectedOrganismId,
        selectedSpeciesId,
        wrappedColumns,
        selectedRemnantId,
      );
      nextPulseCleanup = addActivityPulses(
        application,
        layer,
        world,
        enabledEventFamilies,
        markers,
        layer,
        wrappedColumns,
        pulseStartedAtRef.current,
      );
      nextRender = {
        layer,
        wrappedColumns,
        markers,
        viewport,
        extent,
        fittedZoom: fitted.zoom,
      };
      cameraRef.current = camera;
      applyCamera(nextRender, camera);
      setCameraSummary({
        level: cameraLevel(camera, fitted.zoom),
        relativeZoom: camera.zoom / fitted.zoom,
      });
    }

    renderRef.current = nextRender;
    pulseCleanupRef.current = nextPulseCleanup;
    previousPulseCleanup();
    swapSceneLayer(application.stage, previousRender?.layer ?? null, layer);
  }, [
    enabledEventFamilies,
    focusRequestRevision,
    rendererRevision,
    selectedOrganismId,
    selectedRemnantId,
    selectedOrganism,
    selectedSpeciesId,
    selectedTile,
    selectedTileId,
    viewportRevision,
    world,
  ]);

  const visibleCount = world?.tiles.reduce(
    (total, tile) => total + (tile.detail.case === "live" ? tile.detail.value.organisms.length : 0),
    0,
  ) ?? 0;

  return (
    <section className="world-map" aria-label="World map">
      <div className="world-map-toolbar" aria-label="Camera controls">
        <div className="camera-buttons">
          <button type="button" onClick={() => adjustZoom(1 / 1.45)} disabled={world === null} aria-label="Zoom out">−</button>
          <button type="button" onClick={() => adjustZoom(1.45)} disabled={world === null} aria-label="Zoom in">+</button>
          <button type="button" onClick={fitWorld} disabled={world === null}>Fit world</button>
          <button type="button" onClick={focusSelectedTile} disabled={selectedTile === null}>Focus tile</button>
          <button
            type="button"
            onClick={toggleFollow}
            disabled={selectedOrganism === null}
            aria-pressed={followingOrganism}
          >
            {followingOrganism ? "Following organism" : "Follow organism"}
          </button>
        </div>
        <span className="camera-readout" aria-live="polite">
          {cameraSummary.level} · {cameraSummary.relativeZoom.toFixed(1)}×
        </span>
      </div>
      <div
        className="world-canvas"
        ref={hostRef}
        role="group"
        tabIndex={world === null ? -1 : 0}
        onKeyDown={(event) => handleCameraKey(
          event,
          renderRef.current,
          cameraRef.current,
          commitCamera,
          fitWorld,
          focusSelectedTile,
          selectCenterTile,
          releaseFollow,
        )}
        aria-describedby={world === null ? undefined : "world-map-keyboard-help"}
        aria-keyshortcuts={world === null
          ? undefined
          : "ArrowLeft ArrowRight ArrowUp ArrowDown 0 Space Enter"}
        aria-label={
          world === null
            ? "Waiting for the actor-authorized world projection"
            : `Interactive world grid containing ${visibleCount} visible simulated organisms. Drag to pan, scroll or pinch to zoom, and select a tile.`
        }
      />
      {world === null ? null : (
        <>
          <span className="map-keyboard-reticle" aria-hidden="true" />
          <p id="world-map-keyboard-help" className="sr-only">
            Arrow keys pan the wrapped map and release organism follow. Plus and minus zoom
            without releasing follow. Space selects the tile beneath the center marker. Enter
            focuses the selected tile and releases follow. Zero fits the whole world.
          </p>
        </>
      )}
      <TileSelectionSummary
        tile={selectedTile}
        selectedOrganismId={selectedOrganismId}
        selectedRemnantId={selectedRemnantId}
        selectedSpeciesId={selectedSpeciesId}
      />
    </section>
  );
}

export function swapSceneLayer(
  stage: Container,
  previousLayer: Container | null,
  nextLayer: Container,
): void {
  stage.addChild(nextLayer);
  if (previousLayer === null) return;
  stage.removeChild(previousLayer);
  previousLayer.destroy({ children: true });
}

export function drawWorldExact(
  layer: Container,
  world: ActorWorldProjection,
  selectedTileId: number | null,
  selectedOrganismId: bigint | null,
  selectedSpeciesId: bigint | null = null,
  wrappedColumns: readonly WrappedColumn[] = [],
  selectedRemnantId: bigint | null = null,
): Container[] {
  const markers: Container[] = [];
  const origin = worldPresentationOrigin(world);
  for (const tile of world.tiles) {
    const displayTile = presentationTilePosition(tile, origin);
    const target = wrappedColumns[displayTile.x]?.node ?? layer;
    const left = wrappedColumns.length === 0 ? displayTile.x * TILE_SIZE : 0;
    const top = displayTile.y * TILE_SIZE;
    const color = tile.detail.case === "live"
      ? 0x123f52
      : tile.detail.case === "reduced"
        ? 0x243b3d
        : 0x111c1f;
    const tileGraphic = new Graphics()
      .rect(left, top, TILE_SIZE, TILE_SIZE)
      .fill({ color, alpha: 0.94 })
      .stroke({
        color: tile.tileId === selectedTileId ? 0xf1cf87 : 0x58c4a3,
        width: tile.tileId === selectedTileId ? 4 : 1,
        alpha: tile.tileId === selectedTileId ? 1 : 0.38,
        pixelLine: true,
      });
    target.addChild(tileGraphic);

    if (tile.detail.case === "live") {
      const vents = createVolcanicVentLayer(
        tile.tileId,
        tile.detail.value.baselineVolcanismQ,
        left,
        top,
      );
      if (vents !== null) target.addChild(vents);
    }

    if (tile.detail.case === "reduced") {
      target.addChild(new Graphics()
        .circle(left + TILE_SIZE / 2, top + TILE_SIZE / 2, 10)
        .fill({ color: 0x8ba39d, alpha: 0.18 }));
    } else if (tile.detail.case !== "live") {
      target.addChild(new Graphics()
        .moveTo(left + 18, top + 18)
        .lineTo(left + TILE_SIZE - 18, top + TILE_SIZE - 18)
        .moveTo(left + TILE_SIZE - 18, top + 18)
        .lineTo(left + 18, top + TILE_SIZE - 18)
        .stroke({ color: 0x66807a, width: 1, alpha: 0.2, pixelLine: true }));
    }

    if (tile.detail.case !== "live") continue;
    for (const organism of tile.detail.value.organisms) {
      const position = organismWorldPosition(tile, organism, origin);
      const marker = new Container({
        x: wrappedColumns.length === 0 ? position.x : position.x - displayTile.x * TILE_SIZE,
        y: position.y,
      });
      const selected = organism.organismId === selectedOrganismId ||
        (selectedOrganismId === null && organism.speciesId === selectedSpeciesId);
      const markerColor = organismMarkerColor(organism.speciesId, world.controlledSpeciesId);
      const graphic = new Graphics()
        .circle(0, 0, selected ? 5.5 : 3.2)
        .fill({ color: markerColor, alpha: 0.92 });
      if (selected) graphic.stroke({ color: 0xfff0c3, width: 1.4, alpha: 1 });
      marker.addChild(graphic);
      target.addChild(marker);
      markers.push(marker);
    }
    for (const remnant of tile.detail.value.remnants) {
      const position = remnantWorldPosition(tile, remnant, origin);
      const marker = new Container({
        x: wrappedColumns.length === 0 ? position.x : position.x - displayTile.x * TILE_SIZE,
        y: position.y,
      });
      const selected = remnant.remnantId === selectedRemnantId;
      marker.addChild(new Graphics()
        .poly([-3.6, 0, 0, -2.7, 3.6, 0, 0, 2.7])
        .fill({ color: 0xb99272, alpha: 0.8 })
        .stroke({
          color: selected ? 0xfff0c3 : 0xe1bea0,
          width: selected ? 1.5 : 0.8,
          alpha: selected ? 1 : 0.85,
        }));
      target.addChild(marker);
      markers.push(marker);
    }
  }
  return markers;
}

export function drawWorldDensityAggregates(
  layer: Container,
  world: ActorWorldProjection,
  selectedTileId: number | null,
  wrappedColumns: readonly WrappedColumn[] = [],
): Container[] {
  const markers: Container[] = [];
  const origin = worldPresentationOrigin(world);
  for (const tile of world.tiles) {
    const displayTile = presentationTilePosition(tile, origin);
    const target = wrappedColumns[displayTile.x]?.node ?? layer;
    const left = wrappedColumns.length === 0 ? displayTile.x * TILE_SIZE : 0;
    const top = displayTile.y * TILE_SIZE;
    const color = tile.detail.case === "live"
      ? 0x123f52
      : tile.detail.case === "reduced"
        ? 0x243b3d
        : 0x111c1f;
    target.addChild(new Graphics()
      .rect(left, top, TILE_SIZE, TILE_SIZE)
      .fill({ color, alpha: 0.94 })
      .stroke({
        color: tile.tileId === selectedTileId ? 0xf1cf87 : 0x58c4a3,
        width: tile.tileId === selectedTileId ? 4 : 1,
        alpha: tile.tileId === selectedTileId ? 1 : 0.38,
        pixelLine: true,
      }));
    if (tile.detail.case !== "live") continue;

    const vents = createVolcanicVentLayer(
      tile.tileId,
      tile.detail.value.baselineVolcanismQ,
      left,
      top,
    );
    if (vents !== null) target.addChild(vents);

    const organismCount = tile.detail.value.organisms.length;
    const remnantCount = tile.detail.value.remnants.length;
    if (organismCount === 0 && remnantCount === 0) continue;
    const marker = new Container({ x: left + TILE_SIZE / 2, y: top + TILE_SIZE / 2 });
    const radius = Math.min(24, 7 + Math.sqrt(organismCount + remnantCount) * 0.55);
    const density = new Graphics()
      .circle(0, 0, radius)
      .fill({ color: 0x174d51, alpha: 0.9 })
      .stroke({ color: 0x8ff0c8, width: 2, alpha: 0.94 })
      .circle(-4, 1, 2.2)
      .circle(3, -3, 2.2)
      .circle(5, 5, 2.2)
      .fill({ color: 0xe3fff6, alpha: 0.88 });
    marker.addChild(density);
    target.addChild(marker);
    markers.push(marker);
  }
  return markers;
}

export interface VolcanicVentPlacement extends Point {
  readonly scale: number;
  readonly lean: number;
}

export function volcanicVentCount(baselineVolcanismQ: number): number {
  if (baselineVolcanismQ <= 0) return 0;
  const bounded = Math.min(VOLCANISM_SCALE, baselineVolcanismQ);
  return Math.min(MAX_VOLCANIC_VENTS, Math.max(1, Math.ceil(bounded / VOLCANIC_VENT_STEP_Q)));
}

export function volcanicVentPlacements(
  tileId: number,
  baselineVolcanismQ: number,
): readonly VolcanicVentPlacement[] {
  const count = volcanicVentCount(baselineVolcanismQ);
  return Array.from({ length: count }, (_, index) => {
    const xHash = mixUint32((tileId >>> 0) ^ Math.imul(index + 1, 0x9e37_79b1));
    const yHash = mixUint32(xHash ^ 0x85eb_ca6b);
    return {
      x: 0.13 + (xHash % 10_000) / 10_000 * 0.74,
      y: 0.16 + (yHash % 10_000) / 10_000 * 0.68,
      scale: 0.72 + ((xHash >>> 16) % 6) * 0.07,
      lean: ((yHash >>> 16) % 7 - 3) * 0.22,
    };
  });
}

export function createVolcanicVentLayer(
  tileId: number,
  baselineVolcanismQ: number,
  left = 0,
  top = 0,
): Graphics | null {
  const placements = volcanicVentPlacements(tileId, baselineVolcanismQ);
  if (placements.length === 0) return null;
  const strength = Math.min(1, baselineVolcanismQ / VOLCANISM_SCALE);
  const vents = new Graphics();
  vents.label = "volcanic-vents";
  for (const placement of placements) {
    const x = left + placement.x * TILE_SIZE;
    const baseY = top + placement.y * TILE_SIZE;
    const size = 4.4 * placement.scale;
    const lipX = x + placement.lean;
    const lipY = baseY - size * 1.35;
    vents
      .moveTo(x - size * 1.55, baseY + size * 0.42)
      .lineTo(x - size * 0.58, baseY)
      .lineTo(lipX - size * 0.42, lipY)
      .moveTo(lipX + size * 0.42, lipY)
      .lineTo(x + size * 0.58, baseY)
      .lineTo(x + size * 1.55, baseY + size * 0.42)
      .ellipse(lipX, lipY, size * 0.44, size * 0.18)
      .moveTo(lipX - size * 0.18, lipY - size * 0.32)
      .bezierCurveTo(
        lipX - size * 0.72,
        lipY - size * 0.85,
        lipX + size * 0.25,
        lipY - size * 1.15,
        lipX - size * 0.16,
        lipY - size * 1.62,
      )
      .moveTo(lipX + size * 0.2, lipY - size * 0.26)
      .bezierCurveTo(
        lipX + size * 0.82,
        lipY - size * 0.72,
        lipX - size * 0.14,
        lipY - size * 1.12,
        lipX + size * 0.34,
        lipY - size * 1.48,
      );
  }
  vents.stroke({
    color: 0xd9895b,
    width: 1,
    alpha: 0.24 + strength * 0.18,
    pixelLine: true,
  });
  return vents;
}

function mixUint32(value: number): number {
  let mixed = value >>> 0;
  mixed ^= mixed >>> 16;
  mixed = Math.imul(mixed, 0x7feb_352d);
  mixed ^= mixed >>> 15;
  mixed = Math.imul(mixed, 0x846c_a68b);
  mixed ^= mixed >>> 16;
  return mixed >>> 0;
}

function addActivityPulses(
  application: Application,
  layer: Container,
  world: ActorWorldProjection,
  enabled: ReadonlySet<OrganismJourneyEventFamily>,
  markers: Container[],
  cameraLayer: Container,
  wrappedColumns: readonly WrappedColumn[] = [],
  startedAtByEvent: ReadonlyMap<bigint, number> = new Map(),
): () => void {
  const events = selectActivityPulseEvents(world.activityPulseEvents, enabled);
  if (events.length === 0) return () => undefined;
  const origin = worldPresentationOrigin(world);
  const now = performance.now();
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const pulses = events.flatMap((event) => {
    const startedAt = startedAtByEvent.get(event.eventId) ?? now;
    return now - startedAt >= ACTIVITY_PULSE_LIFETIME_MS
      ? []
      : [{
          ...createPulse(event, world, origin, wrappedColumns.length > 0),
          startedAt,
        }];
  });
  if (pulses.length === 0) return () => undefined;
  for (const pulse of pulses) {
    const column = wrappedColumns[pulse.tileX];
    (column?.node ?? layer).addChild(pulse.node);
    markers.push(pulse.node);
  }

  const update = () => {
    const currentTime = performance.now();
    const zoom = Math.max(0.000_001, cameraLayer.scale.x);
    for (const pulse of pulses) {
      const fadeProgress = activityPulseFadeProgress(pulse.startedAt, currentTime);
      pulse.node.alpha = 1 - fadeProgress;
      pulse.node.y = pulse.baseY - (reducedMotion ? 0 : 16 * fadeProgress / zoom);
    }
  };
  update();
  application.ticker.add(update);
  return () => application.ticker.remove(update);
}

const ACTIVITY_PULSE_HOLD_MS = 400;
const ACTIVITY_PULSE_FADE_MS = 2_600;
const ACTIVITY_PULSE_LIFETIME_MS = ACTIVITY_PULSE_HOLD_MS + ACTIVITY_PULSE_FADE_MS;

export function activityPulseFadeProgress(startedAt: number, now: number): number {
  return Math.max(0, Math.min(1, (now - startedAt - ACTIVITY_PULSE_HOLD_MS) /
    ACTIVITY_PULSE_FADE_MS));
}

export function reconcileActivityPulseStarts(
  starts: Map<bigint, number>,
  events: readonly Pick<OrganismJourneyEvent, "eventId">[],
  now: number,
): void {
  const currentIds = new Set(events.map((event) => event.eventId));
  for (const eventId of starts.keys()) {
    if (!currentIds.has(eventId)) starts.delete(eventId);
  }
  for (const event of events) {
    if (!starts.has(event.eventId)) starts.set(event.eventId, now);
  }
}

function createPulse(
  event: OrganismJourneyEvent,
  world: ActorWorldProjection,
  origin: Point,
  columnRelative = false,
) {
  const tile = world.tiles.find((candidate) => candidate.tileId === event.tileId);
  const displayTile = tile === undefined
    ? { x: 0, y: 0 }
    : presentationTilePosition(tile, origin);
  const tileX = displayTile.x;
  const x = ((columnRelative ? 0 : tileX) + event.positionXQ / UINT32_SCALE) * TILE_SIZE;
  const y = (displayTile.y + event.positionYQ / UINT32_SCALE) * TILE_SIZE;
  const visual = eventVisual(event.family);
  const node = new Container({ x, y });
  const backing = new Graphics()
    .circle(0, 0, 10)
    .fill({ color: 0x071113, alpha: 0.76 })
    .stroke({ color: visual.color, width: 1.5, alpha: 0.92 });
  const glyph = new Text({
    text: visual.glyph,
    style: { fill: visual.color, fontFamily: "system-ui, sans-serif", fontSize: 14, fontWeight: "700" },
  });
  glyph.anchor.set(0.5);
  node.addChild(backing, glyph);
  return { node, baseY: y, tileX };
}

function applyCamera(render: RenderState, camera: CameraState) {
  applyWrappedColumnPositions(render.wrappedColumns, camera.centerX, render.extent.width);
  render.layer.scale.set(camera.zoom);
  render.layer.position.set(
    render.viewport.width / 2 - camera.centerX * camera.zoom,
    render.viewport.height / 2 - camera.centerY * camera.zoom,
  );
  applyMarkerScale(render.markers, camera.zoom);
}

export function createWrappedColumns(layer: Container, columnCount: number): readonly WrappedColumn[] {
  return Array.from({ length: columnCount }, (_, column): WrappedColumn => {
    const baseX = column * TILE_SIZE;
    const node = new Container({ x: baseX });
    layer.addChild(node);
    return { node, baseX };
  });
}

export function applyWrappedColumnPositions(
  columns: readonly WrappedColumn[],
  cameraCenterX: number,
  worldWidth: number,
): void {
  if (worldWidth <= 0) return;
  for (const column of columns) {
    const center = column.baseX + TILE_SIZE / 2;
    const turns = Math.round((cameraCenterX - center) / worldWidth);
    column.node.position.x = column.baseX + turns * worldWidth;
  }
}

export function applyMarkerScale(markers: readonly Container[], zoom: number): void {
  const scale = 1 / Math.max(0.000_001, zoom);
  for (const marker of markers) marker.scale.set(scale);
}

export function pinchGestureIncludesPan(
  startMidpoint: Point,
  startDistance: number,
  currentMidpoint: Point,
  currentDistance: number,
  minimumTravel = 4,
): boolean {
  const translation = distance(startMidpoint, currentMidpoint);
  const scaleTravel = Math.abs(currentDistance - startDistance);
  return translation > minimumTravel && translation > scaleTravel * 0.75;
}

export function selectAtScreenPoint(
  screenPoint: Point,
  world: ActorWorldProjection,
  camera: CameraState | null,
  viewport: ViewportSize,
  onSelectTile?: (tileId: number) => void,
  onSelectOrganism?: (organismId: bigint, tileId: number) => void,
  onSelectRemnant?: (remnantId: bigint, tileId: number) => void,
): TileProjection | null {
  if (camera === null) return null;
  const worldPoint = screenToWorld(screenPoint, camera, viewport);
  const coordinates = wrappedTileCoordinatesAt(worldPoint, world.width, world.height);
  const origin = worldPresentationOrigin(world);
  const tile = coordinates === null
    ? undefined
    : world.tiles.find((candidate) =>
        candidate.x === coordinates.x + origin.x &&
        candidate.y === coordinates.y + origin.y);
  if (tile === undefined) return null;

  if (tile.detail.case === "live") {
    const organism = nearestOrganism(
      screenPoint,
      tile as LiveTileProjection,
      camera,
      viewport,
      world.width * TILE_SIZE,
      origin,
    );
    if (organism !== null) {
      onSelectOrganism?.(organism.organismId, tile.tileId);
      return tile;
    }
    const remnant = nearestRemnant(
      screenPoint,
      tile as LiveTileProjection,
      camera,
      viewport,
      world.width * TILE_SIZE,
      origin,
    );
    if (remnant !== null) {
      onSelectRemnant?.(remnant.remnantId, tile.tileId);
      return tile;
    }
  }
  onSelectTile?.(tile.tileId);
  return tile;
}

function nearestRemnant(
  screenPoint: Point,
  tile: LiveTileProjection,
  camera: CameraState,
  viewport: ViewportSize,
  worldWidth: number,
  origin: Point,
): RemnantProjection | null {
  let nearest: RemnantProjection | null = null;
  let nearestDistance = 11;
  for (const remnant of tile.detail.value.remnants) {
    const projected = worldToScreenWrappedX(
      remnantWorldPosition(tile, remnant, origin),
      camera,
      viewport,
      worldWidth,
    );
    const separation = distance(screenPoint, projected);
    if (separation < nearestDistance) {
      nearest = remnant;
      nearestDistance = separation;
    }
  }
  return nearest;
}

function nearestOrganism(
  screenPoint: Point,
  tile: LiveTileProjection,
  camera: CameraState,
  viewport: ViewportSize,
  worldWidth: number,
  origin: Point,
): OrganismProjection | null {
  let nearest: OrganismProjection | null = null;
  let nearestDistance = 11;
  for (const organism of tile.detail.value.organisms) {
    const projected = worldToScreenWrappedX(
      organismWorldPosition(tile, organism, origin),
      camera,
      viewport,
      worldWidth,
    );
    const separation = distance(screenPoint, projected);
    if (separation < nearestDistance) {
      nearest = organism;
      nearestDistance = separation;
    }
  }
  return nearest;
}

function organismWorldPosition(
  tile: Pick<TileProjection, "x" | "y">,
  organism: OrganismProjection,
  origin: Point,
): Point {
  const displayTile = presentationTilePosition(tile, origin);
  return {
    x: (displayTile.x + organism.positionXQ / UINT32_SCALE) * TILE_SIZE,
    y: (displayTile.y + organism.positionYQ / UINT32_SCALE) * TILE_SIZE,
  };
}

function remnantWorldPosition(
  tile: Pick<TileProjection, "x" | "y">,
  remnant: RemnantProjection,
  origin: Point,
): Point {
  const displayTile = presentationTilePosition(tile, origin);
  return {
    x: (displayTile.x + remnant.positionXQ / UINT32_SCALE) * TILE_SIZE,
    y: (displayTile.y + remnant.positionYQ / UINT32_SCALE) * TILE_SIZE,
  };
}

function TileSelectionSummary({
  tile,
  selectedOrganismId,
  selectedRemnantId,
  selectedSpeciesId,
}: {
  readonly tile: TileProjection | null;
  readonly selectedOrganismId: bigint | null;
  readonly selectedRemnantId: bigint | null;
  readonly selectedSpeciesId: bigint | null;
}) {
  if (tile === null) {
    return (
      <p className="tile-selection-empty" role="status" aria-live="polite">
        Select a tile to inspect its authorized projection.
      </p>
    );
  }
  if (tile.detail.case === "unknown" || tile.detail.case === undefined) {
    return (
      <div className="tile-selection-summary" role="status" aria-live="polite">
        <strong>Tile {tile.tileId} · Unknown</strong>
        <span>Grid {tile.x}, {tile.y} · Current conditions are outside this actor’s knowledge.</span>
      </div>
    );
  }
  if (tile.detail.case === "reduced") {
    return (
      <div className="tile-selection-summary" role="status" aria-live="polite">
        <strong>Tile {tile.tileId} · Last observed</strong>
        <span>
          Grid {tile.x}, {tile.y} · {tile.detail.value.elevationMeters.toLocaleString("en-US")} m ·
          {" "}observed tick {tile.detail.value.observedAtTick.toLocaleString("en-US")}
        </span>
      </div>
    );
  }
  const selectedOrganism = tile.detail.value.organisms.find((organism) =>
    organism.organismId === selectedOrganismId);
  const selectedRemnant = tile.detail.value.remnants.find((remnant) =>
    remnant.remnantId === selectedRemnantId);
  return (
    <div className="tile-selection-summary" role="status" aria-live="polite">
      <strong>Tile {tile.tileId} · Live</strong>
      <span>
        Grid {tile.x}, {tile.y} · {tile.detail.value.organisms.length.toLocaleString("en-US")} organisms ·
        {" "}{tile.detail.value.remnants.length.toLocaleString("en-US")} remnants
      </span>
      {selectedOrganism === undefined ? null : (
        <span>
          Organism #{selectedOrganism.organismId.toString()} · within-tile position {formatCoordinate(selectedOrganism.positionXQ)}, {formatCoordinate(selectedOrganism.positionYQ)}
        </span>
      )}
      {selectedOrganism === undefined && selectedSpeciesId !== null ? (
        <span>Other species #{selectedSpeciesId.toString()} selected · observed members only</span>
      ) : null}
      {selectedOrganism === undefined && selectedRemnant !== undefined ? (
        <span>
          Remnant #{selectedRemnant.remnantId.toString()} · formed at tick {selectedRemnant.createdTick.toLocaleString("en-US")}
        </span>
      ) : null}
    </div>
  );
}

export function handleCameraKey(
  event: ReactKeyboardEvent<HTMLDivElement>,
  render: RenderState | null,
  camera: CameraState | null,
  commit: (camera: CameraState) => void,
  fit: () => void,
  focus: () => void,
  selectCenter: () => void,
  releaseFollow: () => void,
) {
  if (render === null || camera === null) return;
  const panStep = event.shiftKey ? 120 : 48;
  const delta = event.key === "ArrowLeft" ? { x: panStep, y: 0 }
    : event.key === "ArrowRight" ? { x: -panStep, y: 0 }
      : event.key === "ArrowUp" ? { x: 0, y: panStep }
        : event.key === "ArrowDown" ? { x: 0, y: -panStep }
          : null;
  if (delta !== null) {
    event.preventDefault();
    releaseFollow();
    commit(panCamera(camera, delta, render.extent, render.viewport, render.fittedZoom));
  } else if (event.key === "+" || event.key === "=") {
    event.preventDefault();
    commit(zoomCameraAt(camera, 1.45, center(render.viewport), render.extent, render.viewport, render.fittedZoom));
  } else if (event.key === "-" || event.key === "_") {
    event.preventDefault();
    commit(zoomCameraAt(camera, 1 / 1.45, center(render.viewport), render.extent, render.viewport, render.fittedZoom));
  } else if (event.key === "0") {
    event.preventDefault();
    releaseFollow();
    fit();
  } else if (event.key === "Enter") {
    event.preventDefault();
    releaseFollow();
    focus();
  } else if (event.key === " " || event.key === "Spacebar") {
    event.preventDefault();
    selectCenter();
  }
}

function findLiveOrganism(world: ActorWorldProjection, organismId: bigint) {
  const origin = worldPresentationOrigin(world);
  for (const tile of world.tiles) {
    if (tile.detail.case !== "live") continue;
    const organism = tile.detail.value.organisms.find((value) =>
      value.organismId === organismId);
    if (organism !== undefined) {
      return { tile, organism, point: organismWorldPosition(tile, organism, origin) };
    }
  }
  return null;
}

export function organismMarkerColor(speciesId: bigint, controlledSpeciesId: bigint): number {
  return speciesId === controlledSpeciesId ? 0x8ff0c8 : 0xef7a72;
}

export function defaultWorldCamera(
  world: ActorWorldProjection,
  viewport: ViewportSize,
): CameraState {
  const extent = { width: world.width * TILE_SIZE, height: world.height * TILE_SIZE };
  const fitted = fitCamera(extent, viewport);
  const startingTileId = world.gameplay?.roots.find((root) => root.playerSelected)?.startingTileId;
  const startingTile = startingTileId === undefined
    ? undefined
    : world.tiles.find((tile) => tile.tileId === startingTileId);
  return startingTile === undefined
    ? fitted
    : focusTileCamera(
        fitted,
        presentationTilePosition(startingTile, worldPresentationOrigin(world)),
        extent,
        viewport,
        fitted.zoom,
      );
}

export function worldPresentationOrigin(world: ActorWorldProjection): Point {
  if (world.tiles.length === 0) return { x: 0, y: 0 };
  return {
    x: Math.min(...world.tiles.map((tile) => tile.x)),
    y: Math.min(...world.tiles.map((tile) => tile.y)),
  };
}

export function presentationTilePosition(
  tile: Pick<TileProjection, "x" | "y">,
  origin: Point,
): Point {
  return { x: tile.x - origin.x, y: tile.y - origin.y };
}

function center(viewport: ViewportSize): Point {
  return { x: viewport.width / 2, y: viewport.height / 2 };
}

function formatCoordinate(value: number): string {
  return `${((value / UINT32_SCALE) * 100).toFixed(1)}%`;
}

function midpoint(left: Point, right: Point): Point {
  return { x: (left.x + right.x) / 2, y: (left.y + right.y) / 2 };
}

function distance(left: Point, right: Point): number {
  return Math.hypot(left.x - right.x, left.y - right.y);
}

function eventVisual(family: OrganismJourneyEventFamily) {
  switch (family) {
    case OrganismJourneyEventFamily.BIRTH:
      return { glyph: "✦", color: 0x78e6ac };
    case OrganismJourneyEventFamily.REPRODUCTION:
      return { glyph: "+", color: 0x58d99b };
    case OrganismJourneyEventFamily.RESOURCE_ABSORPTION:
      return { glyph: "↟", color: 0x61c9b5 };
    case OrganismJourneyEventFamily.FEEDING:
      return { glyph: "◆", color: 0xa8d887 };
    case OrganismJourneyEventFamily.MIGRATION:
      return { glyph: "→", color: 0xd8b870 };
    case OrganismJourneyEventFamily.DEATH:
      return { glyph: "×", color: 0xed756d };
    case OrganismJourneyEventFamily.STRESS:
      return { glyph: "!", color: 0xe49a62 };
    case OrganismJourneyEventFamily.BEHAVIOR_TRANSITION:
      return { glyph: "~", color: 0xc4b987 };
    default:
      return { glyph: "•", color: 0xc4b987 };
  }
}
