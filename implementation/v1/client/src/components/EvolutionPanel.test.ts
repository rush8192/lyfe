import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  EvolutionActivationWarningKind,
  EvolutionBenefitTiming,
  EvolutionDecisionSurfaceSchema,
  EvolutionPhenotypeSummarySchema,
  EvolutionRecurringCostChannel,
  EvolutionResourceFlowForecastStatus,
  EvolutionStrategicIntent,
  EvolutionTraitOptionSchema,
  SpeciationFailure,
  SpeciationProposalSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  buildPhenotypeComparisonRows,
  buildProposalComparisonRows,
  buildRecurringCostComparisonRows,
  buildStrategicIntentGroups,
} from "./EvolutionPanel";
import type { EvolutionGoal } from "../state/evolutionPlanner";

describe("evolution proposal comparison", () => {
  it("shows exact compiled values and marks only changed attributes", () => {
    const current = create(EvolutionPhenotypeSummarySchema, {
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
    const proposed = create(EvolutionPhenotypeSummarySchema, {
      ...current,
      resourceConservation: true,
    });

    const rows = buildPhenotypeComparisonRows(current, proposed);

    expect(rows).toHaveLength(9);
    expect(rows.filter((row) => row.changed)).toEqual([{
      label: "Resource conservation",
      current: "Disabled",
      proposed: "Enabled",
      changed: true,
    }]);
    expect(buildRecurringCostComparisonRows(current, proposed)).toEqual([{
      label: "Mandatory maintenance",
      current: "100 q/hour",
      proposed: "100 q/hour",
      changed: false,
    }]);
  });

  it("groups traits by their primary authored strategic intent", () => {
    const diversify = create(EvolutionTraitOptionSchema, {
      traitId: 7,
      strategicIntents: [EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS],
    });
    const regulation = create(EvolutionTraitOptionSchema, {
      traitId: 3,
      strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
    });
    const conservation = create(EvolutionTraitOptionSchema, {
      traitId: 4,
      strategicIntents: [EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE],
    });

    expect(buildStrategicIntentGroups([diversify, regulation, conservation])).toEqual([
      { intent: EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE, traits: [conservation] },
      { intent: EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS, traits: [diversify] },
      { intent: EvolutionStrategicIntent.INVEST_IN_COMPLEXITY, traits: [regulation] },
    ]);
  });

  it("compares a pinned goal with the current builder without ranking either plan", () => {
    const surface = create(EvolutionDecisionSurfaceSchema, {
      worldId: 1n,
      controlledSpeciesId: 2n,
      traits: [
        { traitId: 2, displayName: "Shared step", selectable: true },
        { traitId: 3, displayName: "Pinned milestone", selectable: true,
          prerequisiteTraitIds: [2] },
        { traitId: 4, displayName: "Builder milestone", selectable: true,
          prerequisiteTraitIds: [2] },
      ],
    });
    const pinnedGoal: EvolutionGoal = {
      worldId: 1n,
      controlledSpeciesId: 2n,
      traitIds: [2, 3],
      tileIds: [0],
    };
    const builderGoal: EvolutionGoal = {
      ...pinnedGoal,
      traitIds: [2, 4],
      tileIds: [1],
    };
    const pinned = create(SpeciationProposalSchema, {
      accepted: false,
      failure: SpeciationFailure.INSUFFICIENT_MUTATION_POINTS,
      mutationPriceQ: 40_000_000n,
      changeComplexity: 2,
      ancestorPopulationAfter: 50n,
      founderCounts: [{ tileId: 0, count: 50 }],
      benefitTiming: EvolutionBenefitTiming.PREPARATORY,
      strategicIntents: [EvolutionStrategicIntent.INVEST_IN_COMPLEXITY],
      activationWarnings: [{ kind: EvolutionActivationWarningKind.NO_COMPILED_CHANGE }],
      tileActivationEvidence: [{
        tileId: 0,
        reactionOpportunities: [{
          inputs: [{
            resourceId: 1,
            flowForecast: {
              status: EvolutionResourceFlowForecastStatus.NO_HISTORY,
            },
          }],
        }],
      }],
    });
    const builder = create(SpeciationProposalSchema, {
      accepted: true,
      failure: SpeciationFailure.NONE,
      mutationPriceQ: 30_000_000n,
      changeComplexity: 1,
      duplicatedBalanceAfterQ: 7_000_000n,
      ancestorPopulationAfter: 80n,
      founderCounts: [{ tileId: 1, count: 20 }],
      benefitTiming: EvolutionBenefitTiming.IMMEDIATE,
      strategicIntents: [EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE],
      tileActivationEvidence: [{
        tileId: 1,
        reactionOpportunities: [{
          inputs: [{
            resourceId: 1,
            flowForecast: {
              status: EvolutionResourceFlowForecastStatus.EXCEEDS_RECENT_RENEWAL,
              latestObservedCompetitorUptakeQ: 500n,
            },
          }],
        }],
      }],
    });

    const rows = buildProposalComparisonRows(
      surface,
      pinnedGoal,
      pinned,
      builderGoal,
      builder,
    );

    expect(rows.find((row) => row.label === "Milestone")).toMatchObject({
      saved: "Pinned milestone",
      current: "Builder milestone",
      changed: true,
    });
    expect(rows.find((row) => row.label === "Prerequisite closure")).toMatchObject({
      saved: "Shared step",
      current: "Shared step",
      changed: false,
    });
    expect(rows.find((row) => row.label === "Renewable-flow outlook")).toMatchObject({
      saved: "Waiting for history on 1 of 1 inputs",
      current: "Likely overshoot on 1 of 1 inputs",
    });
    expect(rows.find((row) => row.label === "Latest observed competitor uptake")?.current)
      .toBe("500 q across 1 tile-resource observation");
  });
});
