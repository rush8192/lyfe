// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import {
  ActorWorldProjectionSchema,
  GameLossReason,
  GameProjectionSchema,
  GameRunStatus,
  JourneyDeathCauseProbabilitySchema,
  OrganismJourneyEventFamily,
  OrganismJourneyEventSchema,
} from "../generated/lyfe/v1/projection_pb";
import { LossPostmortemPanel } from "./LossPostmortemPanel";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

describe("loss postmortem interactions", () => {
  it("separates realized causes from risks and links to retained evidence", async () => {
    const world = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 2n,
      simulatedHours: 42n,
      gameplay: create(GameProjectionSchema, {
        runStatus: GameRunStatus.LOST,
        lossReason: GameLossReason.CONTROLLED_SPECIES_EXTINCT,
        ended: true,
        endedTick: 40n,
      }),
      journeyEvents: [create(OrganismJourneyEventSchema, {
        eventId: 9n,
        subjectSpeciesId: 2n,
        family: OrganismJourneyEventFamily.DEATH,
        deathCauseProbabilities: [
          create(JourneyDeathCauseProbabilitySchema, { cause: 1, probabilityQ: 800_000, triggered: true }),
          create(JourneyDeathCauseProbabilitySchema, { cause: 4, probabilityQ: 200_000, triggered: false }),
        ],
      })],
    });
    render(<LossPostmortemPanel world={world} />);

    expect(screen.getByRole("region", { name: "Recorded triggering causes" }).textContent)
      .toContain("Reserve exhaustion");
    expect(screen.getByRole("region", { name: "Recorded non-triggering risks" }).textContent)
      .toContain("Temperature exposure");

    await userEvent.setup().click(screen.getByRole("link", { name: "Review chronicle evidence" }));
    expect(window.location.hash).toBe("#chronicle-title");
  });

  it("does not show an ending while the run is active", () => {
    const world = create(ActorWorldProjectionSchema, {
      gameplay: create(GameProjectionSchema, { runStatus: GameRunStatus.ACTIVE }),
    });
    const { container } = render(<LossPostmortemPanel world={world} />);
    expect(container.childElementCount).toBe(0);
  });
});
