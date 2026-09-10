import {
  LineageReviewEvidenceKind,
  LineageReviewEvidenceReferenceKind,
  LineageReviewCapabilityKind,
  LineageReviewLandmarkKind,
  NotableEventFamily,
  NotableEventSignificance,
  OrganismBehavior,
  SpeciesPopulationScope,
  type ActorWorldProjection,
  type LineageReviewLandmark,
  type LineageReviewEvidenceReference,
  type LineageReviewObservation,
  type NotableEvent,
} from "../generated/lyfe/v1/projection_pb";
import type { EvolutionDecisionSurface } from "../generated/lyfe/v1/evolution_pb";
import { MAX_CHRONICLE_NOTE_LENGTH } from "../state/chronicleNotes";

interface ChroniclePanelProps {
  readonly world: ActorWorldProjection;
  readonly evolution: EvolutionDecisionSurface;
  readonly notes: ReadonlyMap<bigint, string>;
  readonly onNoteChange: (eventId: bigint, text: string) => void;
  readonly onNavigateResourceTile: (tileId: number) => void;
}

export function ChroniclePanel({
  world,
  evolution,
  notes,
  onNoteChange,
  onNavigateResourceTile,
}: ChroniclePanelProps) {
  const traitNames = new Map(evolution.traits.map((trait) => [trait.traitId, trait.displayName]));
  const reactionNames = new Map(world.reactionDefinitions.map((reaction) =>
    [reaction.reactionId, reaction.displayName]));
  const entries = [
    ...world.lineageReviewLandmarks.map((value) => ({ kind: "lineage" as const, value })),
    ...world.notableEvents.map((value) => ({ kind: "notable" as const, value })),
  ].sort((left, right) => {
    if (left.value.completedTick !== right.value.completedTick) {
      return left.value.completedTick < right.value.completedTick ? 1 : -1;
    }
    return left.value.eventId < right.value.eventId ? 1 : -1;
  });
  const liveTileIds = new Set(world.tiles
    .filter((tile) => tile.detail.case === "live")
    .map((tile) => tile.tileId));
  if (entries.length === 0) return null;

  return (
    <section className="chronicle-panel" aria-labelledby="chronicle-title">
      <header>
        <p className="section-label">Authoritative completed-boundary record</p>
        <h2 id="chronicle-title">Lineage chronicle</h2>
        <p>
          These are saved simulation facts. They report what changed without scoring the branch
          as a success or failure.
        </p>
      </header>
      <ol className="chronicle-list">
        {entries.map((entry) => entry.kind === "lineage" ? (
          <ChronicleEntry
            key={`lineage-${entry.value.eventId.toString()}`}
            landmark={entry.value}
            traitNames={traitNames}
            reactionNames={reactionNames}
            note={notes.get(entry.value.eventId) ?? ""}
            onNoteChange={onNoteChange}
            liveTileIds={liveTileIds}
            controlledSpeciesId={world.controlledSpeciesId}
            onNavigateResourceTile={onNavigateResourceTile}
          />
        ) : (
          <NotableChronicleEntry
            key={`notable-${entry.value.eventId.toString()}`}
            event={entry.value}
            reactionNames={reactionNames}
            note={notes.get(entry.value.eventId) ?? ""}
            onNoteChange={onNoteChange}
            liveTileIds={liveTileIds}
            onNavigateResourceTile={onNavigateResourceTile}
          />
        ))}
      </ol>
    </section>
  );
}

function NotableChronicleEntry({
  event,
  reactionNames,
  note,
  onNoteChange,
  liveTileIds,
  onNavigateResourceTile,
}: {
  readonly event: NotableEvent;
  readonly reactionNames: ReadonlyMap<number, string>;
  readonly note: string;
  readonly onNoteChange: (eventId: bigint, text: string) => void;
  readonly liveTileIds: ReadonlySet<number>;
  readonly onNavigateResourceTile: (tileId: number) => void;
}) {
  const reactionName = event.reactionId === undefined
    ? undefined
    : reactionNames.get(event.reactionId) ?? `Reaction ${event.reactionId}`;
  const title = notableTitle(event, reactionName);
  const canOpenTile = event.tileId !== undefined && liveTileIds.has(event.tileId);
  return (
    <li
      id={`chronicle-${event.eventId.toString()}`}
      className="chronicle-entry chronicle-notable-entry"
    >
      <div className="chronicle-entry-heading">
        <div>
          <p className="section-label">World event · {significanceLabel(event.significance)}</p>
          <h3>{title}</h3>
        </div>
        <span>Rule v{event.significanceRuleVersion}</span>
      </div>
      <p className="chronicle-meta">
        Recorded at hour {event.simulatedHours.toLocaleString("en-US")} · completed tick{" "}
        {event.completedTick.toLocaleString("en-US")} · chronicle event #{event.eventId.toLocaleString("en-US")}
      </p>
      <p className="chronicle-focus">
        <strong>Evidence:</strong> {notableEvidence(event, reactionName)}
      </p>
      {event.tileId !== undefined ? (
        <button
          type="button"
          className="chronicle-evidence-link"
          disabled={!canOpenTile}
          onClick={() => onNavigateResourceTile(event.tileId!)}
        >
          {canOpenTile ? `Open live tile ${event.tileId}` : `Tile ${event.tileId} is not currently live`}
        </button>
      ) : null}
      <PrivateHypothesis
        eventId={event.eventId}
        note={note}
        onNoteChange={onNoteChange}
      />
    </li>
  );
}

function notableTitle(event: NotableEvent, reactionName?: string): string {
  switch (event.family) {
    case NotableEventFamily.SPECIATION:
      return `Species ${event.speciesId} branched from species ${event.relatedSpeciesId ?? "?"}`;
    case NotableEventFamily.FIRST_REPRODUCTION:
      return `Species ${event.speciesId} reproduced`;
    case NotableEventFamily.POPULATION_MILESTONE:
      return `Species ${event.speciesId} reached population ${event.milestoneValue.toLocaleString("en-US")}`;
    case NotableEventFamily.FIRST_TILE_OCCUPATION:
      return `Species ${event.speciesId} first occupied tile ${event.tileId ?? "?"}`;
    case NotableEventFamily.FIRST_REACTION_EXECUTION:
      return `Species ${event.speciesId} first ran ${reactionName ?? "a reaction"}`;
    case NotableEventFamily.SPECIES_EXTINCTION:
      return `Species ${event.speciesId} became extinct`;
    case NotableEventFamily.POPULATION_DANGER_THRESHOLD:
      return `Species ${event.speciesId} entered the low-population danger band`;
    case NotableEventFamily.POPULATION_DECLINE_THRESHOLD:
      return `Species ${event.speciesId} declined over 24 hours`;
    case NotableEventFamily.SUSTAINED_LOW_HEALTH:
      return `Species ${event.speciesId} remained in the low-health band`;
    case NotableEventFamily.REALIZED_DEATH_MECHANISM:
      return `Species ${event.speciesId} recorded its first ${deathCauseName(event.milestoneValue)} death`;
    case NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE:
      return `Species ${event.speciesId} remained under high resource pressure`;
    default:
      return `Species ${event.speciesId} changed`;
  }
}

function notableEvidence(event: NotableEvent, reactionName?: string): string {
  switch (event.family) {
    case NotableEventFamily.SPECIATION:
      return `${event.milestoneValue.toLocaleString("en-US")} founders in speciation decision #${event.sourceEventId.toLocaleString("en-US")}.`;
    case NotableEventFamily.FIRST_REPRODUCTION:
      return `First retained reproduction journey event #${event.sourceEventId.toLocaleString("en-US")} on tile ${event.tileId}.`;
    case NotableEventFamily.POPULATION_MILESTONE:
      return `World-exact population crossed the fixed ${event.milestoneValue.toLocaleString("en-US")} threshold.`;
    case NotableEventFamily.FIRST_TILE_OCCUPATION:
      return `First retained occupation evidence for tile ${event.tileId}; source event #${event.sourceEventId.toLocaleString("en-US")}.`;
    case NotableEventFamily.FIRST_REACTION_EXECUTION:
      return `The completed ledger first recorded ${reactionName ?? "this reaction"} for this species on tile ${event.tileId}.`;
    case NotableEventFamily.SPECIES_EXTINCTION:
      return `World-exact population fell from ${event.milestoneValue.toLocaleString("en-US")} to zero.`;
    case NotableEventFamily.POPULATION_DANGER_THRESHOLD:
      return `World-exact population crossed from above ten to ${event.milestoneValue.toLocaleString("en-US")}. Rule v1 rearms only after recovery above fifteen.`;
    case NotableEventFamily.POPULATION_DECLINE_THRESHOLD:
      return `World-exact population fell from ${event.baselineValue.toLocaleString("en-US")} to ${event.milestoneValue.toLocaleString("en-US")} across the saved trailing 24-hour window (${declinePercent(event.baselineValue, event.milestoneValue)} decline). Rule v1 rearms after the trailing decline recovers below 10%.`;
    case NotableEventFamily.SUSTAINED_LOW_HEALTH:
      return `Average health was ${fixedPointPercent(event.milestoneValue)} after ${event.baselineValue.toLocaleString("en-US")} consecutive simulated hours below 25%. Rule v1 rearms only after six consecutive hours above 35%.`;
    case NotableEventFamily.REALIZED_DEATH_MECHANISM:
      return `The retained journey first attributes a death in this species to ${deathCauseName(event.milestoneValue)} on tile ${event.tileId}; source journey event #${event.sourceEventId.toLocaleString("en-US")}. This records a realized cause, not a forecast of further deaths.`;
    case NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE:
      return `Average composite resource pressure was ${fixedPointPercent(event.milestoneValue)} after ${event.baselineValue.toLocaleString("en-US")} consecutive simulated hours at or above 75%. Rule v1 rearms only after six consecutive hours below 55%.`;
    default:
      return "Saved simulation evidence.";
  }
}

function deathCauseName(value: bigint): string {
  switch (value) {
    case 1n: return "reserve exhaustion";
    case 2n: return "structural failure";
    case 3n: return "senescence";
    case 4n: return "temperature exposure";
    case 5n: return "hydrogen sulfide exposure";
    case 6n: return "sulfur dioxide exposure";
    case 7n: return "maintenance failure";
    default: return "recorded-cause";
  }
}

function declinePercent(baseline: bigint, current: bigint): string {
  if (baseline === 0n || current >= baseline) return "0.0%";
  const tenths = Number(((baseline - current) * 1_000n) / baseline);
  return `${(tenths / 10).toFixed(1)}%`;
}

function fixedPointPercent(valueQ: bigint): string {
  const tenths = Number(valueQ / 1_000n);
  return `${(tenths / 10).toFixed(1)}%`;
}

function significanceLabel(significance: NotableEventSignificance): string {
  if (significance === NotableEventSignificance.CRITICAL) return "Critical";
  if (significance === NotableEventSignificance.STRATEGIC) return "Strategic";
  return "Informational";
}

function PrivateHypothesis({
  eventId,
  note,
  onNoteChange,
}: {
  readonly eventId: bigint;
  readonly note: string;
  readonly onNoteChange: (eventId: bigint, text: string) => void;
}) {
  return (
    <div className="chronicle-hypothesis">
      <label htmlFor={`chronicle-note-${eventId.toString()}`}>Private hypothesis</label>
      <p>
        Your interpretation, stored only in this browser. It is not a simulation fact and does
        not affect this world.
      </p>
      <textarea
        id={`chronicle-note-${eventId.toString()}`}
        value={note}
        maxLength={MAX_CHRONICLE_NOTE_LENGTH}
        rows={3}
        placeholder="What might explain this change?"
        onChange={(change) => onNoteChange(eventId, change.target.value)}
      />
      <small>{note.length.toLocaleString("en-US")} / {MAX_CHRONICLE_NOTE_LENGTH.toLocaleString("en-US")}</small>
    </div>
  );
}

function ChronicleEntry({
  landmark,
  traitNames,
  reactionNames,
  note,
  onNoteChange,
  liveTileIds,
  controlledSpeciesId,
  onNavigateResourceTile,
}: {
  readonly landmark: LineageReviewLandmark;
  readonly traitNames: ReadonlyMap<number, string>;
  readonly reactionNames: ReadonlyMap<number, string>;
  readonly note: string;
  readonly onNoteChange: (eventId: bigint, text: string) => void;
  readonly liveTileIds: ReadonlySet<number>;
  readonly controlledSpeciesId: bigint;
  readonly onNavigateResourceTile: (tileId: number) => void;
}) {
  const isFollowUp = landmark.kind === LineageReviewLandmarkKind.PROPOSAL_FOLLOW_UP;
  const traits = landmark.traitIds.map((id) => traitNames.get(id) ?? `Trait ${id}`);
  return (
    <li id={`chronicle-${landmark.eventId.toString()}`} className="chronicle-entry">
      <div className="chronicle-entry-heading">
        <div>
          <p className="section-label">
            {isFollowUp ? "Proposal-specific follow-up landmark" : "Cooldown-boundary summary"}
          </p>
          <h3>{traits.join(" + ") || "Lineage branch"}</h3>
        </div>
        <span>{formatWindow(landmark.windowHours)}</span>
      </div>
      <p
        id={`chronicle-${landmark.eventId.toString()}-decision`}
        className="chronicle-meta"
      >
        Recorded at hour {landmark.simulatedHours.toLocaleString("en-US")} · completed tick{" "}
        {landmark.completedTick.toLocaleString("en-US")} · lineage event #
        {landmark.speciationEventId.toLocaleString("en-US")}
      </p>
      <p className="chronicle-focus">
        <strong>Evidence focus:</strong> {evidenceLabel(landmark.evidenceKind)}
      </p>
      <div className="chronicle-branches">
        <ObservationCard
          id={`chronicle-${landmark.eventId.toString()}-reviewed`}
          title={`Reviewed branch · species ${landmark.perspectiveSpeciesId.toString()}`}
          baseline={landmark.perspectiveBaseline}
          current={landmark.perspectiveCurrent}
        />
        <ObservationCard
          id={`chronicle-${landmark.eventId.toString()}-comparison`}
          title={`Comparison branch · species ${landmark.comparisonCurrent?.speciesId.toString() ?? "?"}`}
          baseline={landmark.comparisonBaseline}
          current={landmark.comparisonCurrent}
        />
      </div>
      <ActivationEvidence
        landmark={landmark}
        traitNames={traitNames}
        reactionNames={reactionNames}
      />
      {isFollowUp ? (
        <small className="chronicle-note">
          This longer observation did not extend the 168-hour mechanical cooldown.
        </small>
      ) : null}
      <EvidenceNavigation
        landmark={landmark}
        references={landmark.evidenceReferences}
        liveTileIds={liveTileIds}
        controlledSpeciesId={controlledSpeciesId}
        onNavigateResourceTile={onNavigateResourceTile}
      />
      <div className="chronicle-hypothesis">
        <label htmlFor={`chronicle-note-${landmark.eventId.toString()}`}>
          Private hypothesis
        </label>
        <p>
          Your interpretation, stored only in this browser. It is not a simulation fact and does
          not affect this world.
        </p>
        <textarea
          id={`chronicle-note-${landmark.eventId.toString()}`}
          value={note}
          maxLength={MAX_CHRONICLE_NOTE_LENGTH}
          rows={3}
          placeholder="What might explain this change?"
          onChange={(event) => onNoteChange(landmark.eventId, event.target.value)}
        />
        <small>{note.length.toLocaleString("en-US")} / {MAX_CHRONICLE_NOTE_LENGTH.toLocaleString("en-US")}</small>
      </div>
    </li>
  );
}

function ActivationEvidence({
  landmark,
  traitNames,
  reactionNames,
}: {
  readonly landmark: LineageReviewLandmark;
  readonly traitNames: ReadonlyMap<number, string>;
  readonly reactionNames: ReadonlyMap<number, string>;
}) {
  const introducedReactions = landmark.reactionActivations.filter((reaction) =>
    reaction.introducedByProposal);
  return (
    <section className="chronicle-activation" aria-label="Capability activation evidence">
      <h4>Capability activation</h4>
      <p>
        World-exact counters for the reviewed branch from the decision through this boundary.
        Zero means no recorded activation—not that the capability lacked value.
      </p>
      {landmark.capabilityActivations.length > 0 ? (
        <ul>
          {landmark.capabilityActivations.map((capability) => (
            <li key={capability.kind}>
              <strong>{capabilityLabel(capability.kind)}</strong>
              <span>{capability.installed ? "Installed" : "Not installed"}</span>
              <small>
                {capability.introducedByProposal ? "Introduced by this proposal" : "Inherited"}
                {" · "}{traitNames.get(capability.sourceTraitId) ??
                  `Trait ${capability.sourceTraitId}`}
                {" · "}{capability.activationCount.toLocaleString("en-US")} recorded entries into
                Conserving
              </small>
            </li>
          ))}
        </ul>
      ) : (
        <p className="unavailable">No tracked non-reaction capability was installed.</p>
      )}
      {introducedReactions.length === 0 ? (
        <p className="chronicle-activation-none">
          This proposal installed no new compiled reaction.
        </p>
      ) : null}
      {landmark.reactionActivations.length > 0 ? (
        <ul>
          {landmark.reactionActivations.map((reaction) => (
            <li key={reaction.reactionId}>
              <strong>{reactionNames.get(reaction.reactionId) ??
                `Reaction ${reaction.reactionId}`}</strong>
              <span>{reaction.installed ? "Installed" : "Not installed"}</span>
              <small>
                {reaction.introducedByProposal ? "Introduced by this proposal" : "Inherited"}
                {" · "}{reaction.activationCount.toLocaleString("en-US")} recorded reaction events
              </small>
            </li>
          ))}
        </ul>
      ) : (
        <p className="unavailable">The reviewed branch has no compiled reaction.</p>
      )}
    </section>
  );
}

function capabilityLabel(kind: LineageReviewCapabilityKind): string {
  switch (kind) {
    case LineageReviewCapabilityKind.RESOURCE_CONSERVATION: return "Resource conservation";
    default: return "Unknown capability";
  }
}

function ObservationCard({
  id,
  title,
  baseline,
  current,
}: {
  readonly id: string;
  readonly title: string;
  readonly baseline: LineageReviewObservation | undefined;
  readonly current: LineageReviewObservation | undefined;
}) {
  if (baseline === undefined || current === undefined) return null;
  return (
    <section id={id} className="chronicle-observation">
      <h4>{title}</h4>
      <p>{scopeLabel(current.populationScope)}</p>
      <dl>
        <Metric label="Population" before={baseline.population} after={current.population} />
        <Metric label="Average health" before={baseline.averageHealthQ} after={current.averageHealthQ} ratio />
        <Metric label="Average reserve" before={baseline.averageReserveQ} after={current.averageReserveQ} ratio />
        <Metric label="Acquisition coverage" before={baseline.averageAcquisitionCoverageQ} after={current.averageAcquisitionCoverageQ} ratio />
        <Metric label="Resource pressure" before={baseline.averageResourcePressureQ} after={current.averageResourcePressureQ} ratio />
        <Metric label="Occupied tiles" before={baseline.occupiedTileCount} after={current.occupiedTileCount} />
      </dl>
      <BehaviorMix baseline={baseline} current={current} />
      {current.activityCountsAvailable ? (
        <p className="chronicle-activity">
          During window: {current.birthCount.toLocaleString("en-US")} births ·{" "}
          {current.deathCount.toLocaleString("en-US")} deaths ·{" "}
          {current.migrationCount.toLocaleString("en-US")} migrations
        </p>
      ) : (
        <p className="chronicle-activity unavailable">
          Window activity is unavailable at this observation scope—not zero.
        </p>
      )}
    </section>
  );
}

function BehaviorMix({
  baseline,
  current,
}: {
  readonly baseline: LineageReviewObservation;
  readonly current: LineageReviewObservation;
}) {
  const baselineCounts = new Map(baseline.behaviorCounts.map((count) =>
    [count.behavior, count.count]));
  const currentCounts = new Map(current.behaviorCounts.map((count) =>
    [count.behavior, count.count]));
  const behaviors = [...new Set([...baselineCounts.keys(), ...currentCounts.keys()])]
    .sort((left, right) => left - right);
  if (behaviors.length === 0) return null;
  return (
    <div className="chronicle-behaviors">
      <strong>Behavior mix</strong>
      <table>
        <thead>
          <tr><th>Behavior</th><th>Start</th><th>Boundary</th></tr>
        </thead>
        <tbody>
          {behaviors.map((behavior) => (
            <tr key={behavior}>
              <th>{behaviorLabel(behavior)}</th>
              <td>{formatBehaviorCount(baselineCounts.get(behavior) ?? 0n, baseline.population)}</td>
              <td>{formatBehaviorCount(currentCounts.get(behavior) ?? 0n, current.population)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function formatBehaviorCount(count: bigint, population: bigint): string {
  const percent = population === 0n
    ? 0
    : Number(count * 1_000n / population) / 10;
  return `${count.toLocaleString("en-US")} (${percent.toFixed(1)}%)`;
}

function behaviorLabel(behavior: OrganismBehavior): string {
  switch (behavior) {
    case OrganismBehavior.BASELINE: return "Baseline";
    case OrganismBehavior.CONSERVING: return "Conserving";
    case OrganismBehavior.FORAGING: return "Foraging";
    case OrganismBehavior.DISPERSING: return "Dispersing";
    case OrganismBehavior.FLEEING: return "Fleeing";
    default: return "Unknown";
  }
}

function EvidenceNavigation({
  landmark,
  references,
  liveTileIds,
  controlledSpeciesId,
  onNavigateResourceTile,
}: {
  readonly landmark: LineageReviewLandmark;
  readonly references: readonly LineageReviewEvidenceReference[];
  readonly liveTileIds: ReadonlySet<number>;
  readonly controlledSpeciesId: bigint;
  readonly onNavigateResourceTile: (tileId: number) => void;
}) {
  if (references.length === 0) return null;
  const eventId = landmark.eventId.toString();
  return (
    <nav className="chronicle-evidence" aria-label={`Evidence for chronicle event ${eventId}`}>
      <strong>Follow the evidence</strong>
      <ul>
        {references.map((reference, index) => (
          <li key={`${reference.kind}:${reference.speciesId ?? ""}:${reference.tileId ?? ""}:${index}`}>
            <EvidenceLink
              eventId={eventId}
              reference={reference}
              liveTileIds={liveTileIds}
              controlledSpeciesId={controlledSpeciesId}
              onNavigateResourceTile={onNavigateResourceTile}
            />
          </li>
        ))}
      </ul>
    </nav>
  );
}

function EvidenceLink({
  eventId,
  reference,
  liveTileIds,
  controlledSpeciesId,
  onNavigateResourceTile,
}: {
  readonly eventId: string;
  readonly reference: LineageReviewEvidenceReference;
  readonly liveTileIds: ReadonlySet<number>;
  readonly controlledSpeciesId: bigint;
  readonly onNavigateResourceTile: (tileId: number) => void;
}) {
  const window = `after tick ${reference.fromExclusiveTick.toLocaleString("en-US")} through ${reference.throughCompletedTick.toLocaleString("en-US")}`;
  switch (reference.kind) {
    case LineageReviewEvidenceReferenceKind.SPECIATION_DECISION:
      return <a href={`#chronicle-${eventId}-decision`}>Speciation decision facts · {window}</a>;
    case LineageReviewEvidenceReferenceKind.REVIEWED_SPECIES_SUMMARY:
      return <a href={`#chronicle-${eventId}-reviewed`}>Reviewed species summary · {window}</a>;
    case LineageReviewEvidenceReferenceKind.COMPARISON_SPECIES_SUMMARY:
      return <a href={`#chronicle-${eventId}-comparison`}>Comparison species summary · {window}</a>;
    case LineageReviewEvidenceReferenceKind.REVIEWED_JOURNEY_WINDOW:
      return reference.speciesId === controlledSpeciesId
        ? <a href="#journey-title">Reviewed organism Journey · {window}</a>
        : <span>Reviewed Journey · unavailable under current species control</span>;
    case LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW:
      if (reference.tileId === undefined || !liveTileIds.has(reference.tileId)) {
        return <span>Tile {reference.tileId ?? "?"} resources · not currently live</span>;
      }
      return (
        <a
          href="#resource-title"
          onClick={() => onNavigateResourceTile(reference.tileId!)}
        >
          Tile {reference.tileId} live resource evidence · {window}
        </a>
      );
    default:
      return <span>Evidence reference unavailable</span>;
  }
}

function Metric({
  label,
  before,
  after,
  ratio = false,
}: {
  readonly label: string;
  readonly before: bigint | number;
  readonly after: bigint | number;
  readonly ratio?: boolean;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{formatMetric(before, ratio)} → {formatMetric(after, ratio)}</dd>
    </div>
  );
}

function formatMetric(value: bigint | number, ratio: boolean): string {
  if (ratio) return `${(Number(value) / 10_000).toFixed(1)}%`;
  return value.toLocaleString("en-US");
}

function formatWindow(hours: bigint): string {
  if (hours % 24n === 0n) return `${(hours / 24n).toLocaleString("en-US")} days`;
  return `${hours.toLocaleString("en-US")} hours`;
}

function scopeLabel(scope: SpeciesPopulationScope): string {
  return scope === SpeciesPopulationScope.WORLD_EXACT
    ? "World-exact branch evidence"
    : "Observed only on the reviewed branch's live tiles";
}

function evidenceLabel(kind: LineageReviewEvidenceKind): string {
  switch (kind) {
    case LineageReviewEvidenceKind.CAPABILITY_ACTIVATION:
      return "capability activation and common branch outcomes";
    case LineageReviewEvidenceKind.CONDITION_AND_PRESSURE:
      return "condition, acquisition coverage, reserve response, and common branch outcomes";
    case LineageReviewEvidenceKind.GEOGRAPHIC_SPREAD:
      return "migration, occupied tiles, and common branch outcomes";
    case LineageReviewEvidenceKind.RESERVE_STORAGE:
      return "reserve condition, reproduction, and common branch outcomes";
    default:
      return "population, health, acquisition coverage, reserves, occupancy, births, deaths, and migration";
  }
}
