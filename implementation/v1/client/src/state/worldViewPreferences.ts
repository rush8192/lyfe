import type { ActorWorldProjection } from "../generated/lyfe/v1/projection_pb";

export interface WorldViewPreferenceStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export interface StoredCameraPreference {
  readonly centerXFraction: number;
  readonly centerYFraction: number;
  readonly relativeZoom: number;
}

export interface WorldViewPreferences {
  readonly version: 2;
  readonly camera?: StoredCameraPreference;
  readonly selectedTileId?: number;
}

export type WorldViewPreferencePatch = Omit<Partial<WorldViewPreferences>, "version">;

export interface ResolvedWorldViewSelection {
  readonly selectedTileId: number | null;
}

const STORAGE_PREFIX = "lyfe.world-view.v2.";
const MAX_RELATIVE_ZOOM = 128;

export function readWorldViewPreferences(
  storage: WorldViewPreferenceStorage,
  worldId: bigint,
): WorldViewPreferences | null {
  try {
    const raw = storage.getItem(storageKey(worldId));
    if (raw === null) return null;
    const value = JSON.parse(raw) as unknown;
    if (!isRecord(value) || value.version !== 2) return null;

    const camera = value.camera === undefined ? undefined : parseCamera(value.camera);
    if (value.camera !== undefined && camera === undefined) return null;
    const selectedTileId = value.selectedTileId === undefined
      ? undefined
      : parseTileId(value.selectedTileId);
    if (value.selectedTileId !== undefined && selectedTileId === undefined) return null;
    return {
      version: 2,
      ...(camera === undefined ? {} : { camera }),
      ...(selectedTileId === undefined ? {} : { selectedTileId }),
    };
  } catch {
    return null;
  }
}

export function updateWorldViewPreferences(
  storage: WorldViewPreferenceStorage,
  worldId: bigint,
  patch: WorldViewPreferencePatch,
): void {
  try {
    const prior = readWorldViewPreferences(storage, worldId) ?? { version: 2 as const };
    const next: WorldViewPreferences = { ...prior, ...patch, version: 2 };
    storage.setItem(storageKey(worldId), JSON.stringify(next));
  } catch {
    // Browser-local preferences are optional and must never block play.
  }
}

export function resolveWorldViewSelection(
  world: ActorWorldProjection,
  preferences: WorldViewPreferences | null,
): ResolvedWorldViewSelection {
  const selectedTileId = preferences?.selectedTileId !== undefined &&
      world.tiles.some((tile) => tile.tileId === preferences.selectedTileId)
    ? preferences.selectedTileId
    : null;
  return { selectedTileId };
}

export function browserWorldViewPreferenceStorage(): WorldViewPreferenceStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function storageKey(worldId: bigint): string {
  return `${STORAGE_PREFIX}${worldId.toString()}`;
}

function parseCamera(value: unknown): StoredCameraPreference | undefined {
  if (!isRecord(value) ||
      !isFiniteNumber(value.centerXFraction) ||
      !isFiniteNumber(value.centerYFraction) ||
      !isFiniteNumber(value.relativeZoom) ||
      value.centerXFraction < 0 || value.centerXFraction >= 1 ||
      value.centerYFraction < 0 || value.centerYFraction > 1 ||
      value.relativeZoom < 1 || value.relativeZoom > MAX_RELATIVE_ZOOM) {
    return undefined;
  }
  return {
    centerXFraction: value.centerXFraction,
    centerYFraction: value.centerYFraction,
    relativeZoom: value.relativeZoom,
  };
}

function parseTileId(value: unknown): number | undefined {
  return typeof value === "number" && Number.isSafeInteger(value) && value >= 0
    ? value
    : undefined;
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
