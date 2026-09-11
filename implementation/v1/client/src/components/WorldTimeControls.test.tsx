import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import {
  SimulationSpeedPreset,
  WorldControlStateSchema,
} from "../generated/lyfe/v1/control_pb";
import { GameRunStatus, WorldLifecycle } from "../generated/lyfe/v1/projection_pb";
import { WorldTimeControls } from "./WorldTimeControls";

describe("world time controls", () => {
  it("offers one running-state action and disables single-step", () => {
    const html = renderToStaticMarkup(
      <WorldTimeControls
        control={create(WorldControlStateSchema, {
          lifecycle: WorldLifecycle.RUNNING,
          speed: SimulationSpeedPreset.FAST,
          targetIntervalMs: 100,
          gameRunStatus: GameRunStatus.ACTIVE,
        })}
        simulationTimeLabel="Hour 12"
        tickDurationHours={1}
        visibleOrganismCount={100}
        busy={false}
        error={null}
        onCommand={() => undefined}
      />,
    );

    expect(html).toContain("Hour 12");
    expect(html).toContain("Pause time");
    expect(html).toContain("Step 1 hour");
    expect(html).not.toContain("Target cadence");
    expect(html).not.toContain("Visible projection");
    expect(html).toContain("disabled");
  });

  it("offers resume and single-step at a paused boundary", () => {
    const html = renderToStaticMarkup(
      <WorldTimeControls
        control={create(WorldControlStateSchema, {
          lifecycle: WorldLifecycle.PAUSED_READY,
          speed: SimulationSpeedPreset.NORMAL,
          targetIntervalMs: 1_000,
          gameRunStatus: GameRunStatus.ACTIVE,
        })}
        simulationTimeLabel="Hour 24"
        tickDurationHours={6}
        visibleOrganismCount={100}
        busy={false}
        error={null}
        onCommand={() => undefined}
      />,
    );

    expect(html).toContain("Hour 24");
    expect(html).toContain("Resume time");
    expect(html).toContain("Step 6 hours");
    expect(html).not.toContain("Target cadence");
  });

  it("keeps projection diagnostics behind debug UI mode", () => {
    const html = renderToStaticMarkup(
      <WorldTimeControls
        control={create(WorldControlStateSchema, {
          controlRevision: 7n,
          lifecycle: WorldLifecycle.RUNNING,
          speed: SimulationSpeedPreset.NORMAL,
          targetIntervalMs: 1_000,
          gameRunStatus: GameRunStatus.ACTIVE,
        })}
        simulationTimeLabel="Hour 30"
        tickDurationHours={1}
        visibleOrganismCount={96}
        showDebug
        busy={false}
        error={null}
        onCommand={() => undefined}
      />,
    );

    expect(html).toContain("Debug details");
    expect(html).toContain("Visible projection");
    expect(html).toContain("96 organisms");
    expect(html).toContain("Target cadence");
    expect(html).toContain("1,000 ms");
    expect(html).toContain("Control revision");
  });
});
