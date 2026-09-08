import { create } from "@bufbuild/protobuf";
import {
  SpeciationProposalRequestSchema,
  type EvolutionDecisionSurface,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";

export function toggleTraitSelection(
  surface: EvolutionDecisionSurface,
  selected: ReadonlySet<number>,
  traitId: number,
): ReadonlySet<number> {
  const next = new Set(selected);
  if (next.has(traitId)) {
    next.delete(traitId);
    let changed = true;
    while (changed) {
      changed = false;
      for (const selectedId of [...next]) {
        const trait = surface.traits.find((candidate) => candidate.traitId === selectedId);
        if (trait?.prerequisiteTraitIds.some((id) =>
          !surface.traits.some((candidate) => candidate.traitId === id && candidate.acquired) &&
          !next.has(id))) {
          next.delete(selectedId);
          changed = true;
        }
      }
    }
    return next;
  }

  const addWithPrerequisites = (id: number): boolean => {
    const trait = surface.traits.find((candidate) => candidate.traitId === id);
    if (trait?.acquired || next.has(id)) return true;
    if (trait === undefined || !trait.selectable) return false;
    for (const prerequisiteId of trait.prerequisiteTraitIds) {
      if (!addWithPrerequisites(prerequisiteId)) return false;
    }
    next.add(id);
    return true;
  };
  addWithPrerequisites(traitId);
  return next;
}

export function isTraitReachable(
  surface: EvolutionDecisionSurface,
  traitId: number,
  visited: ReadonlySet<number> = new Set(),
): boolean {
  if (visited.has(traitId)) return false;
  const trait = surface.traits.find((candidate) => candidate.traitId === traitId);
  if (trait === undefined) return false;
  if (trait.acquired) return true;
  if (!trait.selectable) return false;
  const nextVisited = new Set(visited).add(traitId);
  return trait.prerequisiteTraitIds.every((id) =>
    isTraitReachable(surface, id, nextVisited));
}

export function createProposalRequest(
  surface: EvolutionDecisionSurface,
  traitIds: ReadonlySet<number>,
  tileIds: ReadonlySet<number>,
): SpeciationProposalRequest {
  return create(SpeciationProposalRequestSchema, {
    worldId: surface.worldId,
    ancestorSpeciesId: surface.controlledSpeciesId,
    expectedEvolutionRevision: surface.evolutionRevision,
    expectedGenomeHash: surface.genomeHash,
    newTraitIds: [...traitIds].sort((left, right) => left - right),
    selectedTileIds: [...tileIds].sort((left, right) => left - right),
    followDescendantIfPermitted: true,
  });
}
