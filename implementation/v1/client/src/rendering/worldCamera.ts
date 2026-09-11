export interface CameraState {
  readonly centerX: number;
  readonly centerY: number;
  readonly zoom: number;
}

export interface ViewportSize {
  readonly width: number;
  readonly height: number;
}

export interface WorldExtent {
  readonly width: number;
  readonly height: number;
}

export interface Point {
  readonly x: number;
  readonly y: number;
}

export const TILE_SIZE = 100;
export const CAMERA_PADDING = 24;

export function fitCamera(
  extent: WorldExtent,
  viewport: ViewportSize,
  padding = CAMERA_PADDING,
): CameraState {
  const usableWidth = Math.max(1, viewport.width - padding * 2);
  const usableHeight = Math.max(1, viewport.height - padding * 2);
  const zoom = Math.min(usableWidth / extent.width, usableHeight / extent.height);
  return {
    centerX: extent.width / 2,
    centerY: extent.height / 2,
    zoom,
  };
}

export function clampCamera(
  camera: CameraState,
  extent: WorldExtent,
  viewport: ViewportSize,
  minimumZoom = fitCamera(extent, viewport).zoom,
  maximumZoom = Math.max(6, minimumZoom),
  padding = CAMERA_PADDING,
  wrapX = true,
): CameraState {
  const zoom = clamp(camera.zoom, minimumZoom, maximumZoom);
  const halfWidth = Math.max(0, viewport.width / 2 - padding) / zoom;
  const halfHeight = Math.max(0, viewport.height / 2 - padding) / zoom;
  return {
    centerX: wrapX ? wrapAxis(camera.centerX, extent.width) : clampAxis(camera.centerX, extent.width, halfWidth),
    centerY: clampAxis(camera.centerY, extent.height, halfHeight),
    zoom,
  };
}

export function panCamera(
  camera: CameraState,
  deltaScreen: Point,
  extent: WorldExtent,
  viewport: ViewportSize,
  minimumZoom?: number,
  maximumZoom?: number,
): CameraState {
  return clampCamera(
    {
      ...camera,
      centerX: camera.centerX - deltaScreen.x / camera.zoom,
      centerY: camera.centerY - deltaScreen.y / camera.zoom,
    },
    extent,
    viewport,
    minimumZoom,
    maximumZoom,
  );
}

export function zoomCameraAt(
  camera: CameraState,
  factor: number,
  anchorScreen: Point,
  extent: WorldExtent,
  viewport: ViewportSize,
  minimumZoom?: number,
  maximumZoom?: number,
): CameraState {
  const anchoredWorld = screenToWorld(anchorScreen, camera, viewport);
  const requestedZoom = camera.zoom * factor;
  const zoom = clamp(
    requestedZoom,
    minimumZoom ?? fitCamera(extent, viewport).zoom,
    maximumZoom ?? Math.max(6, minimumZoom ?? fitCamera(extent, viewport).zoom),
  );
  return clampCamera(
    {
      centerX: anchoredWorld.x - (anchorScreen.x - viewport.width / 2) / zoom,
      centerY: anchoredWorld.y - (anchorScreen.y - viewport.height / 2) / zoom,
      zoom,
    },
    extent,
    viewport,
    minimumZoom,
    maximumZoom,
  );
}

export function focusTileCamera(
  camera: CameraState,
  tile: Point,
  extent: WorldExtent,
  viewport: ViewportSize,
  minimumZoom?: number,
  maximumZoom?: number,
): CameraState {
  const targetZoom = Math.min(viewport.width, viewport.height) / (TILE_SIZE * 1.65);
  return clampCamera(
    {
      centerX: (tile.x + 0.5) * TILE_SIZE,
      centerY: (tile.y + 0.5) * TILE_SIZE,
      zoom: Math.max(camera.zoom, targetZoom),
    },
    extent,
    viewport,
    minimumZoom,
    maximumZoom,
  );
}

export function screenToWorld(
  point: Point,
  camera: CameraState,
  viewport: ViewportSize,
): Point {
  return {
    x: camera.centerX + (point.x - viewport.width / 2) / camera.zoom,
    y: camera.centerY + (point.y - viewport.height / 2) / camera.zoom,
  };
}

export function worldToScreen(
  point: Point,
  camera: CameraState,
  viewport: ViewportSize,
): Point {
  return {
    x: viewport.width / 2 + (point.x - camera.centerX) * camera.zoom,
    y: viewport.height / 2 + (point.y - camera.centerY) * camera.zoom,
  };
}

export function tileCoordinatesAt(point: Point, columns: number, rows: number): Point | null {
  const x = Math.floor(point.x / TILE_SIZE);
  const y = Math.floor(point.y / TILE_SIZE);
  return x >= 0 && x < columns && y >= 0 && y < rows ? { x, y } : null;
}

export function wrappedTileCoordinatesAt(
  point: Point,
  columns: number,
  rows: number,
): Point | null {
  const y = Math.floor(point.y / TILE_SIZE);
  if (y < 0 || y >= rows || columns <= 0) return null;
  const x = wrapAxis(Math.floor(point.x / TILE_SIZE), columns);
  return { x, y };
}

export function worldToScreenWrappedX(
  point: Point,
  camera: CameraState,
  viewport: ViewportSize,
  worldWidth: number,
): Point {
  return {
    x: viewport.width / 2 + shortestWrappedDelta(point.x - camera.centerX, worldWidth) * camera.zoom,
    y: viewport.height / 2 + (point.y - camera.centerY) * camera.zoom,
  };
}

export function restoreCamera(
  preference: { readonly centerXFraction: number; readonly centerYFraction: number; readonly relativeZoom: number },
  extent: WorldExtent,
  viewport: ViewportSize,
): CameraState {
  const fitted = fitCamera(extent, viewport);
  return clampCamera({
    centerX: preference.centerXFraction * extent.width,
    centerY: preference.centerYFraction * extent.height,
    zoom: fitted.zoom * preference.relativeZoom,
  }, extent, viewport, fitted.zoom);
}

export function cameraLevel(camera: CameraState, fittedZoom: number): "World" | "Region" | "Tile" {
  const relativeZoom = camera.zoom / fittedZoom;
  return relativeZoom <= 1.25 ? "World" : relativeZoom < 5 ? "Region" : "Tile";
}

function clampAxis(value: number, extent: number, visibleHalf: number): number {
  if (visibleHalf * 2 >= extent) return extent / 2;
  return clamp(value, visibleHalf, extent - visibleHalf);
}

function wrapAxis(value: number, extent: number): number {
  if (extent <= 0) return 0;
  return ((value % extent) + extent) % extent;
}

function shortestWrappedDelta(delta: number, extent: number): number {
  if (extent <= 0) return delta;
  const wrapped = wrapAxis(delta + extent / 2, extent) - extent / 2;
  return wrapped === -extent / 2 && delta > 0 ? extent / 2 : wrapped;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
