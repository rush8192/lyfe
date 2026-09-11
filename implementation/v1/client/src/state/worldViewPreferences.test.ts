// @vitest-environment jsdom

import { beforeEach, describe, expect, it } from "vitest";
import { create } from "@bufbuild/protobuf";
import { ActorWorldProjectionSchema } from "../generated/lyfe/v1/projection_pb";
import {
  readWorldViewPreferences,
  resolveWorldViewSelection,
  updateWorldViewPreferences,
} from "./worldViewPreferences";

describe("world view preferences", () => {
  beforeEach(() => localStorage.clear());

  it("merges camera and tile selection state under one saved-world key", () => {
    updateWorldViewPreferences(localStorage, 17n, {
      camera: { centerXFraction: 0.95, centerYFraction: 0.4, relativeZoom: 3.5 },
    });
    updateWorldViewPreferences(localStorage, 17n, {
      selectedTileId: 12,
    });

    expect(localStorage.length).toBe(1);
    expect(readWorldViewPreferences(localStorage, 17n)).toEqual({
      version: 2,
      camera: { centerXFraction: 0.95, centerYFraction: 0.4, relativeZoom: 3.5 },
      selectedTileId: 12,
    });
    expect(readWorldViewPreferences(localStorage, 18n)).toBeNull();
  });

  it("retains only the selected tile and never restores an organism inspector", () => {
    const live = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 3n,
      tiles: [{
        tileId: 7,
        detail: {
          case: "live",
          value: { organisms: [{ organismId: 19n, speciesId: 3n }] },
        },
      }],
    });
    const reduced = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 3n,
      tiles: [{ tileId: 7, detail: { case: "reduced", value: {} } }],
    });
    const preferences = {
      version: 2 as const,
      selectedTileId: 7,
    };

    expect(resolveWorldViewSelection(live, preferences)).toEqual({
      selectedTileId: 7,
    });
    expect(resolveWorldViewSelection(reduced, preferences)).toEqual({
      selectedTileId: 7,
    });
  });

  it("fails closed for malformed or out-of-range local state", () => {
    localStorage.setItem("lyfe.world-view.v2.2", JSON.stringify({
      version: 2,
      camera: { centerXFraction: 1, centerYFraction: 0.4, relativeZoom: 3 },
    }));
    localStorage.setItem("lyfe.world-view.v2.3", JSON.stringify({
      version: 1,
    }));
    localStorage.setItem("lyfe.world-view.v2.4", "not json");

    expect(readWorldViewPreferences(localStorage, 2n)).toBeNull();
    expect(readWorldViewPreferences(localStorage, 3n)).toBeNull();
    expect(readWorldViewPreferences(localStorage, 4n)).toBeNull();
  });
});
