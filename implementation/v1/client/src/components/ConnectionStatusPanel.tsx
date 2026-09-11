import { formatSimulationHour } from "../api/server";
import type { ActorWorldProjection } from "../generated/lyfe/v1/projection_pb";

export type ClientSyncState =
  | { readonly status: "current" }
  | { readonly status: "recovering"; readonly message: string }
  | { readonly status: "resyncing"; readonly message: string };

interface ConnectionStatusPanelProps {
  readonly phase: "loading" | "offline" | ClientSyncState["status"];
  readonly message?: string;
  readonly world?: ActorWorldProjection;
  readonly onRetry: () => void;
}

export function ConnectionStatusPanel({
  phase,
  message,
  world,
  onRetry,
}: ConnectionStatusPanelProps) {
  if (phase === "current") return null;

  if (phase === "loading") {
    return (
      <section className="connection-panel loading" role="status" aria-live="polite">
        <span className="connection-spinner" aria-hidden="true" />
        <div>
          <strong>Connecting to the world host</strong>
          <p>No simulation state is shown until one complete authoritative boundary arrives.</p>
        </div>
      </section>
    );
  }

  if (phase === "offline" || world === undefined) {
    return (
      <section className="connection-panel offline" role="alert">
        <div>
          <strong>World host unavailable</strong>
          <p>{message ?? "No authoritative world state could be loaded."}</p>
        </div>
        <button type="button" onClick={onRetry}>Retry connection</button>
      </section>
    );
  }

  const retrying = phase === "resyncing";
  return (
    <section className={`connection-panel ${phase}`} role="alert">
      <div>
        <strong>{retrying ? "Resynchronizing the world view" : "Connection interrupted — view held"}</strong>
        <p>
          Showing the last complete boundary: tick {world.completedTick.toLocaleString("en-US")},
          {" "}{formatSimulationHour(world.simulatedHours).toLowerCase()}. Commands are locked until
          a fresh complete boundary is verified.
        </p>
        {message === undefined ? null : <small>{message}</small>}
      </div>
      <button type="button" disabled={retrying} onClick={onRetry}>
        {retrying ? "Checking…" : "Retry now"}
      </button>
    </section>
  );
}
