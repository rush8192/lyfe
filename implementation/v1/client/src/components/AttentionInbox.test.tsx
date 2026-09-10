import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import {
  ActorWorldProjectionSchema,
  AttentionAlertClass,
  AttentionAlertKind,
  AttentionAlertSchema,
  NotableEventFamily,
} from "../generated/lyfe/v1/projection_pb";
import { AttentionInbox } from "./AttentionInbox";

describe("attention inbox", () => {
  it("labels critical evidence, rearming, and the absence of automatic pause", () => {
    const html = renderToStaticMarkup(
      <AttentionInbox world={create(ActorWorldProjectionSchema, {
        attentionAlerts: [create(AttentionAlertSchema, {
          alertId: 3n,
          alertClass: AttentionAlertClass.CRITICAL,
          kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
          eventFamily: NotableEventFamily.POPULATION_DANGER_THRESHOLD,
          completedTick: 40n,
          simulatedHours: 40n,
          speciesId: 2n,
          chronicleEventIds: [9n],
          deduplicationKey: "notable:7:species:2:tick:40",
        })],
      })} />,
    );

    expect(html).toContain("Attention inbox");
    expect(html).toContain("Critical");
    expect(html).toContain("low-population danger band");
    expect(html).toContain("rearms only after recovery above fifteen");
    expect(html).toContain('href="#chronicle-9"');
    expect(html).toContain("does not pause the simulation");
  });

  it("explains the trailing decline and sustained-health recovery rules", () => {
    const html = renderToStaticMarkup(
      <AttentionInbox world={create(ActorWorldProjectionSchema, {
        attentionAlerts: [
          create(AttentionAlertSchema, {
            alertId: 4n,
            alertClass: AttentionAlertClass.CRITICAL,
            kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
            eventFamily: NotableEventFamily.POPULATION_DECLINE_THRESHOLD,
            completedTick: 48n,
            simulatedHours: 48n,
            speciesId: 2n,
            chronicleEventIds: [10n],
          }),
          create(AttentionAlertSchema, {
            alertId: 5n,
            alertClass: AttentionAlertClass.CRITICAL,
            kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
            eventFamily: NotableEventFamily.SUSTAINED_LOW_HEALTH,
            completedTick: 54n,
            simulatedHours: 54n,
            speciesId: 2n,
            chronicleEventIds: [11n],
          }),
        ],
      })} />,
    );

    expect(html).toContain("declined sharply over 24 hours");
    expect(html).toContain("rearms after the trailing decline recovers below 10%");
    expect(html).toContain("sustained low health");
    expect(html).toContain("rearms only after health stays above 35% for six hours");
  });

  it("separates realized death evidence from sustained resource pressure", () => {
    const html = renderToStaticMarkup(
      <AttentionInbox world={create(ActorWorldProjectionSchema, {
        attentionAlerts: [
          create(AttentionAlertSchema, {
            alertId: 6n,
            alertClass: AttentionAlertClass.CRITICAL,
            kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
            eventFamily: NotableEventFamily.REALIZED_DEATH_MECHANISM,
            completedTick: 60n,
            simulatedHours: 60n,
            speciesId: 2n,
            chronicleEventIds: [12n],
          }),
          create(AttentionAlertSchema, {
            alertId: 7n,
            alertClass: AttentionAlertClass.STRATEGIC,
            kind: AttentionAlertKind.NOTABLE_EVENT_GROUP,
            eventFamily: NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE,
            completedTick: 66n,
            simulatedHours: 66n,
            speciesId: 2n,
            chronicleEventIds: [13n],
          }),
        ],
      })} />,
    );

    expect(html).toContain("new realized death mechanism");
    expect(html).toContain("not a prediction of extinction");
    expect(html).toContain("sustained resource pressure");
    expect(html).toContain("pressure stays below 55% for six hours");
  });
});
