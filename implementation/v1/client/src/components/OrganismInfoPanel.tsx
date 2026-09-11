import {
  OrganismBehavior,
  OrganismLifecyclePhase,
  type OrganismJourneyEvent,
  type OrganismProjection,
  type OrganismRoutineActivitySummary,
} from "../generated/lyfe/v1/projection_pb";
import { OrganismJourneyPanel } from "./ActivityJourneyPanel";

export function OrganismInfoPanel({
  organism,
  tileId,
  journey,
  routineActivity,
  onBack,
}: {
  readonly organism: OrganismProjection;
  readonly tileId: number;
  readonly journey: readonly OrganismJourneyEvent[];
  readonly routineActivity: readonly OrganismRoutineActivitySummary[];
  readonly onBack: () => void;
}) {
  const reserve = organism.chargedReserveCapacityQ > 0n
    ? `${formatRatio(Number(organism.chargedReserveQ * 1_000_000n / organism.chargedReserveCapacityQ))} · ${organism.chargedReserveQ.toLocaleString("en-US")} q`
    : "No charged capacity";
  return (
    <section className="organism-info" aria-labelledby="organism-info-title">
      <button className="panel-back" type="button" onClick={onBack}>← Back</button>
      <header className="organism-info-heading">
        <p className="section-label">Our species · tile {tileId}</p>
        <h2 id="organism-info-title">Organism #{organism.organismId.toString()}</h2>
      </header>
      <dl className="organism-metrics">
        <div><dt>Health</dt><dd>{formatRatio(organism.relativeHealthQ)}</dd></div>
        <div><dt>Energy reserve</dt><dd>{reserve}</dd></div>
        <div><dt>Age</dt><dd>{formatAge(organism.biologicalAgeHours)}</dd></div>
        <div><dt>Life stage</dt><dd>{lifecycleLabel(organism.lifecyclePhase)}</dd></div>
        <div><dt>Behavior</dt><dd>{behaviorLabel(organism.behavior)}</dd></div>
        <div><dt>Resource pressure</dt><dd>{formatRatio(organism.resourcePressureQ)}</dd></div>
        <div><dt>Reproductions</dt><dd>{organism.successfulReproductionCount.toLocaleString("en-US")}</dd></div>
      </dl>
      <OrganismJourneyPanel
        activeOrganismId={organism.organismId}
        journey={journey}
        routineActivity={routineActivity}
      />
    </section>
  );
}

function formatRatio(valueQ: number): string {
  return `${(valueQ / 10_000).toFixed(1)}%`;
}

function formatAge(hours: bigint): string {
  const days = hours / 24n;
  const remainingHours = hours % 24n;
  return days === 0n
    ? `${hours.toLocaleString("en-US")} h`
    : `${days.toLocaleString("en-US")} d ${remainingHours.toLocaleString("en-US")} h`;
}

function lifecycleLabel(phase: OrganismLifecyclePhase): string {
  return phase === OrganismLifecyclePhase.MATURE ? "Mature" : "Unspecified";
}

function behaviorLabel(behavior: OrganismBehavior): string {
  switch (behavior) {
    case OrganismBehavior.BASELINE: return "Baseline";
    case OrganismBehavior.CONSERVING: return "Conserving";
    case OrganismBehavior.FORAGING: return "Foraging";
    case OrganismBehavior.DISPERSING: return "Dispersing";
    case OrganismBehavior.FLEEING: return "Fleeing";
    default: return "Unspecified";
  }
}
