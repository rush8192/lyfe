import {
  SimulationSpeedPreset,
  WorldControlAction,
  type WorldControlState,
} from "../generated/lyfe/v1/control_pb";
import { GameRunStatus, WorldLifecycle } from "../generated/lyfe/v1/projection_pb";

export interface WorldTimeControlsProps {
  readonly control: WorldControlState;
  readonly simulationTimeLabel: string;
  readonly tickDurationHours: number;
  readonly visibleOrganismCount: number;
  readonly showDebug?: boolean;
  readonly busy: boolean;
  readonly error: string | null;
  readonly onCommand: (
    action: WorldControlAction,
    speed?: SimulationSpeedPreset,
  ) => void;
}

export function WorldTimeControls({
  control,
  simulationTimeLabel,
  tickDurationHours,
  visibleOrganismCount,
  showDebug = false,
  busy,
  error,
  onCommand,
}: WorldTimeControlsProps) {
  const running = control.lifecycle === WorldLifecycle.RUNNING;
  const ended = control.gameRunStatus === GameRunStatus.LOST;

  return (
    <section className="time-controls" aria-labelledby="time-controls-title" aria-busy={busy}>
      <div className="time-controls-heading">
        <div>
          <p className="section-label">Simulation time</p>
          <h2 id="time-controls-title">{simulationTimeLabel}</h2>
        </div>
        <button
          className={running ? "time-toggle running" : "time-toggle"}
          type="button"
          disabled={busy || ended}
          onClick={() => onCommand(
            running ? WorldControlAction.PAUSE : WorldControlAction.RESUME,
          )}
        >
          {running ? "Pause time" : "Resume time"}
        </button>
      </div>
      <div className="time-control-actions">
        <button
          type="button"
          disabled={busy || running || ended}
          onClick={() => onCommand(WorldControlAction.STEP)}
        >
          Step {tickDurationHours} {tickDurationHours === 1 ? "hour" : "hours"}
        </button>
      </div>
      <label className="speed-control">
        <span>Speed</span>
        <select
          aria-label="Simulation speed"
          value={control.speed}
          disabled={busy || ended}
          onChange={(event) => onCommand(
            WorldControlAction.SET_SPEED,
            Number(event.target.value) as SimulationSpeedPreset,
          )}
        >
          <option value={SimulationSpeedPreset.SLOW}>Slow</option>
          <option value={SimulationSpeedPreset.NORMAL}>Normal</option>
          <option value={SimulationSpeedPreset.FAST}>Fast</option>
        </select>
      </label>
      {showDebug ? (
        <details className="debug-ui-details">
          <summary>Debug details</summary>
          <dl>
            <div><dt>Lifecycle</dt><dd>{running ? "running" : "paused-ready"}</dd></div>
            <div><dt>Visible projection</dt><dd>{visibleOrganismCount.toLocaleString("en-US")} organisms</dd></div>
            <div><dt>Tick duration</dt><dd>{tickDurationHours.toLocaleString("en-US")} h</dd></div>
            <div><dt>Target cadence</dt><dd>{control.targetIntervalMs.toLocaleString("en-US")} ms</dd></div>
            <div><dt>Control revision</dt><dd>{control.controlRevision.toLocaleString("en-US")}</dd></div>
          </dl>
        </details>
      ) : null}
      {ended ? <p className="clock-error">This world has reached its ending.</p> : null}
      {error === null ? null : <p className="clock-error" role="alert">{error}</p>}
    </section>
  );
}
