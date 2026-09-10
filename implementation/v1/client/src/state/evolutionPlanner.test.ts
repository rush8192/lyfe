import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  EvolutionActivationWarningKind,
  EvolutionActivationWarningSeverity,
  EvolutionBenefitTiming,
  EvolutionDecisionSurfaceSchema,
  EvolutionOccupiedTileSchema,
  EvolutionPhenotypeSummarySchema,
  EvolutionReactionOpportunityStatus,
  EvolutionRecurringCostChannel,
  EvolutionResourceFlowForecastStatus,
  EvolutionStrategicIntent,
  EvolutionTraitOptionSchema,
  SpeciationProposalSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  createEvolutionGoal,
  createProposalRequest,
  estimateEvolutionGoalAffordability,
  evolutionGoalMilestoneTraitIds,
  evolutionGoalPriceQ,
  isTraitReachable,
  readEvolutionGoal,
  removeEvolutionGoal,
  resolveEvolutionGoal,
  toggleTraitSelection,
  validateSpeciationProposal,
  writeEvolutionGoal,
  type EvolutionGoalStorage,
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

  it("saves one prerequisite-closed goal per world species", () => {
    const goalSurface = create(EvolutionDecisionSurfaceSchema, {
      ...surface,
      maximumChangeComplexity: 3,
      occupiedTiles: [create(EvolutionOccupiedTileSchema, { tileId: 0, population: 100n })],
      traits: [
        create(EvolutionTraitOptionSchema, {
          traitId: 1,
          displayName: "Root",
          acquired: true,
        }),
        create(EvolutionTraitOptionSchema, {
          traitId: 2,
          displayName: "Step",
          selectable: true,
          mutationPointCostQ: 10_000_000n,
          changeComplexity: 1,
          prerequisiteTraitIds: [1],
          incompatibleTraitIds: [],
        }),
        create(EvolutionTraitOptionSchema, {
          traitId: 3,
          displayName: "Milestone",
          selectable: true,
          mutationPointCostQ: 30_000_000n,
          changeComplexity: 1,
          prerequisiteTraitIds: [2],
          incompatibleTraitIds: [],
        }),
      ],
    });
    const storage = memoryGoalStorage();
    const goal = createEvolutionGoal(goalSurface, new Set([3]), new Set([0]));

    expect(goal.traitIds).toEqual([2, 3]);
    expect(goal.tileIds).toEqual([0]);
    expect(evolutionGoalMilestoneTraitIds(goalSurface, goal)).toEqual([3]);
    expect(evolutionGoalPriceQ(goalSurface, goal)).toBe(40_000_000n);

    writeEvolutionGoal(storage, goal);
    expect(readEvolutionGoal(storage, goalSurface)).toEqual(goal);
    expect(resolveEvolutionGoal(goalSurface, goal)).toEqual(goal);

    const otherSurface = create(EvolutionDecisionSurfaceSchema, {
      ...goalSurface,
      controlledSpeciesId: 9n,
    });
    const otherGoal = createEvolutionGoal(otherSurface, new Set([2]), new Set([0]));
    writeEvolutionGoal(storage, otherGoal);
    expect(readEvolutionGoal(storage, goalSurface)).toEqual(goal);
    expect(readEvolutionGoal(storage, otherSurface)).toEqual(otherGoal);

    removeEvolutionGoal(storage, goalSurface.worldId, goalSurface.controlledSpeciesId);
    expect(readEvolutionGoal(storage, goalSurface)).toBeNull();
    expect(readEvolutionGoal(storage, otherSurface)).toEqual(otherGoal);
  });

  it("estimates affordability only from the last completed mutation rate", () => {
    const earning = create(EvolutionDecisionSurfaceSchema, {
      completedTick: 8n,
      tickDurationHours: 2,
      mutationBalanceQ: 5_000_000n,
      lastMutationIncomeQ: 2_000_000n,
    });

    expect(estimateEvolutionGoalAffordability(earning, 8_000_000n)).toEqual({
      status: "estimated",
      remainingQ: 3_000_000n,
      estimatedHours: 3n,
    });
    expect(estimateEvolutionGoalAffordability(earning, 4_000_000n)).toEqual({
      status: "affordable",
      remainingQ: 0n,
      estimatedHours: 0n,
    });
    expect(estimateEvolutionGoalAffordability(create(EvolutionDecisionSurfaceSchema, {
      ...earning,
      completedTick: 0n,
    }), 8_000_000n).status).toBe("pending-first-tick");
    expect(estimateEvolutionGoalAffordability(create(EvolutionDecisionSurfaceSchema, {
      ...earning,
      lastMutationIncomeQ: 0n,
    }), 8_000_000n).status).toBe("no-current-income");
  });

  it("fails closed on malformed saved goals", () => {
    const storage = memoryGoalStorage();
    storage.setItem("lyfe.v1.evolution-goals", JSON.stringify({
      version: 1,
      goals: [{
        worldId: "1",
        controlledSpeciesId: "2",
        traitIds: [3, 2],
        tileIds: [0],
      }],
    }));

    expect(readEvolutionGoal(storage, surface)).toBeNull();
  });

  it("rejects activation warnings that contradict compiled phenotype evidence", () => {
    const request = createProposalRequest(surface, new Set([2]), new Set([1]));
    const phenotype = create(EvolutionPhenotypeSummarySchema, {
      mutationIncomeModifierQ: 1_000_000,
      maximumChangeComplexity: 3,
      maximumCaptureExtentsPerHour: 4,
      favorableCaptureEfficiencyQ: 700_000,
      maintenanceCostQPerHour: 100n,
      chargedReserveCapacityQ: 5_000n,
      minimumReproductionHealthQ: 650_000,
      activeReactionIds: [1, 4, 5],
      recurringCosts: [{
        channel: EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE,
        amountQPerOrganismHour: 100n,
      }],
    });
    const valid = create(SpeciationProposalSchema, {
      currentPhenotype: phenotype,
      proposedPhenotype: phenotype,
      benefitTiming: EvolutionBenefitTiming.PREPARATORY,
      strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      activationWarnings: [{
        kind: EvolutionActivationWarningKind.NO_COMPILED_CHANGE,
        severity: EvolutionActivationWarningSeverity.CAUTION,
        traitIds: [2],
      }],
    });
    expect(() => validateSpeciationProposal(valid, request)).not.toThrow();

    const contradictory = create(SpeciationProposalSchema, {
      ...valid,
      proposedPhenotype: create(EvolutionPhenotypeSummarySchema, {
        ...phenotype,
        resourceConservation: true,
      }),
    });
    expect(() => validateSpeciationProposal(contradictory, request)).toThrow("contradicts");

    const duplicateIntent = create(SpeciationProposalSchema, {
      ...valid,
      strategicIntents: [
        EvolutionStrategicIntent.INVEST_IN_COMPLEXITY,
        EvolutionStrategicIntent.INVEST_IN_COMPLEXITY,
      ],
    });
    expect(() => validateSpeciationProposal(duplicateIntent, request)).toThrow("invalid phenotype");
  });

  it("accepts coherent flow evidence and rejects a contradictory overshoot status", () => {
    const request = createProposalRequest(surface, new Set([2]), new Set([1]));
    const phenotype = create(EvolutionPhenotypeSummarySchema, {
      mutationIncomeModifierQ: 1_000_000,
      maximumChangeComplexity: 3,
      maximumCaptureExtentsPerHour: 250,
      favorableCaptureEfficiencyQ: 800_000,
      maintenanceCostQPerHour: 100n,
      chargedReserveCapacityQ: 5_000n,
      minimumReproductionHealthQ: 650_000,
      activeReactionIds: [1],
      recurringCosts: [{
        channel: EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE,
        amountQPerOrganismHour: 100n,
      }],
    });
    const evidence = [{
      tileId: 1,
      founderCount: 50,
      reactionOpportunities: [{
        reactionId: 1,
        status: EvolutionReactionOpportunityStatus.AVAILABLE,
        stockSupportedExtents: 1_000n,
        limitingResourceId: 1,
        inputs: [{
          resourceId: 1,
          tileStockQ: 4_000n,
          accessibleStockQ: 4_000n,
          requiredPerExtentQ: 4n,
          flowForecast: {
            status: EvolutionResourceFlowForecastStatus.WITHIN_RECENT_RENEWAL,
            historyPeriodHours: 24,
            recentEnvironmentalInflowQ: 100_000n,
            recentEnvironmentalOutflowQ: 10_000n,
            recentOrganismUptakeQ: 60_000n,
            proposedCohortDemandQ: 80_000n,
            latestObservationPeriodHours: 1,
            latestControlledSpeciesUptakeQ: 1_000n,
            latestObservedCompetitorUptakeQ: 500n,
          },
        }],
      }],
    }];
    const valid = create(SpeciationProposalSchema, {
      founderCounts: [{ tileId: 1, count: 50 }],
      currentPhenotype: phenotype,
      proposedPhenotype: phenotype,
      benefitTiming: EvolutionBenefitTiming.PREPARATORY,
      strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      tileActivationEvidence: evidence,
    });

    expect(() => validateSpeciationProposal(valid, request)).not.toThrow();

    const contradictory = create(SpeciationProposalSchema, valid);
    contradictory.tileActivationEvidence[0]
      .reactionOpportunities[0]
      .inputs[0]
      .flowForecast!.status = EvolutionResourceFlowForecastStatus.EXCEEDS_RECENT_RENEWAL;
    expect(() => validateSpeciationProposal(contradictory, request)).toThrow(
      "invalid tile activation evidence",
    );
  });
});

function memoryGoalStorage(): EvolutionGoalStorage {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => { values.set(key, value); },
    removeItem: (key) => { values.delete(key); },
  };
}
