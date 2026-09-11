// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ActorWorldProjectionSchema,
  OrganismBehavior,
  OrganismLifecyclePhase,
  OrganismProjectionSchema,
  SpeciesPopulationScope,
} from "../generated/lyfe/v1/projection_pb";
import { OrganismInfoPanel } from "./OrganismInfoPanel";
import { SpeciesSummaryPanel, summarizeSpecies } from "./SpeciesSummaryPanel";

afterEach(cleanup);

const world = create(ActorWorldProjectionSchema, {
  controlledSpeciesId: 1n,
  species: [
    {
      speciesId: 1n,
      populationScope: SpeciesPopulationScope.WORLD_EXACT,
      population: 100n,
      evolution: { averageHealthQ: 750_000 },
    },
    {
      speciesId: 2n,
      populationScope: SpeciesPopulationScope.LIVE_TILES_OBSERVED,
      population: 2n,
    },
  ],
  tiles: [
    {
      tileId: 7,
      detail: {
        case: "live",
        value: {
          organisms: [
            { organismId: 1n, speciesId: 1n, relativeHealthQ: 750_000 },
            { organismId: 2n, speciesId: 2n, relativeHealthQ: 600_000 },
          ],
        },
      },
    },
    {
      tileId: 8,
      detail: {
        case: "live",
        value: {
          organisms: [
            { organismId: 3n, speciesId: 2n, relativeHealthQ: 800_000 },
          ],
        },
      },
    },
  ],
});

describe("species and organism side panels", () => {
  it("shows exact controlled-species totals in the default summary", () => {
    render(<SpeciesSummaryPanel world={world} speciesId={1n} />);

    const panel = screen.getByRole("region", { name: "Our species" });
    expect(within(panel).getByText("100")).toBeTruthy();
    expect(within(panel).getByText("75.0%")).toBeTruthy();
    expect(within(panel).getByText("1")).toBeTruthy();
    expect(within(panel).queryByRole("button", { name: /Back/ })).toBeNull();
  });

  it("labels other-species figures as live-tile observations and returns to the overview", async () => {
    const onBack = vi.fn();
    render(<SpeciesSummaryPanel world={world} speciesId={2n} onBack={onBack} />);

    const panel = screen.getByRole("region", { name: "Other species #2" });
    expect(within(panel).getByText("70.0%")).toBeTruthy();
    expect(within(panel).getAllByText("2")).toHaveLength(2);
    expect(within(panel).getByText(/observed members only/)).toBeTruthy();
    await userEvent.setup().click(within(panel).getByRole("button", { name: /Back/ }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it("shows high-level individual facts without an organism chooser", async () => {
    const onBack = vi.fn();
    const organism = create(OrganismProjectionSchema, {
      organismId: 19n,
      speciesId: 1n,
      biologicalAgeHours: 50n,
      lifecyclePhase: OrganismLifecyclePhase.MATURE,
      chargedReserveQ: 300n,
      chargedReserveCapacityQ: 600n,
      relativeHealthQ: 820_000,
      resourcePressureQ: 120_000,
      behavior: OrganismBehavior.FORAGING,
      successfulReproductionCount: 2n,
    });
    render(<OrganismInfoPanel
      organism={organism}
      tileId={7}
      journey={[]}
      routineActivity={[]}
      onBack={onBack}
    />);

    expect(screen.getByRole("region", { name: "Organism #19" })).toBeTruthy();
    expect(screen.getByText("82.0%")).toBeTruthy();
    expect(screen.getByText(/50.0% · 300 q/)).toBeTruthy();
    expect(screen.getByText("2 d 2 h")).toBeTruthy();
    expect(screen.getByText("Foraging")).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: "Organism to inspect" })).toBeNull();
    await userEvent.setup().click(screen.getByRole("button", { name: /Back/ }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it("drops an other-species summary as soon as the species leaves live observation", () => {
    const hidden = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 1n,
      species: world.species.filter((species) => species.speciesId === 1n),
      tiles: world.tiles,
    });
    expect(summarizeSpecies(hidden, 2n)).toBeNull();
  });
});
