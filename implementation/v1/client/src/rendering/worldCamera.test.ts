import { describe, expect, it } from "vitest";
import {
  TILE_SIZE,
  cameraLevel,
  fitCamera,
  focusTileCamera,
  panCamera,
  restoreCamera,
  screenToWorld,
  tileCoordinatesAt,
  worldToScreen,
  worldToScreenWrappedX,
  wrappedTileCoordinatesAt,
  zoomCameraAt,
} from "./worldCamera";

const extent = { width: 3_200, height: 1_800 };
const viewport = { width: 800, height: 500 };

describe("world camera", () => {
  it("fits and centers the complete world", () => {
    const camera = fitCamera(extent, viewport);

    expect(camera.centerX).toBe(1_600);
    expect(camera.centerY).toBe(900);
    expect(camera.zoom).toBeCloseTo(752 / 3_200);
    expect(cameraLevel(camera, camera.zoom)).toBe("World");
  });

  it("preserves the world point beneath a zoom anchor", () => {
    const fitted = fitCamera(extent, viewport);
    const camera = { ...fitted, zoom: fitted.zoom * 2 };
    const anchor = { x: 620, y: 120 };
    const before = screenToWorld(anchor, camera, viewport);
    const zoomed = zoomCameraAt(camera, 1.75, anchor, extent, viewport, fitted.zoom);
    const after = screenToWorld(anchor, zoomed, viewport);

    expect(after.x).toBeCloseTo(before.x);
    expect(after.y).toBeCloseTo(before.y);
  });

  it("uses drag distance in screen pixels, wraps x, and bounds y", () => {
    const fitted = fitCamera(extent, viewport);
    const camera = { ...fitted, zoom: 1, centerX: 1_600, centerY: 900 };

    const moved = panCamera(camera, { x: 100, y: -50 }, extent, viewport, fitted.zoom);
    expect(moved.centerX).toBe(1_500);
    expect(moved.centerY).toBe(950);

    const bounded = panCamera(moved, { x: 100_000, y: 100_000 }, extent, viewport, fitted.zoom);
    expect(bounded.centerX).toBeGreaterThanOrEqual(0);
    expect(bounded.centerX).toBeLessThan(extent.width);
    expect(bounded.centerY).toBeGreaterThanOrEqual(viewport.height / 2 - 24);

    const acrossSeam = panCamera(
      { centerX: 20, centerY: 900, zoom: 1 },
      { x: 100, y: 0 },
      extent,
      viewport,
      fitted.zoom,
    );
    expect(acrossSeam.centerX).toBe(3_120);
  });

  it("round-trips screen and world coordinates", () => {
    const camera = { centerX: 930, centerY: 720, zoom: 2.25 };
    const worldPoint = { x: 1_050, y: 610 };
    const screenPoint = worldToScreen(worldPoint, camera, viewport);

    expect(screenToWorld(screenPoint, camera, viewport)).toEqual(worldPoint);
  });

  it("maps world coordinates to bounded tiles", () => {
    expect(tileCoordinatesAt({ x: TILE_SIZE * 2 + 14, y: 88 }, 4, 3)).toEqual({ x: 2, y: 0 });
    expect(tileCoordinatesAt({ x: -1, y: 88 }, 4, 3)).toBeNull();
    expect(tileCoordinatesAt({ x: 400, y: 88 }, 4, 3)).toBeNull();
  });

  it("maps and projects through the horizontal seam", () => {
    expect(wrappedTileCoordinatesAt({ x: -1, y: 88 }, 32, 18)).toEqual({ x: 31, y: 0 });
    expect(wrappedTileCoordinatesAt({ x: 3_214, y: 188 }, 32, 18)).toEqual({ x: 0, y: 1 });
    expect(wrappedTileCoordinatesAt({ x: 14, y: -1 }, 32, 18)).toBeNull();

    const projected = worldToScreenWrappedX(
      { x: 3_190, y: 900 },
      { centerX: 10, centerY: 900, zoom: 2 },
      viewport,
      3_200,
    );
    expect(projected).toEqual({ x: 360, y: 250 });
  });

  it("restores normalized camera preferences across viewport sizes", () => {
    const restored = restoreCamera({
      centerXFraction: 0.9,
      centerYFraction: 0.25,
      relativeZoom: 3,
    }, extent, viewport);

    expect(restored.centerX).toBe(2_880);
    expect(restored.centerY).toBe(450);
    expect(restored.zoom).toBeCloseTo(fitCamera(extent, viewport).zoom * 3);
  });

  it("focuses a tile without lowering an existing closer zoom", () => {
    const fitted = fitCamera(extent, viewport);
    const focused = focusTileCamera(fitted, { x: 7, y: 3 }, extent, viewport, fitted.zoom);

    expect(focused.centerX).toBe(750);
    expect(focused.centerY).toBe(350);
    expect(focused.zoom).toBeGreaterThan(fitted.zoom * 5);
    expect(cameraLevel(focused, fitted.zoom)).toBe("Tile");
  });
});
