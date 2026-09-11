import {
  OrganismJourneyEventFamily,
  type OrganismJourneyEvent,
  type OrganismRoutineActivitySummary,
} from "../generated/lyfe/v1/projection_pb";

export const ACTIVITY_FAMILIES = [
  { family: OrganismJourneyEventFamily.BIRTH, label: "Birth", glyph: "✦", tone: "positive" },
  { family: OrganismJourneyEventFamily.REPRODUCTION, label: "Reproduction", glyph: "+", tone: "positive" },
  { family: OrganismJourneyEventFamily.RESOURCE_ABSORPTION, label: "Absorption", glyph: "↟", tone: "positive" },
  { family: OrganismJourneyEventFamily.FEEDING, label: "Feeding", glyph: "◆", tone: "positive" },
  { family: OrganismJourneyEventFamily.MIGRATION, label: "Migration", glyph: "→", tone: "neutral" },
  { family: OrganismJourneyEventFamily.DEATH, label: "Death", glyph: "×", tone: "negative" },
  { family: OrganismJourneyEventFamily.STRESS, label: "Stress", glyph: "!", tone: "negative" },
  { family: OrganismJourneyEventFamily.BEHAVIOR_TRANSITION, label: "Behavior", glyph: "~", tone: "neutral" },
] as const;

export const DEFAULT_ACTIVITY_FAMILIES = [
  OrganismJourneyEventFamily.BIRTH,
  OrganismJourneyEventFamily.REPRODUCTION,
  OrganismJourneyEventFamily.RESOURCE_ABSORPTION,
  OrganismJourneyEventFamily.FEEDING,
  OrganismJourneyEventFamily.DEATH,
] as const;

export function selectActivityPulseEvents(
  events: readonly OrganismJourneyEvent[],
  enabled: ReadonlySet<OrganismJourneyEventFamily>,
): readonly OrganismJourneyEvent[] {
  return events.filter((event) => enabled.has(event.family)).slice(-160);
}

export function ActivityFilters({
  enabled,
  onToggle,
}: {
  readonly enabled: ReadonlySet<OrganismJourneyEventFamily>;
  readonly onToggle: (family: OrganismJourneyEventFamily) => void;
}) {
  return (
    <fieldset className="activity-filters">
      <legend>Map activity</legend>
      {ACTIVITY_FAMILIES.map((item) => (
        <label key={item.family}>
          <input
            type="checkbox"
            checked={enabled.has(item.family)}
            onChange={() => onToggle(item.family)}
          />
          <span className={`event-key ${item.tone}`} aria-hidden="true">{item.glyph}</span>
          {item.label}
        </label>
      ))}
    </fieldset>
  );
}

export function OrganismJourneyPanel({
  activeOrganismId,
  journey,
  routineActivity,
}: {
  readonly activeOrganismId: bigint;
  readonly journey: readonly OrganismJourneyEvent[];
  readonly routineActivity: readonly OrganismRoutineActivitySummary[];
}) {
  return (
    <section className="journey-panel" aria-labelledby="journey-title">
      <div className="journey-heading">
        <div>
          <p className="section-label">Organism history</p>
          <h3 id="journey-title">Journey</h3>
        </div>
        <span className="journey-subject">#{activeOrganismId.toString()}</span>
      </div>
      {journey.length === 0 ? (
        <p className="journey-empty">No retained journey events for this organism.</p>
      ) : (
        <ol className="journey-list">
          {journey.slice(0, 24).map((event) => (
            <JourneyEntry key={event.eventId.toString()} event={event} />
          ))}
        </ol>
      )}
      {routineActivity.length > 0 ? (
        <>
          <p className="section-label routine-title">Routine uptake</p>
          <ol className="journey-list routine-list">
            {routineActivity.slice(0, 24).map((summary) => (
              <li key={`${summary.bucketStartHour}:${summary.periodHours}:${summary.tileId}`}>
                <span className="event-key positive" aria-hidden="true">↟</span>
                <div>
                  <strong>{summary.periodHours === 24 ? "Daily uptake" : "Recent uptake"}</strong>
                  <small>
                    Hour {summary.bucketStartHour.toLocaleString("en-US")} · tile {summary.tileId} · {summary.resourceAcquisitions.map((resource) =>
                      `resource ${resource.resourceId}: ${resource.amountQ.toLocaleString("en-US")} q`
                    ).join("; ")}
                  </small>
                </div>
              </li>
            ))}
          </ol>
        </>
      ) : null}
    </section>
  );
}

function JourneyEntry({ event }: { readonly event: OrganismJourneyEvent }) {
  const definition = ACTIVITY_FAMILIES.find((item) => item.family === event.family);
  return (
    <li>
      <span className={`event-key ${definition?.tone ?? "neutral"}`} aria-hidden="true">
        {definition?.glyph ?? "•"}
      </span>
      <div>
        <strong>{definition?.label ?? "Activity"}</strong>
        <small>
          Tick {event.tick.toLocaleString("en-US")} · tile {event.tileId}
          {event.amountQ > 0n ? ` · ${event.amountQ.toLocaleString("en-US")} q` : ""}
        </small>
        {event.family === OrganismJourneyEventFamily.DEATH &&
        event.deathCauseProbabilities.length > 0 ? (
          <small>
            {event.deathCauseProbabilities.map((cause) =>
              `cause ${cause.cause}: ${(cause.probabilityQ / 10_000).toFixed(1)}%${cause.triggered ? " (triggered)" : ""}`
            ).join("; ")}
          </small>
        ) : null}
      </div>
    </li>
  );
}
