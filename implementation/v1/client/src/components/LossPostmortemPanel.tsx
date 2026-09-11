import {
  GameLossReason,
  GameRunStatus,
  OrganismJourneyEventFamily,
  type ActorWorldProjection,
} from "../generated/lyfe/v1/projection_pb";

export function LossPostmortemPanel({ world }: { readonly world: ActorWorldProjection }) {
  const gameplay = world.gameplay;
  if (gameplay === undefined || gameplay.runStatus !== GameRunStatus.LOST || !gameplay.ended) {
    return null;
  }

  const deaths = world.journeyEvents.filter((event) =>
    event.subjectSpeciesId === world.controlledSpeciesId &&
    event.family === OrganismJourneyEventFamily.DEATH);
  const realized = countDeathEvidence(deaths, true);
  const risks = countDeathEvidence(deaths, false);

  return (
    <section className="loss-postmortem" aria-labelledby="loss-postmortem-title">
      <header>
        <p className="section-label">Completed authoritative ending</p>
        <h2 id="loss-postmortem-title">{lossTitle(gameplay.lossReason)}</h2>
        <p>
          The run ended at completed tick {gameplay.endedTick.toLocaleString("en-US")}, hour{" "}
          {world.simulatedHours.toLocaleString("en-US")}. The evidence below is limited to retained
          controlled-lineage journeys and must not be read as hidden or complete world history.
        </p>
      </header>
      <div className="loss-evidence-grid">
        <section aria-labelledby="realized-deaths-title">
          <h3 id="realized-deaths-title">Recorded triggering causes</h3>
          <DeathEvidenceList values={realized} empty="No retained journey names a triggering cause." />
        </section>
        <section aria-labelledby="recorded-risks-title">
          <h3 id="recorded-risks-title">Recorded non-triggering risks</h3>
          <DeathEvidenceList values={risks} empty="No additional non-triggering risks were retained." />
        </section>
      </div>
      <nav className="loss-actions" aria-label="Ending evidence and save actions">
        <a href="#chronicle-title">Review chronicle evidence</a>
        <a href="#persistence-title">Save or leave this boundary</a>
      </nav>
    </section>
  );
}

function countDeathEvidence(
  deaths: ReadonlyArray<ActorWorldProjection["journeyEvents"][number]>,
  triggered: boolean,
): readonly { readonly cause: number; readonly count: number }[] {
  const counts = new Map<number, number>();
  for (const death of deaths) {
    for (const evidence of death.deathCauseProbabilities) {
      if (evidence.triggered !== triggered || evidence.probabilityQ === 0) continue;
      counts.set(evidence.cause, (counts.get(evidence.cause) ?? 0) + 1);
    }
  }
  return [...counts.entries()]
    .sort(([left], [right]) => left - right)
    .map(([cause, count]) => ({ cause, count }));
}

function DeathEvidenceList({
  values,
  empty,
}: {
  readonly values: readonly { readonly cause: number; readonly count: number }[];
  readonly empty: string;
}) {
  if (values.length === 0) return <p>{empty}</p>;
  return (
    <ul>
      {values.map((value) => (
        <li key={value.cause}>
          <span>{deathCauseName(value.cause)}</span>
          <strong>{value.count.toLocaleString("en-US")} retained {value.count === 1 ? "event" : "events"}</strong>
        </li>
      ))}
    </ul>
  );
}

function lossTitle(reason: GameLossReason): string {
  if (reason === GameLossReason.ALL_LIFE_EXTINCT) return "No living organisms remain";
  if (reason === GameLossReason.CONTROLLED_SPECIES_EXTINCT) return "The controlled lineage has ended";
  return "This run has ended";
}

function deathCauseName(value: number): string {
  switch (value) {
    case 1: return "Reserve exhaustion";
    case 2: return "Structural failure";
    case 3: return "Senescence";
    case 4: return "Temperature exposure";
    case 5: return "Hydrogen sulfide exposure";
    case 6: return "Sulfur dioxide exposure";
    case 7: return "Maintenance failure";
    default: return `Recorded cause ${value}`;
  }
}
