import { create } from "@bufbuild/protobuf";
import { Container } from "pixi.js";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { WorldKnowledgeLegend } from "../components/WorldKnowledgeLegend";
import {
  ActorWorldProjectionSchema,
  OrganismJourneyEventFamily,
} from "../generated/lyfe/v1/projection_pb";
import {
  WorldCanvas,
  activityPulseFadeProgress,
  applyMarkerScale,
  applyWrappedColumnPositions,
  createVolcanicVentLayer,
  createWrappedColumns,
  defaultWorldCamera,
  drawWorldDensityAggregates,
  drawWorldExact,
  handleCameraKey,
  organismMarkerColor,
  pinchGestureIncludesPan,
  presentationTilePosition,
  reconcileActivityPulseStarts,
  selectAtScreenPoint,
  swapSceneLayer,
  volcanicVentCount,
  volcanicVentPlacements,
  worldPresentationOrigin,
} from "./WorldCanvas";

describe("world canvas presentation", () => {
  it("exposes camera controls and exact visible within-tile selection", () => {
    const world = create(ActorWorldProjectionSchema, {
      worldId: 4n,
      width: 2,
      height: 1,
      tiles: [{
        tileId: 7,
        x: 0,
        y: 0,
        detail: {
          case: "live",
          value: {
            organisms: [{
              organismId: 19n,
              positionXQ: 0x7fff_ffff,
              positionYQ: 0xffff_ffff,
            }],
            remnants: [],
          },
        },
      }],
    });
    const html = renderToStaticMarkup(
      <WorldCanvas
        world={world}
        enabledEventFamilies={new Set([OrganismJourneyEventFamily.BIRTH])}
        selectedTileId={7}
        selectedOrganismId={19n}
      />,
    );

    expect(html).toContain("Camera controls");
    expect(html).toContain("Fit world");
    expect(html).toContain("Focus tile");
    expect(html).toContain("Follow organism");
    expect(html).toContain("aria-pressed=\"false\"");
    expect(html).toContain("aria-describedby=\"world-map-keyboard-help\"");
    expect(html).toContain("Space selects the tile beneath the");
    expect(html).toContain("aria-keyshortcuts=");
    expect(html).toContain("Tile 7 · Live");
    expect(html).toContain("Organism #19 · within-tile position 50.0%, 100.0%");
  });

  it("provides the knowledge-state key for the adjacent simulation console", () => {
    const html = renderToStaticMarkup(<WorldKnowledgeLegend />);

    expect(html).toContain("Map key");
    expect(html).toContain("Live");
    expect(html).toContain("Last known");
    expect(html).toContain("Unknown");
    expect(html).toContain("Our species");
    expect(html).toContain("Other species");
    expect(html).toContain("Volcanic vents");
    expect(html).toContain("more marks = stronger baseline activity");
  });

  it("draws a deterministic proportional volcanic layer behind organisms", () => {
    expect(volcanicVentCount(0)).toBe(0);
    expect(volcanicVentCount(1)).toBe(1);
    expect(volcanicVentCount(125_000)).toBe(2);
    expect(volcanicVentCount(250_000)).toBe(4);
    expect(volcanicVentCount(800_000)).toBe(13);
    expect(volcanicVentCount(1_000_000)).toBe(16);
    expect(volcanicVentPlacements(7, 800_000)).toEqual(
      volcanicVentPlacements(7, 800_000),
    );
    expect(volcanicVentPlacements(7, 800_000)).not.toEqual(
      volcanicVentPlacements(8, 800_000),
    );

    const world = create(ActorWorldProjectionSchema, {
      width: 1,
      height: 1,
      tiles: [{
        tileId: 7,
        detail: {
          case: "live",
          value: {
            baselineVolcanismQ: 800_000,
            organisms: [{ organismId: 1n, speciesId: 1n }],
          },
        },
      }],
    });
    const layer = new Container();
    const markers = drawWorldExact(layer, world, null, null);
    const vents = layer.children.find((child) => child.label === "volcanic-vents");

    expect(createVolcanicVentLayer(1, 0)).toBeNull();
    expect(vents).toBeTruthy();
    expect(layer.children.indexOf(vents!)).toBeLessThan(layer.children.indexOf(markers[0]));
    layer.destroy({ children: true });
  });

  it("installs the next scene before retiring the previous scene", () => {
    const stage = new Container();
    const previous = new Container();
    const next = new Container();
    stage.addChild(previous);
    const operations: string[] = [];
    const add = vi.spyOn(stage, "addChild").mockImplementation((...children) => {
      operations.push("add");
      return Container.prototype.addChild.apply(stage, children);
    });
    const remove = vi.spyOn(stage, "removeChild").mockImplementation((...children) => {
      operations.push("remove");
      return Container.prototype.removeChild.apply(stage, children);
    });

    swapSceneLayer(stage, previous, next);

    expect(operations).toEqual(["add", "remove"]);
    expect(stage.children).toEqual([next]);
    expect(previous.destroyed).toBe(true);
    add.mockRestore();
    remove.mockRestore();
    stage.destroy({ children: true });
  });

  it("does not restart an authoritative activity pulse during presentation redraws", () => {
    const starts = new Map<bigint, number>();
    reconcileActivityPulseStarts(starts, [{ eventId: 7n }, { eventId: 8n }], 1_000);
    reconcileActivityPulseStarts(starts, [{ eventId: 7n }, { eventId: 8n }], 2_000);

    expect(starts.get(7n)).toBe(1_000);
    expect(starts.get(8n)).toBe(1_000);
    expect(activityPulseFadeProgress(starts.get(7n)!, 2_000)).toBeGreaterThan(0);

    reconcileActivityPulseStarts(starts, [{ eventId: 8n }, { eventId: 9n }], 2_500);
    expect(starts.has(7n)).toBe(false);
    expect(starts.get(8n)).toBe(1_000);
    expect(starts.get(9n)).toBe(2_500);
    expect(activityPulseFadeProgress(1_000, 4_000)).toBe(1);
  });

  it("focuses a fresh camera on the player starting tile", () => {
    const world = create(ActorWorldProjectionSchema, {
      width: 4,
      height: 3,
      gameplay: { roots: [{ startingTileId: 9, playerSelected: true }] },
      tiles: [
        { tileId: 1, x: -2, y: -1, detail: { case: "unknown", value: {} } },
        { tileId: 9, x: 0, y: 0, detail: { case: "live", value: {} } },
        { tileId: 12, x: 1, y: 1, detail: { case: "unknown", value: {} } },
      ],
    });

    const camera = defaultWorldCamera(world, { width: 600, height: 500 });

    expect(camera.centerX).toBe(250);
    expect(camera.centerY).toBe(150);
    expect(camera.zoom).toBeGreaterThan(1);
  });

  it("selects either species only through a direct organism hit", () => {
    const world = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 1n,
      width: 1,
      height: 1,
      tiles: [{
        tileId: 7,
        x: 0,
        y: 0,
        detail: {
          case: "live",
          value: { organisms: [{ organismId: 22n, speciesId: 2n, positionXQ: 0x7fff_ffff, positionYQ: 0x7fff_ffff }] },
        },
      }],
    });
    let selectedOrganism: bigint | null = null;
    let selectedTile: number | null = null;

    selectAtScreenPoint(
      { x: 50, y: 50 },
      world,
      { centerX: 50, centerY: 50, zoom: 1 },
      { width: 100, height: 100 },
      (tileId) => { selectedTile = tileId; },
      (organismId) => { selectedOrganism = organismId; },
    );

    expect(selectedOrganism).toBe(22n);
    expect(selectedTile).toBeNull();
    expect(organismMarkerColor(1n, 1n)).toBe(0x8ff0c8);
    expect(organismMarkerColor(2n, 1n)).toBe(0xef7a72);
  });

  it("provides a keyboard command for selecting the tile beneath the map center", () => {
    let selected = false;
    let prevented = false;
    handleCameraKey(
      { key: " ", shiftKey: false, preventDefault: () => { prevented = true; } } as never,
      {
        layer: {} as never,
        wrappedColumns: [],
        markers: [],
        viewport: { width: 400, height: 430 },
        extent: { width: 200, height: 100 },
        fittedZoom: 1,
      },
      { centerX: 100, centerY: 50, zoom: 1 },
      () => undefined,
      () => undefined,
      () => undefined,
      () => { selected = true; },
      () => undefined,
    );

    expect(prevented).toBe(true);
    expect(selected).toBe(true);
  });

  it("releases follow for keyboard travel but preserves it for zoom", () => {
    let released = 0;
    const render = {
      layer: {} as never,
      wrappedColumns: [],
      markers: [],
      viewport: { width: 400, height: 430 },
      extent: { width: 3_200, height: 1_700 },
      fittedZoom: 0.1,
    };
    const camera = { centerX: 20, centerY: 500, zoom: 1 };
    const event = (key: string) => ({
      key,
      shiftKey: false,
      preventDefault: () => undefined,
    }) as never;

    handleCameraKey(
      event("+"), render, camera, () => undefined,
      () => undefined, () => undefined, () => undefined, () => { released++; },
    );
    expect(released).toBe(0);

    handleCameraKey(
      event("ArrowLeft"), render, camera, () => undefined,
      () => undefined, () => undefined, () => undefined, () => { released++; },
    );
    expect(released).toBe(1);
  });

  it("does not imply current conditions for an unknown selection", () => {
    const world = create(ActorWorldProjectionSchema, {
      worldId: 4n,
      width: 2,
      height: 1,
      tiles: [{
        tileId: 8,
        x: 1,
        y: 0,
        detail: { case: "unknown", value: {} },
      }],
    });
    const html = renderToStaticMarkup(
      <WorldCanvas world={world} enabledEventFamilies={new Set()} selectedTileId={8} />,
    );

    expect(html).toContain("Tile 8 · Unknown");
    expect(html).toContain("Current conditions are outside this actor’s knowledge.");
    expect(html).not.toContain("organisms ·");
  });

  it("keeps exact identity markers while bounding the measured density candidate per tile", () => {
    const world = create(ActorWorldProjectionSchema, {
      width: 2,
      height: 1,
      tiles: [
        {
          tileId: 0,
          x: 0,
          detail: {
            case: "live",
            value: { organisms: [{ organismId: 1n }, { organismId: 2n }] },
          },
        },
        {
          tileId: 1,
          x: 1,
          detail: { case: "live", value: {} },
        },
      ],
    });
    const exactLayer = new Container();
    const aggregateLayer = new Container();

    const exact = drawWorldExact(exactLayer, world, null, null);
    const aggregates = drawWorldDensityAggregates(aggregateLayer, world, null);
    applyMarkerScale(aggregates, 2);

    expect(exact).toHaveLength(2);
    expect(aggregates).toHaveLength(1);
    expect(aggregates[0].scale.x).toBe(0.5);
    exactLayer.destroy({ children: true });
    aggregateLayer.destroy({ children: true });
  });

  it("repositions one column scene continuously across the horizontal seam", () => {
    const layer = new Container();
    const columns = createWrappedColumns(layer, 32);

    applyWrappedColumnPositions(columns, 20, 3_200);
    expect(columns[0].node.x).toBe(0);
    expect(columns[31].node.x).toBe(-100);

    applyWrappedColumnPositions(columns, 3_180, 3_200);
    expect(columns[0].node.x).toBe(3_200);
    expect(columns[31].node.x).toBe(3_100);
    expect(layer.children).toHaveLength(32);
    layer.destroy({ children: true });
  });

  it("distinguishes a pinch-only zoom from deliberate two-pointer travel", () => {
    expect(pinchGestureIncludesPan(
      { x: 100, y: 100 }, 40,
      { x: 105, y: 100 }, 50,
    )).toBe(false);
    expect(pinchGestureIncludesPan(
      { x: 100, y: 100 }, 40,
      { x: 120, y: 100 }, 40,
    )).toBe(true);
  });

  it("normalizes centered signed grid coordinates into the camera extent", () => {
    const world = create(ActorWorldProjectionSchema, {
      width: 2,
      height: 2,
      tiles: [
        { tileId: 1, x: -1, y: -1, detail: { case: "unknown", value: {} } },
        {
          tileId: 2,
          x: 0,
          y: -1,
          detail: {
            case: "live",
            value: { organisms: [{ organismId: 7n, positionXQ: 0, positionYQ: 0 }] },
          },
        },
        { tileId: 3, x: -1, y: 0, detail: { case: "unknown", value: {} } },
        { tileId: 4, x: 0, y: 0, detail: { case: "unknown", value: {} } },
      ],
    });
    const origin = worldPresentationOrigin(world);
    const layer = new Container();
    const columns = createWrappedColumns(layer, world.width);
    const markers = drawWorldExact(layer, world, null, null, null, columns);

    expect(origin).toEqual({ x: -1, y: -1 });
    expect(presentationTilePosition(world.tiles[1], origin)).toEqual({ x: 1, y: 0 });
    expect(markers[0].position.x).toBe(0);
    expect(markers[0].position.y).toBe(0);
    expect(columns[1].node.children).toContain(markers[0]);
    layer.destroy({ children: true });
  });
});
