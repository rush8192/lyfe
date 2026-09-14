// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, describe, expect, it } from "vitest";
import { EvolutionDecisionSurfaceSchema } from "../generated/lyfe/v1/evolution_pb";
import {
  ActorWorldProjectionSchema,
  OrganismJourneyEventFamily,
  ResourceDefinitionSchema,
  ResourceFlowKind,
  TileProjectionSchema,
  UnknownTileSchema,
} from "../generated/lyfe/v1/projection_pb";
import {
  TileInspectorPanel,
  observationAge,
  summarizeTileDeaths,
} from "./TileInspectorPanel";

afterEach(cleanup);

describe("tile inspector", () => {
  it("labels reduced evidence as last known and computes its simulation age", () => {
    const world = create(ActorWorldProjectionSchema, {
      completedTick: 12n,
      tickDurationHours: 2,
      width: 2,
      height: 1,
      resourceDefinitions: [create(ResourceDefinitionSchema, {
        resourceId: 3,
        displayName: "Hydrogen",
      })],
      tiles: [{
        tileId: 8,
        x: 1,
        y: 0,
        detail: {
          case: "reduced",
          value: { elevationMeters: -240, observedAtTick: 7n, knownPresentResourceIds: [3] },
        },
      }],
    });
    const html = renderToStaticMarkup(<TileInspectorPanel world={world} selectedTileId={8} />);

    expect(html).toContain("Last known");
    expect(html).toContain("10 simulation hours (5 ticks) ago");
    expect(html).toContain("Hydrogen");
    expect(html).toContain("No current organisms, remnants, exact stocks");
    expect(observationAge(12n, 7n, 2)).toEqual({ ticks: 5n, hours: 10n });
  });

  it("compares a live tile with current DNA and limits compounds to biological relevance", () => {
    const { world, evolution } = liveFixture();
    const html = renderToStaticMarkup(
      <TileInspectorPanel world={world} evolution={evolution} selectedTileId={7} />,
    );

    expect(html).toContain("Favorable for our current DNA");
    expect(html).toContain("Tile seasonal 38 °C–48 °C · DNA preferred 35 °C–55 °C");
    expect(html).toContain("Relevant compounds");
    expect(html).toContain("Hydrogen gas");
    expect(html).toContain("Abundant");
    expect(html).toContain("Inorganic phosphorus");
    expect(html).toContain("Missing");
    expect(html).toContain("Hydrogen sulfide");
    expect(html).not.toContain("Irrelevant methane");
    expect(html).not.toContain("Current authorized observation");
    expect(html).toContain("2 deaths");
    expect(html).toContain("Temperature exposure");
    expect(html).toContain("Last 336 ticks (14 days)");
    expect(html).toContain("Last 720 ticks (30 days)");
    expect(html).not.toContain("Last 24 ticks");
    expect(summarizeTileDeaths(world, 7, 72).total).toBe(2);
    expect(summarizeTileDeaths(world, 7, 336).total).toBe(3);
    expect(summarizeTileDeaths(world, 7, 720).total).toBe(3);
  });

  it("changes the death diagnosis window without changing authoritative history", async () => {
    const user = userEvent.setup();
    const { world, evolution } = liveFixture();
    render(<TileInspectorPanel world={world} evolution={evolution} selectedTileId={7} />);

    expect(screen.getByText("2 deaths")).toBeTruthy();
    await user.selectOptions(screen.getByRole("combobox", { name: "Death history window" }), "336");
    expect(screen.getByText("3 deaths")).toBeTruthy();
    expect(screen.getByText("Reserve exhaustion")).toBeTruthy();
  });

  it("does not leak live habitat or compound detail into unknown tiles", () => {
    const { world, evolution } = liveFixture();
    const unknown = create(ActorWorldProjectionSchema, {
      ...world,
      tiles: [create(TileProjectionSchema, {
        tileId: 9,
        detail: { case: "unknown", value: create(UnknownTileSchema) },
      })],
    });

    const html = renderToStaticMarkup(
      <TileInspectorPanel world={unknown} evolution={evolution} selectedTileId={9} />,
    );
    expect(html).toContain("No observation is available");
    expect(html).not.toContain("Hydrogen gas");
    expect(html).not.toContain("35 °C");
    expect(html).not.toContain("Why life is dying here");
  });
});

function liveFixture() {
  const world = create(ActorWorldProjectionSchema, {
    completedTick: 200n,
    tickDurationHours: 1,
    controlledSpeciesId: 1n,
    width: 2,
    height: 1,
    resourceDefinitions: [
      { resourceId: 1, displayName: "Hydrogen gas" },
      { resourceId: 2, displayName: "Inorganic phosphorus" },
      { resourceId: 9, displayName: "Irrelevant methane" },
      { resourceId: 10, displayName: "Hydrogen sulfide" },
    ],
    journeyEvents: [
      {
        eventId: 4n,
        tick: 10n,
        family: OrganismJourneyEventFamily.DEATH,
        subjectSpeciesId: 1n,
        tileId: 7,
        detailId: 1,
      },
      {
        eventId: 1n,
        tick: 150n,
        family: OrganismJourneyEventFamily.DEATH,
        subjectSpeciesId: 1n,
        tileId: 7,
        detailId: 4,
      },
      {
        eventId: 2n,
        tick: 190n,
        family: OrganismJourneyEventFamily.DEATH,
        subjectSpeciesId: 1n,
        tileId: 7,
        detailId: 7,
      },
      {
        eventId: 3n,
        tick: 195n,
        family: OrganismJourneyEventFamily.DEATH,
        subjectSpeciesId: 2n,
        tileId: 7,
        detailId: 1,
      },
    ],
    tiles: [{
      tileId: 7,
      x: 0,
      y: 0,
      detail: {
        case: "live",
        value: {
          elevationMeters: -120,
          baselineVolcanismQ: 700_000,
          resourceFlowPeriodHours: 1,
          resourceStocks: [
            { resourceId: 1, quantityQ: 20_000n },
            { resourceId: 2, quantityQ: 0n },
            { resourceId: 9, quantityQ: 5_000n },
            { resourceId: 10, quantityQ: 10_000n },
          ],
          resourceFlowContributors: [{
            resourceId: 1,
            kind: ResourceFlowKind.ORGANISM_UPTAKE,
            speciesId: 1n,
            amountQ: 100n,
          }],
        },
      },
    }],
  });
  const evolution = create(EvolutionDecisionSurfaceSchema, {
    completedTick: 200n,
    controlledSpeciesId: 1n,
    occupiedTiles: [{
      tileId: 7,
      averageEnvironmentalFactorQ: 920_000,
      elevationMeters: -120,
      hasCurrentClimate: true,
      currentTemperatureMilliC: 42_000,
      currentSurfaceMoistureQ: 1_000_000,
      currentAccessibleLightQ: 250_000,
      currentPrecipitationMicrometersPerHour: 120,
      currentCloudQ: 400_000,
      currentSurfaceLightQ: 600_000,
      seasonalTemperatureMinimumMilliC: 38_000,
      seasonalTemperatureMaximumMilliC: 48_000,
      baselineVolcanismQ: 700_000,
    }],
    habitatProfile: {
      preferredTemperatureMinimumMilliC: 35_000,
      preferredTemperatureMaximumMilliC: 55_000,
      hardTemperatureMinimumMilliC: 15_000,
      hardTemperatureMaximumMilliC: 75_000,
      requiresLight: false,
      relevantResources: [
        { resourceId: 1, metabolismInput: true },
        { resourceId: 2, healthRequirement: true },
        {
          resourceId: 10,
          environmentalHazard: true,
          softHazardThresholdQ: 50_000n,
          hardHazardThresholdQ: 200_000n,
        },
      ],
    },
  });
  return { world, evolution };
}
