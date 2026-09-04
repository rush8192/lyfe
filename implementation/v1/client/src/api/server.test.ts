import { create, toBinary } from "@bufbuild/protobuf";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ActorWorldProjectionSchema,
  ProjectionSnapshotSchema,
  SpeciesPopulationScope,
} from "../generated/lyfe/v1/projection_pb";
import { formatSimulationHour, readActiveWorldProjection } from "./server";

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
});
