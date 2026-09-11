import { create, toBinary } from "@bufbuild/protobuf";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ActorWorldProjectionSchema,
  GameMode,
  GameRunStatus,
  ProjectionSnapshotSchema,
  SpeciesPopulationScope,
  WorldLifecycle,
} from "../generated/lyfe/v1/projection_pb";
import {
  EvolutionDecisionSurfaceSchema,
  SpeciationFailure,
  SpeciationProposalRequestSchema,
  SpeciationProposalSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  ActiveWorldStateSchema,
  SimulationSpeedPreset,
  WorldControlAction,
  WorldControlCommandSchema,
  WorldControlStateSchema,
} from "../generated/lyfe/v1/control_pb";
import {
  CreateWorldRequestSchema,
  CreateWorldResponseSchema,
  WorldSetupSurfaceSchema,
} from "../generated/lyfe/v1/setup_pb";
import {
  LoadWorldRequestSchema,
  SaveActiveWorldRequestSchema,
  UnloadWorldRequestSchema,
  WorldCatalogueSchema,
} from "../generated/lyfe/v1/persistence_pb";
import {
  ServerRequestError,
  classifyServerFailure,
  createWorld,
  formatSimulationHour,
  loadWorld,
  previewSpeciation,
  readActiveWorldState,
  readActiveWorldProjection,
  readWorldSetup,
  readWorldCatalogue,
  saveActiveWorld,
  sendWorldControl,
  unloadWorld,
} from "./server";

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("projection transport", () => {
  it("classifies rejected, unavailable, and malformed server responses separately", () => {
    expect(classifyServerFailure(new ServerRequestError(
      "rejected",
      "Server returned HTTP 409.",
      409,
    ))).toEqual({ kind: "rejected", message: "Server returned HTTP 409." });
    expect(classifyServerFailure(new TypeError("fetch failed"))).toEqual({
      kind: "unavailable",
      message: "The world host could not be reached.",
    });
    expect(classifyServerFailure(new Error("bad projection"))).toEqual({
      kind: "protocol",
      message: "bad projection",
    });
  });

  it("turns an unresponsive transport into a bounded unavailable failure", async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) =>
      new Promise<Response>((_resolve, reject) => {
        init?.signal?.addEventListener("abort", () => reject(
          new DOMException("aborted", "AbortError"),
        ));
      }));
    vi.stubGlobal("fetch", fetchMock);

    const pending = readWorldCatalogue(new AbortController().signal);
    const rejection = expect(pending).rejects.toMatchObject({
      kind: "unavailable",
      message: "The world host did not respond within 8 seconds.",
    });
    await vi.advanceTimersByTimeAsync(8_001);
    await rejection;
    vi.useRealTimers();
  });

  it("preserves values beyond JavaScript's safe-number range", () => {
    expect(formatSimulationHour(9_007_199_254_740_993n)).toBe(
      "Hour 9,007,199,254,740,993",
    );
  });

  it("decodes the generated binary contract with native bigint fields", async () => {
    const expected = create(ActorWorldProjectionSchema, {
      worldId: 9_007_199_254_740_993n,
      completedTick: 12n,
      worldRevision: 13n,
      simulatedHours: 12n,
      tickDurationHours: 1,
      worldRulesHash: "a".repeat(64),
      lifecycle: WorldLifecycle.PAUSED_READY,
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
      tiles: [],
      species: [{
        speciesId: 1n,
        populationScope: SpeciesPopulationScope.WORLD_EXACT,
        population: 0n,
      }],
    });
    const snapshot = create(ProjectionSnapshotSchema, {
      projectionStreamId: 44n,
      streamRevision: 1n,
      projection: expected,
    });
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(toBinary(ProjectionSnapshotSchema, snapshot), {
          headers: { "content-type": "application/x-protobuf" },
        })),
    );

    const decoded = await readActiveWorldProjection(new AbortController().signal);

    expect(decoded.world.worldId).toBe(9_007_199_254_740_993n);
    expect(decoded.world).toEqual(expected);
    expect(decoded.projectionStreamId).toBe(44n);
  });

  it("decodes one authoritative boundary for projection, evolution, and control", async () => {
    const world = create(ActorWorldProjectionSchema, {
      worldId: 1n,
      completedTick: 12n,
      worldRevision: 13n,
      simulatedHours: 12n,
      tickDurationHours: 1,
      worldRulesHash: "a".repeat(64),
      lifecycle: WorldLifecycle.RUNNING,
      width: 1,
      height: 1,
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
      species: [{
        speciesId: 1n,
        populationScope: SpeciesPopulationScope.WORLD_EXACT,
      }],
    });
    const state = create(ActiveWorldStateSchema, {
      projection: create(ProjectionSnapshotSchema, {
        projectionStreamId: 1n,
        streamRevision: 1n,
        projection: world,
      }),
      evolution: create(EvolutionDecisionSurfaceSchema, {
        worldId: 1n,
        completedTick: 12n,
        worldRevision: 13n,
        controlledSpeciesId: 1n,
      }),
      control: create(WorldControlStateSchema, {
        controlRevision: 4n,
        lifecycle: WorldLifecycle.RUNNING,
        speed: SimulationSpeedPreset.FAST,
        targetIntervalMs: 100,
        completedTick: 12n,
        worldRevision: 13n,
        simulatedHours: 12n,
        gameRunStatus: GameRunStatus.ACTIVE,
      }),
    });
    vi.stubGlobal("fetch", vi.fn(async () =>
      new Response(toBinary(ActiveWorldStateSchema, state), {
        headers: { "content-type": "application/x-protobuf" },
      })));

    const decoded = await readActiveWorldState(new AbortController().signal);

    expect(decoded.cache.world.lifecycle).toBe(WorldLifecycle.RUNNING);
    expect(decoded.control.controlRevision).toBe(4n);
    expect(decoded.evolution.completedTick).toBe(12n);
  });

  it("posts an optimistic world-control command", async () => {
    const command = create(WorldControlCommandSchema, {
      expectedControlRevision: 7n,
      action: WorldControlAction.STEP,
    });
    const response = create(WorldControlStateSchema, {
      controlRevision: 8n,
      lifecycle: WorldLifecycle.PAUSED_READY,
      speed: SimulationSpeedPreset.NORMAL,
      targetIntervalMs: 1_000,
      completedTick: 1n,
      worldRevision: 1n,
      simulatedHours: 1n,
      gameRunStatus: GameRunStatus.ACTIVE,
    });
    const fetchMock = vi.fn(async () =>
      new Response(toBinary(WorldControlStateSchema, response), {
        headers: { "content-type": "application/x-protobuf" },
      }));
    vi.stubGlobal("fetch", fetchMock);

    const decoded = await sendWorldControl(command, new AbortController().signal);

    expect(decoded.completedTick).toBe(1n);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/v1/worlds/active/control",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("reads seed-specific setup and creates a world through generated contracts", async () => {
    const seed = "00112233445566778899aabbccddeeff";
    const setup = create(WorldSetupSurfaceSchema, {
      rootSeedHex: seed,
      width: 32,
      height: 17,
    });
    const created = create(CreateWorldResponseSchema, { worldId: 9n });
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) =>
      init?.method === "POST"
        ? new Response(toBinary(CreateWorldResponseSchema, created), {
            headers: { "content-type": "application/x-protobuf" },
          })
        : new Response(toBinary(WorldSetupSurfaceSchema, setup), {
            headers: { "content-type": "application/x-protobuf" },
          }));
    vi.stubGlobal("fetch", fetchMock);

    const decodedSetup = await readWorldSetup(seed, new AbortController().signal);
    const decodedCreated = await createWorld(create(CreateWorldRequestSchema, {
      rootSeedHex: seed,
      mode: GameMode.FREE_SANDBOX,
      founderGenomeId: 1,
      founderAllocationId: 1,
    }), new AbortController().signal);

    expect(decodedSetup.width).toBe(32);
    expect(decodedCreated.worldId).toBe(9n);
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      `/api/v1/worlds/setup?seed=${seed}`,
      expect.objectContaining({ headers: { Accept: "application/x-protobuf" } }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "/api/v1/worlds",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("reads the world catalogue and posts save, load, and unload lifecycle commands", async () => {
    const catalogue = create(WorldCatalogueSchema, {
      hasActiveWorld: true,
      activeWorldId: 9n,
      activeWorldRevision: 4n,
      activeHasUnsavedChanges: true,
      savedWorlds: [{
        worldId: 2n,
        simulatedHours: 12n,
        organismCount: 88,
      }],
    });
    const fetchMock = vi.fn(async (_input: RequestInfo | URL) =>
      new Response(toBinary(WorldCatalogueSchema, catalogue), {
        headers: { "content-type": "application/x-protobuf" },
      }));
    vi.stubGlobal("fetch", fetchMock);
    const signal = new AbortController().signal;

    const decoded = await readWorldCatalogue(signal);
    await saveActiveWorld(create(SaveActiveWorldRequestSchema, {
      expectedWorldId: 9n,
      expectedWorldRevision: 4n,
    }), signal);
    await loadWorld(create(LoadWorldRequestSchema, {
      worldId: 2n,
      expectedActiveWorldId: 9n,
      expectedActiveWorldRevision: 4n,
      confirmDiscardUnsaved: true,
    }), signal);
    await unloadWorld(create(UnloadWorldRequestSchema, {
      expectedWorldId: 9n,
      expectedWorldRevision: 4n,
      confirmDiscardUnsaved: true,
    }), signal);

    expect(decoded.activeHasUnsavedChanges).toBe(true);
    expect(decoded.savedWorlds[0]?.worldId).toBe(2n);
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/v1/worlds",
      "/api/v1/worlds/active/save",
      "/api/v1/worlds/load",
      "/api/v1/worlds/active/unload",
    ]);
  });

  it("posts and decodes an exact mutation proposal", async () => {
    const request = create(SpeciationProposalRequestSchema, {
      worldId: 1n,
      ancestorSpeciesId: 2n,
      expectedEvolutionRevision: 3n,
      expectedGenomeHash: "a".repeat(64),
      newTraitIds: [3],
      selectedTileIds: [0],
    });
    const response = create(SpeciationProposalSchema, {
      failure: SpeciationFailure.INSUFFICIENT_MUTATION_POINTS,
      mutationPriceQ: 40_000_000n,
      balanceBeforeQ: 10_000_000n,
      descendantPopulation: 50n,
    });
    const fetchMock = vi.fn(async () =>
      new Response(toBinary(SpeciationProposalSchema, response), {
        headers: { "content-type": "application/x-protobuf" },
      }));
    vi.stubGlobal("fetch", fetchMock);

    const decoded = await previewSpeciation(request, new AbortController().signal);

    expect(decoded.mutationPriceQ).toBe(40_000_000n);
    expect(decoded.descendantPopulation).toBe(50n);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/v1/worlds/active/evolution/preview",
      expect.objectContaining({ method: "POST" }),
    );
  });
});
