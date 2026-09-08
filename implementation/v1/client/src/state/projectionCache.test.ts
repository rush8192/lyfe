import { create, toBinary } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  AcquisitionGateEvidenceSchema,
  AcquisitionGateReason,
  AcquisitionProcess,
  ActorWorldProjectionSchema,
  GameMode,
  GameRunStatus,
  OrganismLifecyclePhase,
  OrganismActionGateEvidenceSchema,
  OrganismActionGateReason,
  OrganismActionProcess,
  OrganismJourneyEventFamily,
  OrganismJourneyEventSchema,
  ProjectionBatchSchema,
  ProjectionSnapshotSchema,
  ResourceAcquisitionEvidenceSchema,
  ResourceBiologicalForm,
  ResourceEnvironmentalPhase,
  ResourceFlowHistoryIntervalSchema,
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
      gameplay: targetWorld.gameplay,
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
      gameplay: cache.world.gameplay,
      tileReplacements: [reduced],
    });

    const result = applyProjectionBatch(cache, batch);

    expect(result.status).toBe("applied");
    const detail = result.cache.world.tiles[0].detail;
    expect(detail.case).toBe("reduced");
    if (detail.case !== "reduced") throw new Error("Expected reduced tile.");
    expect("organisms" in detail.value).toBe(false);
  });

  it("applies an authoritative sandbox control transfer with the same batch", () => {
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(0n, liveTile(0n)),
    }));
    if (cache.world.gameplay === undefined) throw new Error("Expected gameplay state.");
    const gameplay = {
      ...cache.world.gameplay,
      controlledSpeciesId: 2n,
      gameplayRevision: 2n,
    };
    const secondSpecies = {
      speciesId: 2n,
      populationScope: SpeciesPopulationScope.WORLD_EXACT,
      population: 1n,
    };
    const batch = create(ProjectionBatchSchema, {
      projectionStreamId: 7n,
      worldId: 1n,
      worldRulesHash: cache.world.worldRulesHash,
      baseStreamRevision: 1n,
      targetStreamRevision: 2n,
      fromExclusiveTick: 0n,
      throughCompletedTick: 0n,
      worldRevision: 1n,
      simulatedHours: 0n,
      lifecycle: WorldLifecycle.PAUSED_READY,
      gameplay,
      speciesReplacements: [secondSpecies],
    });

    const result = applyProjectionBatch(cache, batch);

    expect(result.status).toBe("applied");
    expect(result.cache.world.controlledSpeciesId).toBe(2n);
    expect(result.cache.world.gameplay?.controlledSpeciesId).toBe(2n);
    expect(result.cache.world.species.map((species) => species.speciesId)).toEqual([1n, 2n]);
  });

  it("appends journey events exactly once and rejects reused identities", () => {
    const initialWorld = worldAt(0n, liveTile(0n));
    const cache = createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: initialWorld,
    }));
    const event = create(OrganismJourneyEventSchema, {
      eventId: 1n,
      tick: 1n,
      phase: 7,
      family: OrganismJourneyEventFamily.FEEDING,
      subjectOrganismId: 1n,
      subjectSpeciesId: 1n,
      tileId: 0,
      positionXQ: 100,
      positionYQ: 200,
      resourceId: 1,
      amountQ: 40n,
    });
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
      gameplay: initialWorld.gameplay,
      tileReplacements: [liveTile(1n)],
      journeyEventAppends: [event],
      routineActivitySummaries: [{
        bucketStartHour: 0n,
        periodHours: 1,
        subjectOrganismId: 1n,
        subjectSpeciesId: 1n,
        tileId: 0,
        resourceAcquisitions: [{ resourceId: 1, amountQ: 40n }],
      }],
      activityPulseEvents: [{
        ...event,
        eventId: 2n,
        family: OrganismJourneyEventFamily.RESOURCE_ABSORPTION,
      }],
    });

    const applied = applyProjectionBatch(cache, batch);
    expect(applied.status).toBe("applied");
    expect(applied.cache.world.journeyEvents).toHaveLength(1);
    expect(applied.cache.world.journeyEvents[0].eventId).toBe(1n);
    expect(applied.cache.world.routineActivitySummaries).toHaveLength(1);
    expect(applied.cache.world.activityPulseEvents[0].eventId).toBe(2n);

    const reused = create(ProjectionBatchSchema, {
      ...batch,
      baseStreamRevision: 2n,
      targetStreamRevision: 3n,
      fromExclusiveTick: 1n,
      throughCompletedTick: 2n,
      worldRevision: 2n,
      simulatedHours: 2n,
      journeyEventAppends: [{ ...event, tick: 2n }],
    });
    expect(applyProjectionBatch(applied.cache, reused).status).toBe("resync-required");
  });

  it("rejects malformed per-resource acquisition evidence", () => {
    const tile = liveTile(1n);
    if (tile.detail.case !== "live") throw new Error("Expected live tile.");
    tile.detail.value.organisms[0].resourceAcquisitionEvidence = [
      create(ResourceAcquisitionEvidenceSchema, {
        resourceId: 1,
        requestedQ: 10n,
        grantedQ: 11n,
        tileSupplyConstrained: true,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");
  });

  it("rejects malformed acquisition-gate evidence", () => {
    const tile = liveTile(1n);
    if (tile.detail.case !== "live") throw new Error("Expected live tile.");
    tile.detail.value.organisms[0].acquisitionGateEvidence = [
      create(AcquisitionGateEvidenceSchema, {
        process: AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE,
        reason: AcquisitionGateReason.INTERNAL_CAPACITY,
        availableQ: 2n,
        requiredQ: 2n,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");

    tile.detail.value.organisms[0].acquisitionGateEvidence = [
      create(AcquisitionGateEvidenceSchema, {
        process: AcquisitionProcess.SCAVENGING,
        reason: AcquisitionGateReason.COOLDOWN_ACTIVE,
        clearsAtTick: 1n,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");

    tile.detail.value.organisms[0].acquisitionGateEvidence = [
      create(AcquisitionGateEvidenceSchema, {
        process: AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE,
        reason: AcquisitionGateReason.COOLDOWN_ACTIVE,
        clearsAtTick: 2n,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");
  });

  it("rejects malformed organism action-gate evidence", () => {
    const tile = liveTile(1n);
    if (tile.detail.case !== "live") throw new Error("Expected live tile.");
    tile.detail.value.organisms[0].actionGateEvidence = [
      create(OrganismActionGateEvidenceSchema, {
        process: OrganismActionProcess.REPRODUCTION,
        reason: OrganismActionGateReason.RESOURCE_SUPPLY,
        availableQ: 0n,
        requiredQ: 1n,
        resourceId: 1,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");

    tile.detail.value.organisms[0].actionGateEvidence = [
      create(OrganismActionGateEvidenceSchema, {
        process: OrganismActionProcess.REPRODUCTION,
        reason: OrganismActionGateReason.COOLDOWN_ACTIVE,
        clearsAtTick: 1n,
      }),
    ];

    expect(() => createProjectionCache(create(ProjectionSnapshotSchema, {
      projectionStreamId: 7n,
      streamRevision: 1n,
      projection: worldAt(1n, tile),
    }))).toThrow("structurally invalid");
  });
});

function worldAt(completedTick: bigint, tile: ReturnType<typeof liveTile>) {
  if (tile.detail.case === "live") {
    tile.detail.value.resourceFlowHistory = completedTick === 0n ? [] : [create(
      ResourceFlowHistoryIntervalSchema,
      {
        completedTick,
        endSimulatedHour: completedTick,
        periodHours: 1,
      },
    )];
  }
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
    gameplay: {
      mode: GameMode.FREE_SANDBOX,
      runStatus: GameRunStatus.ACTIVE,
      controlledSpeciesId: 1n,
      gameplayRevision: 1n,
      roots: [{
        speciesId: 1n,
        founderGenomeId: 1,
        startingTileId: 0,
        initialPopulation: 1,
        playerSelected: true,
      }],
    },
    tiles: [tile],
    species: [
      {
        speciesId: 1n,
        populationScope: SpeciesPopulationScope.WORLD_EXACT,
        population: 1n,
      },
    ],
    resourceDefinitions: [{
      resourceId: 1,
      stableKey: "resource.test",
      displayName: "Test resource",
      biologicalForm: ResourceBiologicalForm.INORGANIC,
      environmentalPhase: ResourceEnvironmentalPhase.DISSOLVED,
    }],
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
        resourceFlowPeriodHours: 1,
        resourceFlowHistory: age === 0n ? [] : [{
          completedTick: age,
          endSimulatedHour: age,
          periodHours: 1,
        }],
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
