import { useEffect, useMemo, useState } from "react";
import {
  OrganismJourneyEventFamily,
  type ActorWorldProjection,
  type OrganismJourneyEvent,
} from "./generated/lyfe/v1/projection_pb";
import {
  formatSimulationHour,
  readActiveWorldState,
  type ServerConnection,
} from "./api/server";
import { WorldCanvas } from "./rendering/WorldCanvas";
import { EvolutionPanel } from "./components/EvolutionPanel";
import { ResourcePressurePanel } from "./components/ResourcePressurePanel";

export function App() {
  const [connection, setConnection] = useState<ServerConnection>({
    status: "loading",
  });
  const [enabledEventFamilies, setEnabledEventFamilies] = useState<
    ReadonlySet<OrganismJourneyEventFamily>
  >(() => new Set(DEFAULT_ACTIVITY_FAMILIES));
  const [selectedOrganismId, setSelectedOrganismId] = useState<bigint | null>(null);
  const [reloadRevision, setReloadRevision] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    readActiveWorldState(controller.signal)
      .then(({ cache, evolution }) => setConnection({ status: "online", cache, evolution }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setConnection({
            status: "offline",
            message: error instanceof Error ? error.message : "Unknown error",
          });
        }
      });

    return () => controller.abort();
  }, [reloadRevision]);

  const world = connection.status === "online" ? connection.cache.world : null;
  const visibleOrganisms = useMemo(
    () => world === null ? [] : controlledOrganisms(world),
    [world],
  );
  const activeOrganismId = selectedOrganismId ?? visibleOrganisms[0]?.organismId ?? null;
  const journey = world === null
    ? []
    : world.journeyEvents
        .filter((event) => event.subjectOrganismId === activeOrganismId)
        .slice()
        .reverse();
  const routineActivity = world === null
    ? []
    : world.routineActivitySummaries
        .filter((summary) => summary.subjectOrganismId === activeOrganismId)
        .slice()
        .reverse();

  return (
    <main className="app-shell">
      <header className="masthead">
        <div>
          <p className="eyebrow">A world learning to live</p>
          <h1>LYFE</h1>
        </div>
        <ConnectionBadge connection={connection} />
      </header>

      <section className="world-panel">
        <WorldCanvas
          world={world}
          enabledEventFamilies={enabledEventFamilies}
        />
        <div className="world-copy">
          <p className="section-label">Foundation world</p>
          <h2>Volcanic ocean</h2>
          <p>
            Every visible cell is an authoritative organism. The browser receives
            only the actor-authorized projection produced by the server.
          </p>
          {connection.status === "online" ? (
            <dl>
              <div>
                <dt>Simulation time</dt>
                <dd>{formatSimulationHour(connection.cache.world.simulatedHours)}</dd>
              </div>
              <div>
                <dt>Lifecycle</dt>
                <dd>
                  {connection.cache.world.lifecycle === 1 ? "paused-ready" : "unknown"}
                </dd>
              </div>
              <div>
                <dt>Visible organisms</dt>
                <dd>{countVisibleOrganisms(connection.cache.world).toLocaleString("en-US")}</dd>
              </div>
            </dl>
          ) : null}
          {world !== null ? (
            <>
              <fieldset className="activity-filters">
                <legend>Map activity</legend>
                {ACTIVITY_FAMILIES.map((item) => (
                  <label key={item.family}>
                    <input
                      type="checkbox"
                      checked={enabledEventFamilies.has(item.family)}
                      onChange={() => setEnabledEventFamilies((current) => {
                        const next = new Set(current);
                        if (next.has(item.family)) next.delete(item.family);
                        else next.add(item.family);
                        return next;
                      })}
                    />
                    <span className={`event-key ${item.tone}`}>{item.glyph}</span>
                    {item.label}
                  </label>
                ))}
              </fieldset>
              <section className="journey-panel" aria-labelledby="journey-title">
                <div className="journey-heading">
                  <div>
                    <p className="section-label">Organism history</p>
                    <h3 id="journey-title">Journey</h3>
                  </div>
                  <select
                    aria-label="Organism to inspect"
                    value={activeOrganismId?.toString() ?? ""}
                    onChange={(event) => setSelectedOrganismId(BigInt(event.target.value))}
                  >
                    {visibleOrganisms.map((organism) => (
                      <option key={organism.organismId.toString()} value={organism.organismId.toString()}>
                        #{organism.organismId.toString()}
                      </option>
                    ))}
                  </select>
                </div>
                <ol className="journey-list">
                  {journey.slice(0, 24).map((event) => (
                    <JourneyEntry key={event.eventId.toString()} event={event} />
                  ))}
                </ol>
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
            </>
          ) : null}
        </div>
      </section>
      {world !== null ? (
        <ResourcePressurePanel
          world={world}
          selectedOrganismId={activeOrganismId}
        />
      ) : null}
      {connection.status === "online" ? (
        <EvolutionPanel
          surface={connection.evolution}
          onApplied={() => setReloadRevision((value) => value + 1)}
        />
      ) : null}
    </main>
  );
}

const ACTIVITY_FAMILIES = [
  { family: OrganismJourneyEventFamily.BIRTH, label: "Birth", glyph: "✦", tone: "positive" },
  { family: OrganismJourneyEventFamily.REPRODUCTION, label: "Reproduction", glyph: "+", tone: "positive" },
  { family: OrganismJourneyEventFamily.RESOURCE_ABSORPTION, label: "Absorption", glyph: "↟", tone: "positive" },
  { family: OrganismJourneyEventFamily.FEEDING, label: "Feeding", glyph: "◆", tone: "positive" },
  { family: OrganismJourneyEventFamily.MIGRATION, label: "Migration", glyph: "→", tone: "neutral" },
  { family: OrganismJourneyEventFamily.DEATH, label: "Death", glyph: "×", tone: "negative" },
  { family: OrganismJourneyEventFamily.STRESS, label: "Stress", glyph: "!", tone: "negative" },
  { family: OrganismJourneyEventFamily.BEHAVIOR_TRANSITION, label: "Behavior", glyph: "~", tone: "neutral" },
] as const;

const DEFAULT_ACTIVITY_FAMILIES = [
  OrganismJourneyEventFamily.BIRTH,
  OrganismJourneyEventFamily.REPRODUCTION,
  OrganismJourneyEventFamily.RESOURCE_ABSORPTION,
  OrganismJourneyEventFamily.FEEDING,
  OrganismJourneyEventFamily.DEATH,
] as const;

function controlledOrganisms(world: ActorWorldProjection) {
  return world.tiles
    .flatMap((tile) => tile.detail.case === "live" ? tile.detail.value.organisms : [])
    .filter((organism) => organism.speciesId === world.controlledSpeciesId)
    .sort((left, right) => left.organismId < right.organismId ? -1 : 1);
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

function countVisibleOrganisms(
  world: Extract<ServerConnection, { status: "online" }>["cache"]["world"],
): number {
  return world.tiles.reduce(
    (total, tile) =>
      total + (tile.detail.case === "live" ? tile.detail.value.organisms.length : 0),
    0,
  );
}

function ConnectionBadge({ connection }: { connection: ServerConnection }) {
  if (connection.status === "loading") {
    return <span className="connection pending">Connecting</span>;
  }

  if (connection.status === "offline") {
    return (
      <span className="connection offline" title={connection.message}>
        Server offline
      </span>
    );
  }

  return <span className="connection online">Server connected</span>;
}
