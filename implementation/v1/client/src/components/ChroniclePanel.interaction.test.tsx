// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { EvolutionDecisionSurfaceSchema } from "../generated/lyfe/v1/evolution_pb";
import {
  ActorWorldProjectionSchema,
  NotableEventFamily,
  NotableEventSchema,
  TileProjectionSchema,
} from "../generated/lyfe/v1/projection_pb";
import { ChroniclePanel } from "./ChroniclePanel";

afterEach(cleanup);

describe("chronicle interactions", () => {
  it("edits a private hypothesis and opens only currently live tile evidence", async () => {
    const navigate = vi.fn();
    const world = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 2n,
      tiles: [
        create(TileProjectionSchema, { tileId: 3, detail: { case: "live", value: {} } }),
        create(TileProjectionSchema, { tileId: 4, detail: { case: "reduced", value: {} } }),
      ],
      notableEvents: [
        create(NotableEventSchema, {
          eventId: 7n, completedTick: 5n, tileId: 3, speciesId: 2n,
          family: NotableEventFamily.FIRST_TILE_OCCUPATION,
        }),
        create(NotableEventSchema, {
          eventId: 8n, completedTick: 4n, tileId: 4, speciesId: 2n,
          family: NotableEventFamily.FIRST_TILE_OCCUPATION,
        }),
      ],
    });
    function Harness() {
      const [notes, setNotes] = useState<ReadonlyMap<bigint, string>>(new Map());
      return <ChroniclePanel
        world={world}
        evolution={create(EvolutionDecisionSurfaceSchema)}
        notes={notes}
        onNavigateResourceTile={navigate}
        onNoteChange={(eventId, text) => setNotes((current) => new Map(current).set(eventId, text))}
      />;
    }

    const user = userEvent.setup();
    render(<Harness />);
    const notes = screen.getAllByRole("textbox", { name: "Private hypothesis" });
    await user.type(notes[0], "Perhaps the warm vent helped.");
    expect((notes[0] as HTMLTextAreaElement).value).toBe("Perhaps the warm vent helped.");

    await user.click(screen.getByRole("button", { name: "Open live tile 3" }));
    expect(navigate).toHaveBeenCalledWith(3);
    expect((screen.getByRole("button", { name: "Tile 4 is not currently live" }) as HTMLButtonElement).disabled)
      .toBe(true);
  });
});
