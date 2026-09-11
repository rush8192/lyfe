import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import {
  FoundingMetabolism,
  WorldSetupSurfaceSchema,
} from "../generated/lyfe/v1/setup_pb";
import { WorldSetupPanel } from "./WorldSetupPanel";

describe("world setup", () => {
  it("shows authored choices and bounded region evidence without hidden stocks", () => {
    const html = renderToStaticMarkup(
      <WorldSetupPanel
        surface={create(WorldSetupSurfaceSchema, {
          rootSeedHex: "00112233445566778899aabbccddeeff",
          worldPackId: "lyfe.world.primordial-earth-v1",
          worldPackVersion: "0.2.0",
          worldProfileDisplayName: "Primordial Earth 32 by 17",
          width: 32,
          height: 17,
          tickDurationHours: 1,
          founderPopulation: 100,
          founders: [{
            founderGenomeId: 1,
            stableKey: "genome.hydrogen-founder",
            displayName: "Hydrogen founder",
            metabolism: FoundingMetabolism.HYDROGEN_ACETOGENESIS,
            favorableCaptureEfficiencyQ: 800_000,
            maintenanceCostQPerHour: 56n,
            baseReproductionCooldownHours: 24n,
            reproductionCooldownJitterMaximumHours: 3,
          }],
          allocations: [{
            founderAllocationId: 1,
            displayName: "Balanced",
            isBaseline: true,
            captureEfficiencyMultiplierQ: 1_000_000,
            chemicalToleranceMultiplierQ: 1_000_000,
          }],
          startingRegions: [{
            startingPairIndex: 0,
            hydrogenDepthMeters: 200,
            hydrogenTemperatureMilliC: 44_500,
            hydrogenVolcanismQ: 800_000,
          }],
        })}
        busy={false}
        error={null}
        onPreviewSeed={() => undefined}
        onCreate={() => undefined}
      />,
    );

    expect(html).toContain("Kindle a world");
    expect(html).toContain("Free sandbox");
    expect(html).toContain("Survival");
    expect(html).toContain("Hydrogen founder");
    expect(html).toContain("24–27 hours");
    expect(html).toContain("200 m deep");
    expect(html).toContain("44.5 °C");
    expect(html).toContain("100 founders");
    expect(html).toContain("Exact resources and the rest of the map remain for observation");
    expect(html).not.toContain("tile 0");
    expect(html).not.toContain("resource stock");
  });
});
