// @vitest-environment jsdom

import { create } from "@bufbuild/protobuf";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import {
  ActorWorldProjectionSchema,
  AttentionAlertClass,
  AttentionAlertKind,
  AttentionAlertSchema,
} from "../generated/lyfe/v1/projection_pb";
import { AttentionInbox } from "./AttentionInbox";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

describe("attention evidence navigation", () => {
  it("routes an alert to its authoritative chronicle event", async () => {
    const world = create(ActorWorldProjectionSchema, {
      attentionAlerts: [create(AttentionAlertSchema, {
        alertId: 1n,
        kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
        alertClass: AttentionAlertClass.STRATEGIC,
        speciesId: 2n,
        chronicleEventIds: [7n],
      })],
    });
    render(<AttentionInbox world={world} />);

    await userEvent.setup().click(screen.getByRole("link", { name: "Chronicle event #7" }));

    expect(window.location.hash).toBe("#chronicle-7");
  });
});
