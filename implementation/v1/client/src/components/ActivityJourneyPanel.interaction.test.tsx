// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import {
  OrganismJourneyEventFamily,
  OrganismJourneyEventSchema,
} from "../generated/lyfe/v1/projection_pb";
import {
  ActivityFilters,
  DEFAULT_ACTIVITY_FAMILIES,
  OrganismJourneyPanel,
  selectActivityPulseEvents,
} from "./ActivityJourneyPanel";

afterEach(cleanup);

describe("activity and organism journey interactions", () => {
  it("updates the rendered pulse selection when a player toggles an activity family", async () => {
    const events = [
      create(OrganismJourneyEventSchema, { eventId: 1n, family: OrganismJourneyEventFamily.BIRTH }),
      create(OrganismJourneyEventSchema, { eventId: 2n, family: OrganismJourneyEventFamily.STRESS }),
    ];
    function Harness() {
      const [enabled, setEnabled] = useState<ReadonlySet<OrganismJourneyEventFamily>>(
        new Set(DEFAULT_ACTIVITY_FAMILIES),
      );
      return <>
        <ActivityFilters enabled={enabled} onToggle={(family) => setEnabled((current) => {
          const next = new Set(current);
          if (next.has(family)) next.delete(family); else next.add(family);
          return next;
        })} />
        <output aria-label="Visible activity pulses">
          {selectActivityPulseEvents(events, enabled).map((event) => event.eventId.toString()).join(",")}
        </output>
      </>;
    }

    const user = userEvent.setup();
    render(<Harness />);
    expect(screen.getByLabelText("Visible activity pulses").textContent).toBe("1");

    await user.click(screen.getByRole("checkbox", { name: /Stress/ }));

    expect((screen.getByRole("checkbox", { name: /Stress/ }) as HTMLInputElement).checked).toBe(true);
    expect(screen.getByLabelText("Visible activity pulses").textContent).toBe("1,2");
  });

  it("renders the retained journey for the map-selected organism without another selection route", () => {
    const events = [10n, 20n].map((subjectOrganismId, index) =>
      create(OrganismJourneyEventSchema, {
        eventId: BigInt(index + 1),
        subjectOrganismId,
        family: index === 0 ? OrganismJourneyEventFamily.BIRTH : OrganismJourneyEventFamily.MIGRATION,
        tick: BigInt(index + 3),
        tileId: index,
      }));
    render(<OrganismJourneyPanel
      activeOrganismId={10n}
      journey={events.filter((event) => event.subjectOrganismId === 10n)}
      routineActivity={[]}
    />);
    const journey = screen.getByRole("region", { name: "Journey" });
    expect(within(journey).getByText("Birth")).toBeTruthy();
    expect(within(journey).getByText("#10")).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: "Organism to inspect" })).toBeNull();
    expect(within(journey).queryByText("Migration")).toBeNull();
  });
});
