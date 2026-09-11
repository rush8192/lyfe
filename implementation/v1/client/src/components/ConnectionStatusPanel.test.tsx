import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { ActorWorldProjectionSchema } from "../generated/lyfe/v1/projection_pb";
import { ConnectionStatusPanel } from "./ConnectionStatusPanel";

describe("connection status panel", () => {
  it("does not imply state exists during initial loading", () => {
    const html = renderToStaticMarkup(
      <ConnectionStatusPanel phase="loading" onRetry={() => undefined} />,
    );
    expect(html).toContain("Connecting to the world host");
    expect(html).toContain("until one complete authoritative boundary arrives");
  });

  it("preserves and labels the last complete boundary during recovery", () => {
    const world = create(ActorWorldProjectionSchema, {
      completedTick: 44n,
      simulatedHours: 88n,
    });
    const html = renderToStaticMarkup(
      <ConnectionStatusPanel
        phase="recovering"
        message="The world host could not be reached."
        world={world}
        onRetry={() => undefined}
      />,
    );
    expect(html).toContain("Connection interrupted — view held");
    expect(html).toContain("tick 44, hour 88");
    expect(html).toContain("Commands are locked");
    expect(html).toContain("Retry now");
  });

  it("offers recovery when no authoritative view was loaded", () => {
    const html = renderToStaticMarkup(
      <ConnectionStatusPanel
        phase="offline"
        message="The world host could not be reached."
        onRetry={() => undefined}
      />,
    );
    expect(html).toContain("World host unavailable");
    expect(html).toContain("Retry connection");
  });
});
