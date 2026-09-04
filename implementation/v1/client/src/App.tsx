import { useEffect, useState } from "react";
import {
  formatSimulationHour,
  readActiveWorldProjection,
  type ServerConnection,
} from "./api/server";
import { WorldCanvas } from "./rendering/WorldCanvas";

export function App() {
  const [connection, setConnection] = useState<ServerConnection>({
    status: "loading",
  });

  useEffect(() => {
    const controller = new AbortController();

    readActiveWorldProjection(controller.signal)
      .then((cache) => setConnection({ status: "online", cache }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setConnection({
            status: "offline",
            message: error instanceof Error ? error.message : "Unknown error",
          });
        }
      });

    return () => controller.abort();
  }, []);

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
          world={connection.status === "online" ? connection.cache.world : null}
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
        </div>
      </section>
    </main>
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
