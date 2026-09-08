import { create, toBinary } from "@bufbuild/protobuf";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ActorWorldProjectionSchema,
  GameMode,
  GameRunStatus,
  ProjectionSnapshotSchema,
  SpeciesPopulationScope,
} from "../generated/lyfe/v1/projection_pb";
import {
  SpeciationFailure,
  SpeciationProposalRequestSchema,
  SpeciationProposalSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  formatSimulationHour,
  previewSpeciation,
  readActiveWorldProjection,
} from "./server";

afterEach(() => vi.unstubAllGlobals());

describe("projection transport", () => {
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
