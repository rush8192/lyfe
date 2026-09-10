import {
  AttentionAlertClass,
  AttentionAlertKind,
  NotableEventFamily,
  type ActorWorldProjection,
  type AttentionAlert,
} from "../generated/lyfe/v1/projection_pb";

export function AttentionInbox({ world }: { readonly world: ActorWorldProjection }) {
  if (world.attentionAlerts.length === 0) return null;
  const alerts = world.attentionAlerts.slice().reverse();
  return (
    <section className="attention-panel" aria-labelledby="attention-title">
      <header>
        <p className="section-label">Completed-boundary notifications</p>
        <h2 id="attention-title">Attention inbox</h2>
        <p>
          These notifications report saved evidence. This inbox does not pause the simulation or
          predict what will happen next.
        </p>
      </header>
      <ol className="attention-list">
        {alerts.map((alert) => <AttentionEntry key={alert.alertId.toString()} alert={alert} />)}
      </ol>
    </section>
  );
}

function AttentionEntry({ alert }: { readonly alert: AttentionAlert }) {
  return (
    <li className={`attention-entry attention-${className(alert.alertClass)}`}>
      <div className="attention-heading">
        <span>{classLabel(alert.alertClass)}</span>
        <small>
          Hour {alert.simulatedHours.toLocaleString("en-US")} · tick{" "}
          {alert.completedTick.toLocaleString("en-US")}
        </small>
      </div>
      <strong>{alertTitle(alert)}</strong>
      <p>{alertDetail(alert)}</p>
      <div className="attention-evidence" aria-label="Alert evidence">
        {alert.chronicleEventIds.map((eventId) => (
          <a key={eventId.toString()} href={`#chronicle-${eventId.toString()}`}>
            Chronicle event #{eventId.toLocaleString("en-US")}
          </a>
        ))}
      </div>
    </li>
  );
}

function alertTitle(alert: AttentionAlert): string {
  if (alert.kind === AttentionAlertKind.LINEAGE_REVIEW_BOUNDARY) {
    return `Species ${alert.speciesId} review boundary completed`;
  }
  switch (alert.eventFamily) {
    case NotableEventFamily.SPECIATION:
      return `Species ${alert.speciesId} branched`;
    case NotableEventFamily.FIRST_REPRODUCTION:
      return `Species ${alert.speciesId} first reproduced`;
    case NotableEventFamily.POPULATION_MILESTONE:
      return `Species ${alert.speciesId} crossed a population milestone`;
    case NotableEventFamily.FIRST_TILE_OCCUPATION:
      return `Species ${alert.speciesId} occupied new ground`;
    case NotableEventFamily.FIRST_REACTION_EXECUTION:
      return `Species ${alert.speciesId} activated new chemistry`;
    case NotableEventFamily.SPECIES_EXTINCTION:
      return `Species ${alert.speciesId} became extinct`;
    case NotableEventFamily.POPULATION_DANGER_THRESHOLD:
      return `Species ${alert.speciesId} entered the low-population danger band`;
    case NotableEventFamily.POPULATION_DECLINE_THRESHOLD:
      return `Species ${alert.speciesId} declined sharply over 24 hours`;
    case NotableEventFamily.SUSTAINED_LOW_HEALTH:
      return `Species ${alert.speciesId} has sustained low health`;
    case NotableEventFamily.REALIZED_DEATH_MECHANISM:
      return `Species ${alert.speciesId} encountered a new realized death mechanism`;
    case NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE:
      return `Species ${alert.speciesId} is under sustained resource pressure`;
    default:
      return `Species ${alert.speciesId} changed`;
  }
}

function alertDetail(alert: AttentionAlert): string {
  if (alert.eventFamily === NotableEventFamily.POPULATION_DANGER_THRESHOLD) {
    return "Population crossed from above ten to ten or fewer. The alert rearms only after recovery above fifteen.";
  }
  if (alert.eventFamily === NotableEventFamily.POPULATION_DECLINE_THRESHOLD) {
    return "Population fell at least 25% across the saved trailing 24-hour window. The alert rearms after the trailing decline recovers below 10%.";
  }
  if (alert.eventFamily === NotableEventFamily.SUSTAINED_LOW_HEALTH) {
    return "Average health remained below 25% for six simulated hours. The alert rearms only after health stays above 35% for six hours.";
  }
  if (alert.eventFamily === NotableEventFamily.REALIZED_DEATH_MECHANISM) {
    return "A retained organism journey recorded this death cause for the species for the first time. This is realized evidence, not a prediction of extinction.";
  }
  if (alert.eventFamily === NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE) {
    return "Average composite resource pressure remained at or above 75% for six simulated hours. The alert rearms only after pressure stays below 55% for six hours.";
  }
  if (alert.chronicleEventIds.length > 1) {
    return `${alert.chronicleEventIds.length.toLocaleString("en-US")} related facts were coalesced at this completed boundary.`;
  }
  return "Open the linked factual record for its saved evidence.";
}

function classLabel(value: AttentionAlertClass): string {
  if (value === AttentionAlertClass.CRITICAL) return "Critical";
  if (value === AttentionAlertClass.STRATEGIC) return "Strategic";
  return "Informational";
}

function className(value: AttentionAlertClass): string {
  if (value === AttentionAlertClass.CRITICAL) return "critical";
  if (value === AttentionAlertClass.STRATEGIC) return "strategic";
  return "informational";
}
