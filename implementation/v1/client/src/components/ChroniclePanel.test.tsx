import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import {
  EvolutionDecisionSurfaceSchema,
  EvolutionTraitOptionSchema,
} from "../generated/lyfe/v1/evolution_pb";
import {
  ActorWorldProjectionSchema,
  LineageReviewCapabilityKind,
  LineageReviewEvidenceKind,
  LineageReviewEvidenceReferenceKind,
  LineageReviewLandmarkKind,
  LineageReviewLandmarkSchema,
  LineageReviewObservationSchema,
  NotableEventFamily,
  NotableEventSchema,
  NotableEventSignificance,
  OrganismBehavior,
  SpeciesPopulationScope,
  TileProjectionSchema,
} from "../generated/lyfe/v1/projection_pb";
import { ChroniclePanel } from "./ChroniclePanel";

describe("lineage chronicle", () => {
  it("renders authoritative notable events with clearly separate private notes", () => {
    const event = create(NotableEventSchema, {
      eventId: 7n,
      family: NotableEventFamily.FIRST_REACTION_EXECUTION,
      significance: NotableEventSignificance.STRATEGIC,
      significanceRuleVersion: 1,
      completedTick: 12n,
      simulatedHours: 12n,
      speciesId: 2n,
      tileId: 3,
      reactionId: 1,
      milestoneValue: 1n,
      deduplicationKey: "first-reaction:species:2:reaction:1",
    });
    const html = renderToStaticMarkup(
      <ChroniclePanel
        world={create(ActorWorldProjectionSchema, {
          controlledSpeciesId: 2n,
          reactionDefinitions: [{ reactionId: 1, displayName: "Hydrogen Acetogenesis" }],
          tiles: [create(TileProjectionSchema, {
            tileId: 3,
            detail: { case: "live", value: {} },
          })],
          notableEvents: [event],
        })}
        evolution={create(EvolutionDecisionSurfaceSchema)}
        notes={new Map([[7n, "Maybe the new niche made this possible."]])}
        onNoteChange={() => undefined}
        onNavigateResourceTile={() => undefined}
      />,
    );

    expect(html).toContain("World event · Strategic");
    expect(html).toContain("first ran Hydrogen Acetogenesis");
    expect(html).toContain("completed ledger first recorded");
    expect(html).toContain("Open live tile 3");
    expect(html).toContain("Maybe the new niche made this possible.");
    expect(html).toContain("not a simulation fact");
  });

  it("shows exact saved evidence for critical window thresholds", () => {
    const html = renderToStaticMarkup(
      <ChroniclePanel
        world={create(ActorWorldProjectionSchema, {
          controlledSpeciesId: 2n,
          notableEvents: [
            create(NotableEventSchema, {
              eventId: 8n,
              family: NotableEventFamily.POPULATION_DECLINE_THRESHOLD,
              significance: NotableEventSignificance.CRITICAL,
              significanceRuleVersion: 1,
              completedTick: 24n,
              simulatedHours: 24n,
              speciesId: 2n,
              milestoneValue: 75n,
              baselineValue: 100n,
              deduplicationKey: "population-decline:species:2:episode:1",
            }),
            create(NotableEventSchema, {
              eventId: 9n,
              family: NotableEventFamily.SUSTAINED_LOW_HEALTH,
              significance: NotableEventSignificance.CRITICAL,
              significanceRuleVersion: 1,
              completedTick: 30n,
              simulatedHours: 30n,
              speciesId: 2n,
              milestoneValue: 240_000n,
              baselineValue: 6n,
              deduplicationKey: "low-health:species:2:episode:1",
            }),
          ],
        })}
        evolution={create(EvolutionDecisionSurfaceSchema)}
        notes={new Map()}
        onNoteChange={() => undefined}
        onNavigateResourceTile={() => undefined}
      />,
    );

    expect(html).toContain("declined over 24 hours");
    expect(html).toContain("fell from 100 to 75");
    expect(html).toContain("25.0% decline");
    expect(html).toContain("remained in the low-health band");
    expect(html).toContain("Average health was 24.0%");
    expect(html).toContain("six consecutive hours above 35%");
  });

  it("names realized death causes and sustained composite pressure without forecasting", () => {
    const html = renderToStaticMarkup(
      <ChroniclePanel
        world={create(ActorWorldProjectionSchema, {
          controlledSpeciesId: 2n,
          tiles: [create(TileProjectionSchema, {
            tileId: 3,
            detail: { case: "live", value: {} },
          })],
          notableEvents: [
            create(NotableEventSchema, {
              eventId: 10n,
              family: NotableEventFamily.REALIZED_DEATH_MECHANISM,
              significance: NotableEventSignificance.CRITICAL,
              significanceRuleVersion: 1,
              completedTick: 31n,
              simulatedHours: 31n,
              speciesId: 2n,
              tileId: 3,
              sourceEventId: 44n,
              milestoneValue: 4n,
              deduplicationKey: "death-mechanism:species:2:cause:4",
            }),
            create(NotableEventSchema, {
              eventId: 11n,
              family: NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE,
              significance: NotableEventSignificance.STRATEGIC,
              significanceRuleVersion: 1,
              completedTick: 36n,
              simulatedHours: 36n,
              speciesId: 2n,
              milestoneValue: 800_000n,
              baselineValue: 6n,
              deduplicationKey: "resource-pressure:species:2:episode:1",
            }),
          ],
        })}
        evolution={create(EvolutionDecisionSurfaceSchema)}
        notes={new Map()}
        onNoteChange={() => undefined}
        onNavigateResourceTile={() => undefined}
      />,
    );

    expect(html).toContain("first temperature exposure death");
    expect(html).toContain("source journey event #44");
    expect(html).toContain("not a forecast of further deaths");
    expect(html).toContain("remained under high resource pressure");
    expect(html).toContain("Average composite resource pressure was 80.0%");
    expect(html).toContain("six consecutive hours below 55%");
  });

  it("labels cooldown and proposal follow-up facts without a success verdict", () => {
    const exact = create(LineageReviewObservationSchema, {
      speciesId: 2n,
      populationScope: SpeciesPopulationScope.WORLD_EXACT,
      population: 50n,
      averageHealthQ: 800_000,
      averageReserveQ: 700_000,
      averageAcquisitionCoverageQ: 900_000,
      averageResourcePressureQ: 300_000,
      occupiedTileCount: 1,
      behaviorCounts: [
        { behavior: OrganismBehavior.BASELINE, count: 30n },
        { behavior: OrganismBehavior.FORAGING, count: 20n },
      ],
      activityCountsAvailable: true,
      birthCount: 3n,
      migrationCount: 2n,
    });
    const observed = create(LineageReviewObservationSchema, {
      speciesId: 1n,
      populationScope: SpeciesPopulationScope.LIVE_TILES_OBSERVED,
      population: 45n,
      averageHealthQ: 750_000,
      averageReserveQ: 650_000,
      averageAcquisitionCoverageQ: 850_000,
      averageResourcePressureQ: 350_000,
      occupiedTileCount: 1,
      behaviorCounts: [
        { behavior: OrganismBehavior.BASELINE, count: 40n },
        { behavior: OrganismBehavior.CONSERVING, count: 5n },
      ],
    });
    const landmark = create(LineageReviewLandmarkSchema, {
      eventId: 2n,
      kind: LineageReviewLandmarkKind.PROPOSAL_FOLLOW_UP,
      completedTick: 720n,
      simulatedHours: 720n,
      windowHours: 720n,
      speciationEventId: 1n,
      ancestorSpeciesId: 1n,
      descendantSpeciesId: 2n,
      perspectiveSpeciesId: 2n,
      traitIds: [4],
      evidenceKind: LineageReviewEvidenceKind.CONDITION_AND_PRESSURE,
      perspectiveBaseline: {
        ...exact,
        activityCountsAvailable: false,
        birthCount: 0n,
        migrationCount: 0n,
      },
      comparisonBaseline: observed,
      perspectiveCurrent: exact,
      comparisonCurrent: observed,
      capabilityActivations: [{
        kind: LineageReviewCapabilityKind.RESOURCE_CONSERVATION,
        sourceTraitId: 4,
        introducedByProposal: true,
        installed: true,
        activationCount: 12n,
      }],
      reactionActivations: [{
        reactionId: 1,
        installed: true,
        activationCount: 430n,
      }],
      evidenceReferences: [
        {
          kind: LineageReviewEvidenceReferenceKind.SPECIATION_DECISION,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
        {
          kind: LineageReviewEvidenceReferenceKind.REVIEWED_SPECIES_SUMMARY,
          speciesId: 2n,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
        {
          kind: LineageReviewEvidenceReferenceKind.COMPARISON_SPECIES_SUMMARY,
          speciesId: 1n,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
        {
          kind: LineageReviewEvidenceReferenceKind.REVIEWED_JOURNEY_WINDOW,
          speciesId: 2n,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
        {
          kind: LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW,
          speciesId: 2n,
          tileId: 3,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
        {
          kind: LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW,
          speciesId: 2n,
          tileId: 4,
          fromExclusiveTick: 0n,
          throughCompletedTick: 720n,
        },
      ],
    });
    const html = renderToStaticMarkup(
      <ChroniclePanel
        world={create(ActorWorldProjectionSchema, {
          controlledSpeciesId: 2n,
          reactionDefinitions: [{
            reactionId: 1,
            displayName: "Hydrogen Acetogenesis",
          }],
          tiles: [create(TileProjectionSchema, {
            tileId: 3,
            detail: { case: "live", value: {} },
          })],
          lineageReviewLandmarks: [landmark],
        })}
        evolution={create(EvolutionDecisionSurfaceSchema, {
          traits: [create(EvolutionTraitOptionSchema, {
            traitId: 4,
            displayName: "Resource Conservation",
          })],
        })}
        notes={new Map([[2n, "Perhaps lower pressure helped."]])}
        onNoteChange={() => undefined}
        onNavigateResourceTile={() => undefined}
      />,
    );

    expect(html).toContain("Proposal-specific follow-up landmark");
    expect(html).toContain("Resource Conservation");
    expect(html).toContain("30 days");
    expect(html).toContain("did not extend the 168-hour mechanical cooldown");
    expect(html).toContain("Window activity is unavailable at this observation scope—not zero");
    expect(html).toContain("Acquisition coverage");
    expect(html).toContain("90.0%");
    expect(html).toContain("Behavior mix");
    expect(html).toContain("Foraging");
    expect(html).toContain("Conserving");
    expect(html).toContain("Capability activation");
    expect(html).toContain("Introduced by this proposal");
    expect(html).toContain("12 recorded entries into");
    expect(html).toContain("This proposal installed no new compiled reaction");
    expect(html).toContain("Hydrogen Acetogenesis");
    expect(html).toContain("430 recorded reaction events");
    expect(html).toContain("Private hypothesis");
    expect(html).toContain("Perhaps lower pressure helped.");
    expect(html).toContain("not a simulation fact");
    expect(html).toContain("Follow the evidence");
    expect(html).toContain("Speciation decision facts");
    expect(html).toContain("Reviewed species summary");
    expect(html).toContain("Tile 3 live resource evidence");
    expect(html).toContain('href="#resource-title"');
    expect(html).toContain("Tile 4 resources · not currently live");
    expect(html).toContain("Reviewed organism Journey");
    expect(html).not.toMatch(/successful|failed branch/i);
  });
});
