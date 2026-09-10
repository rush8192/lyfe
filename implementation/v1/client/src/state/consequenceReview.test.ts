import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  ApplySpeciationResponseSchema,
  EvolutionBenefitTiming,
  EvolutionRecurringCostChannel,
  EvolutionStrategicIntent,
  SpeciationProposalRequestSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  ActorWorldProjectionSchema,
  OrganismActionGateReason,
  OrganismActionProcess,
  OrganismBehavior,
  OrganismJourneyEventFamily,
  ResourceFlowKind,
  ResourceFlowProcess,
  SpeciesPopulationScope,
} from "../generated/lyfe/v1/projection_pb";
import {
  buildConsequenceSnapshot,
  createConsequenceReview,
  readPinnedConsequenceReview,
  writeConsequenceReview,
  type ConsequenceReviewStorage,
} from "./consequenceReview";

describe("consequence review", () => {
  it("anchors a bounded review to an accepted authoritative lineage event", () => {
    const review = createConsequenceReview(beforeWorld(), appliedResponse(), proposalRequest());

    expect(review.eventId).toBe(44n);
    expect(review.applicationHour).toBe(10n);
    expect(review.cooldownEndHour).toBe(178n);
    expect(review.ancestorPopulationAfter).toBe(80n);
    expect(review.descendantPopulation).toBe(20n);
    expect(review.founderAverageHealthQ).toBe(800_000);
    expect(review.addedReactionIds).toEqual([2]);
  });

  it("round-trips the baseline and selects it only for its controlled descendant window", () => {
    const storage = memoryStorage();
    const review = createConsequenceReview(beforeWorld(), appliedResponse(), proposalRequest());
    writeConsequenceReview(storage, review);

    expect(readPinnedConsequenceReview(storage, afterWorld())).toEqual(review);
    expect(readPinnedConsequenceReview(storage, create(ActorWorldProjectionSchema, {
      ...afterWorld(),
      simulatedHours: 179n,
    }))).toBeNull();
    expect(readPinnedConsequenceReview(storage, create(ActorWorldProjectionSchema, {
      ...afterWorld(),
      controlledSpeciesId: 10n,
    }))).toBeNull();
  });

  it("compares only currently authorized exact and observed evidence", () => {
    const review = createConsequenceReview(beforeWorld(), appliedResponse(), proposalRequest());
    const snapshot = buildConsequenceSnapshot(review, afterWorld());

    expect(snapshot.elapsedHours).toBe(24n);
    expect(snapshot.remainingHours).toBe(144n);
    expect(snapshot.ancestor.population).toBe(76n);
    expect(snapshot.ancestor.populationScope).toBe(SpeciesPopulationScope.LIVE_TILES_OBSERVED);
    expect(snapshot.ancestor.birthsSinceBranch).toBeNull();
    expect(snapshot.descendant.population).toBe(23n);
    expect(snapshot.descendant.populationScope).toBe(SpeciesPopulationScope.WORLD_EXACT);
    expect(snapshot.descendant.birthsSinceBranch).toBe(1);
    expect(snapshot.descendant.deathsSinceBranch).toBe(1);
    expect(snapshot.descendant.organismsWithSuppressedActions).toBe(1);
    expect(snapshot.addedReactionFlow).toEqual({
      observed: true,
      amountQ: 300n,
      tileIds: [2],
      periodHours: 1,
    });
  });

  it("fails closed when browser state is malformed", () => {
    const storage = memoryStorage();
    storage.setItem("lyfe.v1.consequence-reviews", JSON.stringify({
      version: 1,
      reviews: [{ worldId: "1" }],
    }));

    expect(readPinnedConsequenceReview(storage, afterWorld())).toBeNull();
  });
});

function beforeWorld() {
  return create(ActorWorldProjectionSchema, {
    worldId: 1n,
    completedTick: 10n,
    simulatedHours: 10n,
    tickDurationHours: 1,
    controlledSpeciesId: 5n,
  });
}

function proposalRequest() {
  return create(SpeciationProposalRequestSchema, {
    worldId: 1n,
    ancestorSpeciesId: 5n,
    newTraitIds: [7],
    selectedTileIds: [2],
  });
}

function appliedResponse() {
  return create(ApplySpeciationResponseSchema, {
    applied: true,
    eventId: 44n,
    descendantSpeciesId: 9n,
    applicationTick: 10n,
    proposal: {
      accepted: true,
      ancestorPopulationBefore: 100n,
      ancestorPopulationAfter: 80n,
      descendantPopulation: 20n,
      resultingSpeciationNotBeforeTick: 178n,
      founderCounts: [{ tileId: 2, count: 20 }],
      tileActivationEvidence: [{
        tileId: 2,
        founderCount: 20,
        averageHealthQ: 800_000,
        averageReserveQ: 700_000,
        averageResourcePressureQ: 200_000,
      }],
      currentPhenotype: {
        activeReactionIds: [1],
        recurringCosts: [{
          channel: EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE,
          amountQPerOrganismHour: 10n,
        }],
      },
      proposedPhenotype: {
        activeReactionIds: [1, 2],
        recurringCosts: [{
          channel: EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE,
          amountQPerOrganismHour: 12n,
        }],
      },
      benefitTiming: EvolutionBenefitTiming.IMMEDIATE,
      strategicIntents: [EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS],
    },
  });
}

function afterWorld() {
  return create(ActorWorldProjectionSchema, {
    worldId: 1n,
    completedTick: 34n,
    simulatedHours: 34n,
    tickDurationHours: 1,
    controlledSpeciesId: 9n,
    species: [
      {
        speciesId: 5n,
        population: 76n,
        populationScope: SpeciesPopulationScope.LIVE_TILES_OBSERVED,
        behaviorCounts: [{ behavior: OrganismBehavior.BASELINE, count: 2n }],
      },
      {
        speciesId: 9n,
        population: 23n,
        populationScope: SpeciesPopulationScope.WORLD_EXACT,
        evolution: { averageHealthQ: 750_000 },
        behaviorCounts: [
          { behavior: OrganismBehavior.BASELINE, count: 1n },
          { behavior: OrganismBehavior.CONSERVING, count: 1n },
        ],
      },
    ],
    tiles: [{
      tileId: 2,
      detail: {
        case: "live",
        value: {
          observedAtTick: 34n,
          resourceFlowPeriodHours: 1,
          organisms: [
            {
              organismId: 1n,
              speciesId: 5n,
              relativeHealthQ: 600_000,
              reserveFactorQ: 500_000,
              recentAcquisitionCoverageQ: 400_000,
              resourcePressureQ: 300_000,
            },
            {
              organismId: 2n,
              speciesId: 9n,
              relativeHealthQ: 700_000,
              reserveFactorQ: 600_000,
              recentAcquisitionCoverageQ: 500_000,
              resourcePressureQ: 400_000,
              actionGateEvidence: [{
                process: OrganismActionProcess.REPRODUCTION,
                reason: OrganismActionGateReason.BEHAVIOR_SUPPRESSED,
              }],
            },
            {
              organismId: 3n,
              speciesId: 9n,
              relativeHealthQ: 800_000,
              reserveFactorQ: 700_000,
              recentAcquisitionCoverageQ: 600_000,
              resourcePressureQ: 200_000,
            },
          ],
          resourceFlowContributors: [{
            resourceId: 1,
            kind: ResourceFlowKind.ORGANISM_UPTAKE,
            process: ResourceFlowProcess.EXTERNAL_ENERGY_CAPTURE,
            reactionId: 2,
            speciesId: 9n,
            amountQ: 300n,
          }],
        },
      },
    }],
    journeyEvents: [
      {
        eventId: 1n,
        tick: 12n,
        family: OrganismJourneyEventFamily.BIRTH,
        subjectOrganismId: 2n,
        subjectSpeciesId: 9n,
        tileId: 2,
      },
      {
        eventId: 2n,
        tick: 13n,
        family: OrganismJourneyEventFamily.DEATH,
        subjectOrganismId: 4n,
        subjectSpeciesId: 9n,
        tileId: 2,
      },
    ],
  });
}

function memoryStorage(): ConsequenceReviewStorage {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
}
