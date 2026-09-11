import type {
  SavedWorldSummary,
  WorldCatalogue,
} from "../generated/lyfe/v1/persistence_pb";
import { formatSimulationHour } from "../api/server";

export interface WorldPersistencePanelProps {
  readonly catalogue: WorldCatalogue;
  readonly activeWorldRunning: boolean;
  readonly busy: boolean;
  readonly error: string | null;
  readonly onSave: () => void;
  readonly onLoad: (world: SavedWorldSummary, confirmDiscardUnsaved: boolean) => void;
  readonly onUnload: (confirmDiscardUnsaved: boolean) => void;
}

export function WorldPersistencePanel({
  catalogue,
  activeWorldRunning,
  busy,
  error,
  onSave,
  onLoad,
  onUnload,
}: WorldPersistencePanelProps) {
  function confirmDiscard(): boolean {
    return !catalogue.activeHasUnsavedChanges || window.confirm(
      "This world has changes that are not saved. Discard them?",
    );
  }

  return (
    <section className="persistence-panel" aria-labelledby="persistence-title" aria-busy={busy}>
      <div className="persistence-heading">
        <div>
          <p className="section-label">World archive</p>
          <h2 id="persistence-title">Keep a boundary. Return to it later.</h2>
        </div>
        {catalogue.hasActiveWorld ? (
          <span className={catalogue.activeHasUnsavedChanges
            ? "save-state unsaved"
            : "save-state"}
          >
            {catalogue.activeHasUnsavedChanges ? "Unsaved changes" : "Saved exactly"}
          </span>
        ) : null}
      </div>
      <p className="persistence-intro">
        Saves capture one completed authoritative boundary. Loading restores that exact hour,
        population, history, and random continuation.
      </p>
      {catalogue.hasActiveWorld ? (
        <div className="persistence-actions">
          <button type="button" disabled={busy} onClick={onSave}>Save current boundary</button>
          <button
            type="button"
            className="secondary-action"
            disabled={busy || activeWorldRunning}
            onClick={() => {
              if (confirmDiscard()) onUnload(catalogue.activeHasUnsavedChanges);
            }}
          >
            Return to world setup
          </button>
        </div>
      ) : null}
      {activeWorldRunning ? (
        <p className="persistence-boundary-note">Pause before loading another world or returning to setup.</p>
      ) : null}
      {catalogue.savedWorlds.length === 0 ? (
        <p className="empty-archive">No saved worlds yet.</p>
      ) : (
        <ol className="saved-world-list">
          {catalogue.savedWorlds.map((world) => (
            <li key={world.worldId.toString()}>
              <div>
                <strong>World {world.worldId.toLocaleString("en-US")}</strong>
                <span>
                  {formatSimulationHour(world.simulatedHours)} · {world.organismCount.toLocaleString("en-US")} organisms
                </span>
                <small>
                  Boundary {world.worldRevision.toLocaleString("en-US")} · saved {formatSavedAt(world.savedAtUnixMilliseconds)}
                </small>
                <small className="saved-world-seed">Seed {world.rootSeedHex}</small>
              </div>
              <button
                type="button"
                disabled={busy || activeWorldRunning || world.isActiveSavedBoundary}
                onClick={() => {
                  if (confirmDiscard()) onLoad(world, catalogue.activeHasUnsavedChanges);
                }}
              >
                {world.isActiveSavedBoundary ? "Loaded" : world.isActive ? "Restore save" : "Load world"}
              </button>
            </li>
          ))}
        </ol>
      )}
      {error === null ? null : <p className="persistence-error" role="alert">{error}</p>}
    </section>
  );
}

function formatSavedAt(value: bigint): string {
  const milliseconds = Number(value);
  if (!Number.isSafeInteger(milliseconds)) return "at an unknown time";
  const date = new Date(milliseconds);
  if (Number.isNaN(date.valueOf())) return "at an unknown time";
  return date.toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}
