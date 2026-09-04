import { create } from "@bufbuild/protobuf";
import {
  ActorWorldProjectionSchema,
  type ActorWorldProjection,
  type ProjectionBatch,
  type ProjectionSnapshot,
  type SpeciesProjection,
} from "../generated/lyfe/v1/projection_pb";

export interface ProjectionCache {
  readonly projectionStreamId: bigint;
  readonly streamRevision: bigint;
  readonly world: ActorWorldProjection;
}

export type ProjectionApplyResult =
  | { readonly status: "applied"; readonly cache: ProjectionCache }
  | { readonly status: "duplicate"; readonly cache: ProjectionCache }
  | { readonly status: "resync-required"; readonly cache: ProjectionCache; readonly reason: string };

export function createProjectionCache(snapshot: ProjectionSnapshot): ProjectionCache {
  if (
    snapshot.projectionStreamId === 0n ||
    snapshot.streamRevision === 0n ||
    snapshot.projection === undefined
  ) {
    throw new Error("Projection snapshot identity, revision, and world are required.");
  }

  validateWorld(snapshot.projection);
  return {
    projectionStreamId: snapshot.projectionStreamId,
    streamRevision: snapshot.streamRevision,
    world: snapshot.projection,
  };
}

export function applyProjectionBatch(
  cache: ProjectionCache,
  batch: ProjectionBatch,
): ProjectionApplyResult {
  if (
    batch.projectionStreamId !== cache.projectionStreamId ||
    batch.worldId !== cache.world.worldId ||
    batch.worldRulesHash !== cache.world.worldRulesHash
  ) {
    return resync(cache, "stream, world, or rules identity mismatch");
  }

  if (batch.targetStreamRevision <= cache.streamRevision) {
    return { status: "duplicate", cache };
  }

  if (
    batch.baseStreamRevision !== cache.streamRevision ||
    batch.targetStreamRevision !== batch.baseStreamRevision + 1n ||
    batch.fromExclusiveTick !== cache.world.completedTick ||
    batch.throughCompletedTick < batch.fromExclusiveTick ||
    batch.worldRevision < cache.world.worldRevision ||
    batch.simulatedHours < cache.world.simulatedHours
  ) {
    return resync(cache, "projection revision or boundary gap");
  }

  const tiles = indexBy(cache.world.tiles, (tile) => tile.tileId);
  const tileReplacements = unique(batch.tileReplacements, (tile) => tile.tileId);
  const removedTileIds = uniqueValues(batch.removedTileIds);
  if (
    tileReplacements === null ||
    removedTileIds === null ||
    intersects(tileReplacements.keys(), removedTileIds)
  ) {
    return resync(cache, "ambiguous tile operations");
  }

  const species = indexBy(cache.world.species, (item) => item.speciesId);
  const speciesReplacements = unique(batch.speciesReplacements, (item) => item.speciesId);
  const removedSpeciesIds = uniqueValues(batch.removedSpeciesIds);
  if (
    speciesReplacements === null ||
    removedSpeciesIds === null ||
    intersects(speciesReplacements.keys(), removedSpeciesIds)
  ) {
    return resync(cache, "ambiguous species operations");
  }

  for (const id of removedTileIds) tiles.delete(id);
  for (const [id, tile] of tileReplacements) tiles.set(id, tile);
  for (const id of removedSpeciesIds) species.delete(id);
  for (const [id, item] of speciesReplacements) species.set(id, item);
  if (!species.has(cache.world.controlledSpeciesId)) {
    return resync(cache, "controlled species disappeared from projection");
  }

  const world = create(ActorWorldProjectionSchema, {
    ...cache.world,
    completedTick: batch.throughCompletedTick,
    worldRevision: batch.worldRevision,
    simulatedHours: batch.simulatedHours,
    lifecycle: batch.lifecycle,
    tiles: [...tiles.values()].sort((left, right) => left.tileId - right.tileId),
    species: [...species.values()].sort(compareSpeciesIds),
  });
  try {
    validateWorld(world);
  } catch {
    return resync(cache, "replacement produced an invalid projection");
  }
  return {
    status: "applied",
    cache: {
      projectionStreamId: cache.projectionStreamId,
      streamRevision: batch.targetStreamRevision,
      world,
    },
  };
}

function validateWorld(world: ActorWorldProjection) {
  const tiles = unique(world.tiles, (tile) => tile.tileId);
  const species = unique(world.species, (item) => item.speciesId);
  if (
    world.worldId === 0n ||
    world.tickDurationHours === 0 ||
    world.width === 0 ||
    world.height === 0 ||
    world.worldRulesHash.length === 0 ||
    world.controlledSpeciesId === 0n ||
    tiles === null ||
    species === null ||
    !species.has(world.controlledSpeciesId)
  ) {
    throw new Error("Projection world is structurally invalid.");
  }
}

function indexBy<T, TKey>(values: readonly T[], key: (value: T) => TKey): Map<TKey, T> {
  return new Map(values.map((value) => [key(value), value]));
}

function unique<T, TKey>(
  values: readonly T[],
  key: (value: T) => TKey,
): Map<TKey, T> | null {
  const result = new Map<TKey, T>();
  for (const value of values) {
    const id = key(value);
    if (result.has(id)) return null;
    result.set(id, value);
  }
  return result;
}

function uniqueValues<T>(values: readonly T[]): Set<T> | null {
  const result = new Set(values);
  return result.size === values.length ? result : null;
}

function intersects<T>(left: Iterable<T>, right: Set<T>): boolean {
  for (const value of left) if (right.has(value)) return true;
  return false;
}

function compareSpeciesIds(left: SpeciesProjection, right: SpeciesProjection): number {
  return left.speciesId < right.speciesId ? -1 : left.speciesId > right.speciesId ? 1 : 0;
}

function resync(cache: ProjectionCache, reason: string): ProjectionApplyResult {
  return { status: "resync-required", cache, reason };
}
