import { create, toBinary } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  ActorWorldProjectionSchema,
  OrganismLifecyclePhase,
  ProjectionBatchSchema,
  ProjectionSnapshotSchema,
  SpeciesPopulationScope,
  TileProjectionSchema,
  WorldLifecycle,
} from "../generated/lyfe/v1/projection_pb";
import { applyProjectionBatch, createProjectionCache } from "./projectionCache";

describe("projection cache", () => {
  it("applies absolute replacements atomically to equal a fresh projection", () => {
    const initialWorld = worldAt(0n, liveTile(0n));
    const targetWorld = worldAt(1n, liveTile(1n));
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: initialWorld,
    }));
    const batch = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: initialWorld.worldRulesHash,
      baseStreamRevision: 1n,
      targetStreamRevision: 2n,
      fromExclusiveTick: 0n,
      throughCompletedTick: 1n,
      worldRevision: 1n,
      simulatedHours: 1n,
      lifecycle: WorldLifecycle.PAUSED_READY,
      tileReplacements: targetWorld.tiles,
    });

    const result = applyProjectionBatch(cache, batch);

    expect(result.status).toBe("applied");
    expect(result.cache.streamRevision).toBe(2n);
    expect(toBinary(ActorWorldProjectionSchema, result.cache.world)).toEqual(
      toBinary(ActorWorldProjectionSchema, targetWorld),
    );
    expect(cache.world.completedTick).toBe(0n);
  });

  it("ignores duplicates and requests resync for a gap without mutation", () => {
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 2n,
      projection: worldAt(1n, liveTile(1n)),
    }));
    const duplicate = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: cache.world.worldRulesHash,
      baseStreamRevision: 1n,
      targetStreamRevision: 2n,
    });
    const gap = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: cache.world.worldRulesHash,
      baseStreamRevision: 3n,
      targetStreamRevision: 4n,
      fromExclusiveTick: 1n,
      throughCompletedTick: 2n,
      worldRevision: 2n,
      simulatedHours: 2n,
    });

    expect(applyProjectionBatch(cache, duplicate)).toEqual({ status: "duplicate", cache });
    const result = applyProjectionBatch(cache, gap);
    expect(result.status).toBe("resync-required");
    expect(result.cache).toBe(cache);

    const replacement = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 11n,
      streamRevision: 1n,
      projection: worldAt(2n, liveTile(2n)),
    }));
    expect(replacement.world.completedTick).toBe(2n);
    expect(replacement.projectionStreamId).toBe(11n);
  });

  it("requests resync for wrong rules and an out-of-order batch", () => {
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 2n,
      projection: worldAt(1n, liveTile(1n)),
    }));
    const wrongRules = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: "b".repeat(64),
      baseStreamRevision: 2n,
      targetStreamRevision: 3n,
    });
    const outOfOrder = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: cache.world.worldRulesHash,
      baseStreamRevision: 1n,
      targetStreamRevision: 3n,
    });

    expect(applyProjectionBatch(cache, wrongRules).status).toBe("resync-required");
    expect(applyProjectionBatch(cache, outOfOrder).status).toBe("resync-required");
  });

  it("uses whole-tile replacement to evict live-only organisms", () => {
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(0n, liveTile(0n)),
    }));
    const reduced = create(TileProjectionSchema, {
      tileId: 0,
      x: 0,
      y: 0,
      detail: {
        case: "reduced",
        value: { elevationMeters: -100, observedAtTick: 0n, knownPresentResourceIds: [1] },
      },
    });
    const batch = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: cache.world.worldRulesHash,
      baseStreamRevision: 1n,
      targetStreamRevision: 2n,
      fromExclusiveTick: 0n,
      throughCompletedTick: 1n,
      worldRevision: 1n,
      simulatedHours: 1n,
      lifecycle: WorldLifecycle.PAUSED_READY,
      tileReplacements: [reduced],
    });

    const result = applyProjectionBatch(cache, batch);

    expect(result.status).toBe("applied");
    const detail = result.cache.world.tiles[0].detail;
    expect(detail.case).toBe("reduced");
    if (detail.case !== "reduced") throw new Error("Expected reduced tile.");
    expect("organisms" in detail.value).toBe(false);
  });
});

function worldAt(completedTick: bigint, tile: ReturnType<typeof liveTile>) {
  return create(ActorWorldProjectionSchema, {
    worldId: 1n,
    completedTick,
    worldRevision: completedTick,
    simulatedHours: completedTick,
    tickDurationHours: 1,
    lifecycle: WorldLifecycle.PAUSED_READY,
    worldRulesHash: "a".repeat(64),
    width: 1,
    height: 1,
    wrapX: true,
    controlledSpeciesId: 1n,
    tiles: [tile],
    species: [
      {
        speciesId: 1n,
        populationScope: SpeciesPopulationScope.WORLD_EXACT,
        population: 1n,
      },
    ],
  });
}

function liveTile(age: bigint) {
  return create(TileProjectionSchema, {
    tileId: 0,
    x: 0,
    y: 0,
    detail: {
      case: "live",
      value: {
        elevationMeters: -100,
        observedAtTick: age,
        resourceStocks: [{ resourceId: 1, quantityQ: 1_000n }],
        organisms: [
          {
            organismId: 1n,
            speciesId: 1n,
            positionXQ: 100,
            positionYQ: 200,
            biologicalAgeHours: age,
            lifecyclePhase: OrganismLifecyclePhase.MATURE,
            structuralMatterQ: 1_000n,
            chargedReserveQ: 5_000n,
          },
        ],
      },
    },
  });
}
