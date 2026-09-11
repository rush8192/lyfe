// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  ApplySpeciationResponseSchema,
  EvolutionBenefitTiming,
  EvolutionDecisionSurfaceSchema,
  EvolutionPhenotypeSummarySchema,
  EvolutionStrategicIntent,
  SpeciationFailure,
  SpeciationProposalSchema,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";
import { EvolutionPanel } from "./EvolutionPanel";

const server = vi.hoisted(() => ({
  previewSpeciation: vi.fn(),
  applySpeciation: vi.fn(),
}));

vi.mock("../api/server", () => ({
  previewSpeciation: server.previewSpeciation,
  applySpeciation: server.applySpeciation,
  classifyServerFailure: (error: unknown) => ({
    kind: "protocol",
    message: error instanceof Error ? error.message : "Request failed",
  }),
}));

afterEach(cleanup);

beforeEach(() => {
  localStorage.clear();
  server.previewSpeciation.mockReset();
  server.applySpeciation.mockReset();
  vi.stubGlobal("crypto", { randomUUID: () => "interaction-test-command" });
  server.previewSpeciation.mockImplementation(async (request: SpeciationProposalRequest) =>
    proposalFor(request));
});

describe("evolution decision interactions", () => {
  it("closes prerequisites, saves and compares proposals, then applies the selected branch", async () => {
    const onApplied = vi.fn();
    const user = userEvent.setup();
    server.applySpeciation.mockImplementation(async () => create(ApplySpeciationResponseSchema, {
      applied: true,
      eventId: 99n,
      descendantSpeciesId: 3n,
      applicationTick: 1n,
      proposal: proposalFor({ selectedTileIds: [0], newTraitIds: [2, 4] } as SpeciationProposalRequest),
    }));

    render(<EvolutionPanel surface={decisionSurface()} onApplied={onApplied} />);

    await user.click(screen.getByRole("checkbox", { name: /Milestone adaptation/ }));
    expect(traitCheckbox("Prerequisite step").checked)
      .toBe(true);
    expect(traitCheckbox("Milestone adaptation").checked)
      .toBe(true);
    await screen.findByText("Ready to branch");
    expect(server.previewSpeciation.mock.calls.at(-1)?.[0].newTraitIds).toEqual([2, 3]);

    await user.click(screen.getByRole("button", { name: "Save as evolution goal" }));
    expect(await screen.findByText("Evolution goal saved for this species.")).toBeTruthy();
    expect(localStorage.length).toBe(1);

    await user.click(screen.getByRole("checkbox", { name: /Milestone adaptation/ }));
    await user.click(screen.getByRole("checkbox", { name: /Alternative adaptation/ }));
    expect(traitCheckbox("Prerequisite step").checked)
      .toBe(true);
    expect(await screen.findByRole("heading", { name: "Compare branch proposals" })).toBeTruthy();
    expect(screen.getByText(/ranks neither plan/i)).toBeTruthy();
    await waitFor(() => {
      expect(server.previewSpeciation.mock.calls.some(([request]) =>
        request.newTraitIds.join(",") === "2,4")).toBe(true);
    });

    await user.click(screen.getByRole("button", { name: "Found descendant species" }));
    await waitFor(() => expect(onApplied).toHaveBeenCalledTimes(1));
    expect(onApplied.mock.calls[0][1].newTraitIds).toEqual([2, 4]);
  });
});

function decisionSurface() {
  return create(EvolutionDecisionSurfaceSchema, {
    worldId: 1n,
    completedTick: 1n,
    controlledSpeciesId: 2n,
    controlledPopulation: 100n,
    evolutionRevision: 3n,
    genomeHash: "a".repeat(64),
    mutationBalanceQ: 100_000_000n,
    lastMutationIncomeQ: 1_000_000n,
    maximumChangeComplexity: 4,
    occupiedTiles: [{ tileId: 0, population: 100n }],
    traits: [
      {
        traitId: 1, displayName: "Ancestral root", family: "Root", acquired: true,
        strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      },
      {
        traitId: 2, displayName: "Prerequisite step", family: "Structure", selectable: true,
        mutationPointCostQ: 10_000_000n, changeComplexity: 1, prerequisiteTraitIds: [1],
        strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      },
      {
        traitId: 3, displayName: "Milestone adaptation", family: "Structure", selectable: true,
        mutationPointCostQ: 20_000_000n, changeComplexity: 1, prerequisiteTraitIds: [2],
        strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      },
      {
        traitId: 4, displayName: "Alternative adaptation", family: "Pressure", selectable: true,
        mutationPointCostQ: 20_000_000n, changeComplexity: 1, prerequisiteTraitIds: [2],
        strategicIntents: [EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE],
      },
    ],
  });
}

function proposalFor(request: SpeciationProposalRequest) {
  return create(SpeciationProposalSchema, {
    accepted: true,
    failure: SpeciationFailure.NONE,
    mutationPriceQ: BigInt(request.newTraitIds.length) * 10_000_000n,
    changeComplexity: request.newTraitIds.length,
    founderCounts: request.selectedTileIds.map((tileId) => ({ tileId, count: 20 })),
    balanceBeforeQ: 100_000_000n,
    duplicatedBalanceAfterQ: 70_000_000n,
    ancestorPopulationBefore: 100n,
    ancestorPopulationAfter: 80n,
    descendantPopulation: 20n,
    resultingSpeciationNotBeforeTick: 169n,
    currentPhenotype: create(EvolutionPhenotypeSummarySchema),
    proposedPhenotype: create(EvolutionPhenotypeSummarySchema),
    benefitTiming: EvolutionBenefitTiming.IMMEDIATE,
    strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
  });
}

function traitCheckbox(name: string): HTMLInputElement {
  const match = screen.getAllByRole("checkbox").find((element) =>
    element.closest("label")?.querySelector("strong")?.textContent === name);
  if (match === undefined) throw new Error(`Trait checkbox not found: ${name}`);
  return match as HTMLInputElement;
}
