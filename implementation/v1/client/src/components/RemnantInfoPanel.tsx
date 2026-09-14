import type {
  OrganismJourneyEvent,
  RemnantProjection,
  ResourceDefinition,
} from "../generated/lyfe/v1/projection_pb";
import { deathCauseCopy } from "./deathCauses";

export function RemnantInfoPanel({
  remnant,
  tileId,
  completedTick,
  tickDurationHours,
  deathEvent,
  resourceDefinitions,
  onBack,
}: {
  readonly remnant: RemnantProjection;
  readonly tileId: number;
  readonly completedTick: bigint;
  readonly tickDurationHours: number;
  readonly deathEvent?: OrganismJourneyEvent;
  readonly resourceDefinitions: readonly ResourceDefinition[];
  readonly onBack: () => void;
}) {
  const ageHours = completedTick >= remnant.createdTick
    ? (completedTick - remnant.createdTick) * BigInt(tickDurationHours)
    : 0n;
  const actualCauseId = deathEvent?.detailId ||
    deathEvent?.deathCauseProbabilities.find((cause) => cause.triggered)?.cause;
  const actualCause = actualCauseId === undefined || actualCauseId === 0
    ? null
    : deathCauseCopy(actualCauseId);
  const actualProbability = deathEvent?.deathCauseProbabilities.find((cause) =>
    cause.cause === actualCauseId)?.probabilityQ;
  const otherCauses = deathEvent?.deathCauseProbabilities.filter((cause) =>
    cause.cause !== actualCauseId && cause.probabilityQ > 0) ?? [];
  const resourceNames = new Map(resourceDefinitions.map((resource) =>
    [resource.resourceId, resource.displayName]));
  const recyclables = remnant.micronutrients.filter((stock) => stock.quantityQ > 0n);

  return (
    <section className="organism-info remnant-info" aria-labelledby="remnant-info-title">
      <button className="panel-back" type="button" onClick={onBack}>← Back</button>
      <header className="organism-info-heading">
        <p className="section-label">Remnant · tile {tileId}</p>
        <h2 id="remnant-info-title">Remnant #{remnant.remnantId.toString()}</h2>
      </header>
      <dl className="organism-metrics">
        <div><dt>Time since death</dt><dd>{formatAge(ageHours)}</dd></div>
        <div><dt>Death boundary</dt><dd>Tick {remnant.createdTick.toLocaleString("en-US")}</dd></div>
        <div><dt>Remaining structure</dt><dd>{remnant.structuralMatterQ.toLocaleString("en-US")} q</dd></div>
        <div><dt>Remaining reserve</dt><dd>{remnant.chargedReserveQ.toLocaleString("en-US")} q</dd></div>
        <div className="remnant-source"><dt>Source</dt><dd>Organism #{remnant.sourceOrganismId.toString()} · species #{remnant.sourceSpeciesId.toString()}</dd></div>
      </dl>

      <section className="remnant-death-evidence" aria-labelledby="remnant-death-title">
        <p className="section-label">Retained death evidence</p>
        <h3 id="remnant-death-title">Why it died</h3>
        {actualCause === null ? (
          <p className="remnant-evidence-unavailable">
            No authorized cause-of-death record is retained for this remnant.
          </p>
        ) : (
          <div className="remnant-actual-cause">
            <strong>{actualCause.label}</strong>
            <span>{actualCause.explanation}</span>
            {actualProbability === undefined ? null : (
              <small>Recorded probability in that tick: {formatProbability(actualProbability)}</small>
            )}
          </div>
        )}
        {actualCause === null ? null : (
          <>
            <h4>Other eligible causes at death</h4>
            {otherCauses.length === 0 ? (
              <p className="remnant-evidence-unavailable">No other nonzero cause was recorded.</p>
            ) : (
              <ul className="mortality-causes remnant-cause-list">
                {otherCauses.map((cause) => {
                  const copy = deathCauseCopy(cause.cause);
                  return (
                    <li key={cause.cause}>
                      <span>
                        <strong>{copy.label}</strong>
                        <small>
                          {copy.explanation}{cause.triggered ? " This risk also triggered in the same assessment." : ""}
                        </small>
                      </span>
                      <b>{formatProbability(cause.probabilityQ)}</b>
                    </li>
                  );
                })}
              </ul>
            )}
          </>
        )}
      </section>

      {recyclables.length === 0 ? null : (
        <section className="remnant-recyclables" aria-labelledby="remnant-recyclables-title">
          <p className="section-label">Still bound in the remnant</p>
          <h3 id="remnant-recyclables-title">Recyclable micronutrients</h3>
          <ul>
            {recyclables.map((stock) => (
              <li key={stock.resourceId}>
                <span>{resourceNames.get(stock.resourceId) ?? `Resource ${stock.resourceId}`}</span>
                <strong>{stock.quantityQ.toLocaleString("en-US")} q</strong>
              </li>
            ))}
          </ul>
        </section>
      )}
    </section>
  );
}

function formatAge(hours: bigint): string {
  const days = hours / 24n;
  const remainingHours = hours % 24n;
  return days === 0n
    ? `${hours.toLocaleString("en-US")} h`
    : `${days.toLocaleString("en-US")} d ${remainingHours.toLocaleString("en-US")} h`;
}

function formatProbability(probabilityQ: number): string {
  return `${(probabilityQ / 10_000).toFixed(1)}%`;
}
