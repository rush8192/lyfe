import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  EvolutionDecisionSurfaceSchema,
  EvolutionTraitOptionSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  createProposalRequest,
  isTraitReachable,
  toggleTraitSelection,
} from "./evolutionPlanner";

describe("evolution planner", () => {
  const surface = create(EvolutionDecisionSurfaceSchema, {
    worldId: 1n,
    controlledSpeciesId: 2n,
    evolutionRevision: 3n,
    genomeHash: "a".repeat(64),
    traits: [
      { traitId: 1, displayName: "Root", acquired: true },
      { traitId: 2, displayName: "Step", selectable: true, prerequisiteTraitIds: [1] },
      { traitId: 3, displayName: "Milestone", selectable: true, prerequisiteTraitIds: [2] },
    ],
  });

  it("does not offer a path through an unacquired setup-only prerequisite", () => {
    const lockedSurface = create(EvolutionDecisionSurfaceSchema, {
      ...surface,
      traits: [
        ...surface.traits,
        create(EvolutionTraitOptionSchema, { traitId: 4, selectable: false }),
        create(EvolutionTraitOptionSchema, {
          traitId: 5,
          selectable: true,
          prerequisiteTraitIds: [4],
        }),
      ],
    });

    expect(isTraitReachable(lockedSurface, 5)).toBe(false);
    expect([...toggleTraitSelection(lockedSurface, new Set(), 5)]).toEqual([]);
  });

  it("closes prerequisites and removes dependent selections", () => {
    const selected = toggleTraitSelection(surface, new Set(), 3);
    expect([...selected]).toEqual([2, 3]);

    expect([...toggleTraitSelection(surface, selected, 2)]).toEqual([]);
  });

  it("creates a canonical optimistic proposal", () => {
    const request = createProposalRequest(surface, new Set([3, 2]), new Set([4, 1]));

    expect(request.newTraitIds).toEqual([2, 3]);
    expect(request.selectedTileIds).toEqual([1, 4]);
    expect(request.expectedEvolutionRevision).toBe(3n);
    expect(request.expectedGenomeHash).toHaveLength(64);
  });
});
