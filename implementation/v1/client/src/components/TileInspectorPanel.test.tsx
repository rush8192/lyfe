import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import {
  ActorWorldProjectionSchema,
  ResourceDefinitionSchema,
} from "../generated/lyfe/v1/projection_pb";
import { TileInspectorPanel, observationAge } from "./TileInspectorPanel";

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

  it("shows exact live stocks without leaking them into unknown tiles", () => {
    const base = {
      completedTick: 12n,
      tickDurationHours: 1,
      width: 2,
      height: 1,
      resourceDefinitions: [{ resourceId: 3, displayName: "Hydrogen" }],
    };
    const live = create(ActorWorldProjectionSchema, {
      ...base,
      tiles: [{
        tileId: 7,
        detail: {
          case: "live",
          value: { resourceStocks: [{ resourceId: 3, quantityQ: 1_250n }] },
        },
      }],
    });
    const unknown = create(ActorWorldProjectionSchema, {
      ...base,
      tiles: [{ tileId: 9, detail: { case: "unknown", value: {} } }],
    });

    const liveHtml = renderToStaticMarkup(<TileInspectorPanel world={live} selectedTileId={7} />);
    const unknownHtml = renderToStaticMarkup(<TileInspectorPanel world={unknown} selectedTileId={9} />);
    expect(liveHtml).toContain("Exact current compounds");
    expect(liveHtml).toContain("1,250 q");
    expect(unknownHtml).toContain("No observation is available");
    expect(unknownHtml).not.toContain("Hydrogen");
    expect(unknownHtml).not.toContain("1,250");
  });
});
