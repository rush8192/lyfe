// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { EvolutionDecisionSurfaceSchema } from "../generated/lyfe/v1/evolution_pb";
import { ActorWorldProjectionSchema } from "../generated/lyfe/v1/projection_pb";
import type { ConsequenceReview } from "../state/consequenceReview";
import { ConsequenceReviewPanel } from "./ConsequenceReviewPanel";

afterEach(cleanup);

const review: ConsequenceReview = {
  worldId: 1n,
  eventId: 44n,
  ancestorSpeciesId: 5n,
  descendantSpeciesId: 9n,
  applicationTick: 10n,
  applicationHour: 10n,
  cooldownEndTick: 178n,
  cooldownEndHour: 178n,
  ancestorPopulationBefore: 100n,
  ancestorPopulationAfter: 80n,
  descendantPopulation: 20n,
  foundingTileIds: [2],
  acquiredTraitIds: [],
  addedReactionIds: [],
  resourceConservationAdded: false,
  strategicIntents: [],
  benefitTiming: 0,
  founderAverageHealthQ: null,
  founderAverageReserveQ: null,
  founderAverageResourcePressureQ: null,
  ancestorRecurringCostQPerOrganismHour: null,
  descendantRecurringCostQPerOrganismHour: null,
};

describe("consequence review timeline", () => {
  it("advances from an in-progress review to its authoritative cooldown boundary", () => {
    const evolution = create(EvolutionDecisionSurfaceSchema);
    const { rerender } = render(<ConsequenceReviewPanel
      review={review}
      world={create(ActorWorldProjectionSchema, { worldId: 1n, simulatedHours: 34n })}
      evolution={evolution}
    />);
    expect(screen.getByText("Review in progress")).toBeTruthy();
    expect(screen.getByText("24 / 168 hours")).toBeTruthy();

    rerender(<ConsequenceReviewPanel
      review={review}
      world={create(ActorWorldProjectionSchema, { worldId: 1n, simulatedHours: 178n })}
      evolution={evolution}
    />);

    expect(screen.getByText("Review boundary reached")).toBeTruthy();
    expect(screen.getByText("168 / 168 hours")).toBeTruthy();
  });
});
