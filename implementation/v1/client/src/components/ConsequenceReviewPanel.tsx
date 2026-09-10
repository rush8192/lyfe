import {
  EvolutionBenefitTiming,
  EvolutionStrategicIntent,
  type EvolutionDecisionSurface,
} from "../generated/lyfe/v1/evolution_pb";
import {
  OrganismBehavior,
  SpeciesPopulationScope,
  type ActorWorldProjection,
} from "../generated/lyfe/v1/projection_pb";
import {
  buildConsequenceSnapshot,
  type ConsequenceReview,
  type LineageConsequenceObservation,
} from "../state/consequenceReview";

interface ConsequenceReviewPanelProps {
  readonly review: ConsequenceReview;
  readonly world: ActorWorldProjection;
  readonly evolution: EvolutionDecisionSurface;
}

export function ConsequenceReviewPanel({
  review,
  world,
  evolution,
}: ConsequenceReviewPanelProps) {
  const snapshot = buildConsequenceSnapshot(review, world);
  const totalHours = review.cooldownEndHour - review.applicationHour;
  const displayedElapsed = snapshot.elapsedHours > totalHours ? totalHours : snapshot.elapsedHours;
  const traitNames = review.acquiredTraitIds.map((id) =>
    evolution.traits.find((trait) => trait.traitId === id)?.displayName ?? `trait ${id}`);

  return (
    <section className="consequence-panel" aria-labelledby="consequence-title">
      <header className="consequence-header">
        <div>
          <p className="section-label">Pinned consequence review · authorized observation</p>
          <h2 id="consequence-title">What changed after the branch?</h2>
          <p>
            Event {review.eventId.toString()} split species {review.ancestorSpeciesId.toString()} into
            descendant species {review.descendantSpeciesId.toString()} at hour {review.applicationHour.toLocaleString("en-US")}.
            This comparison reports evidence; it does not declare the branch a success or failure.
          </p>
        </div>
        <div className="consequence-window">
          <span>{snapshot.complete ? "Review boundary reached" : "Review in progress"}</span>
          <strong>{displayedElapsed.toLocaleString("en-US")} / {totalHours.toLocaleString("en-US")} hours</strong>
          <progress value={Number(displayedElapsed)} max={Number(totalHours)} />
          <small>{snapshot.complete
            ? "The universal cooldown window is complete."
            : `${snapshot.remainingHours.toLocaleString("en-US")} simulated hours remain.`}</small>
        </div>
      </header>

      <div className="consequence-hypothesis">
        <div>
          <span>Acquired DNA</span>
          <strong>{traitNames.join(" + ")}</strong>
        </div>
        <div>
          <span>Authored intent</span>
          <strong>{review.strategicIntents.map(strategicIntentLabel).join(" · ")}</strong>
        </div>
        <div>
          <span>Benefit timing</span>
          <strong>{benefitTimingLabel(review.benefitTiming)}</strong>
        </div>
      </div>

      <div className="consequence-table-wrap">
        <table className="consequence-table">
          <thead>
            <tr>
              <th scope="col">Evidence</th>
              <th scope="col">Ancestor species {review.ancestorSpeciesId.toString()}</th>
              <th scope="col">Descendant species {review.descendantSpeciesId.toString()}</th>
            </tr>
          </thead>
          <tbody>
            <ReviewRow
              label="Branch-start population"
              ancestor={`${formatInteger(review.ancestorPopulationAfter)} world exact`}
              descendant={`${formatInteger(review.descendantPopulation)} world exact`}
            />
            <ReviewRow
              label="Current population"
              ancestor={populationLabel(snapshot.ancestor, review.ancestorPopulationAfter)}
              descendant={populationLabel(snapshot.descendant, review.descendantPopulation)}
            />
            <ReviewRow
              label="Average health"
              ancestor={currentRatioLabel(snapshot.ancestor.averageHealthQ, snapshot.ancestor)}
              descendant={baselineAndCurrentRatio(
                review.founderAverageHealthQ,
                snapshot.descendant.averageHealthQ,
                snapshot.descendant,
                "founder cohort",
              )}
            />
            <ReviewRow
              label="Charged-reserve coverage"
              ancestor={currentRatioLabel(snapshot.ancestor.averageReserveQ, snapshot.ancestor)}
              descendant={baselineAndCurrentRatio(
                review.founderAverageReserveQ,
                snapshot.descendant.averageReserveQ,
                snapshot.descendant,
                "founder cohort",
              )}
            />
            <ReviewRow
              label="Acquisition coverage"
              ancestor={currentRatioLabel(snapshot.ancestor.averageAcquisitionCoverageQ, snapshot.ancestor)}
              descendant={currentRatioLabel(
                snapshot.descendant.averageAcquisitionCoverageQ,
                snapshot.descendant,
              )}
            />
            <ReviewRow
              label="Resource pressure"
              ancestor={currentRatioLabel(snapshot.ancestor.averageResourcePressureQ, snapshot.ancestor)}
              descendant={baselineAndCurrentRatio(
                review.founderAverageResourcePressureQ,
                snapshot.descendant.averageResourcePressureQ,
                snapshot.descendant,
                "founder cohort",
              )}
            />
            <ReviewRow
              label="Births / deaths since branch"
              ancestor={eventCountsLabel(snapshot.ancestor)}
              descendant={eventCountsLabel(snapshot.descendant)}
            />
            <ReviewRow
              label="Visible occupied tiles"
              ancestor={tileLabel(snapshot.ancestor.occupiedTileIds, snapshot.ancestor)}
              descendant={`${tileLabel(snapshot.descendant.occupiedTileIds, snapshot.descendant)} · ` +
                `started on ${review.foundingTileIds.join(", ")}`}
            />
            <ReviewRow
              label="Current behavior distribution"
              ancestor={behaviorLabel(snapshot.ancestor)}
              descendant={behaviorLabel(snapshot.descendant)}
            />
            <ReviewRow
              label="Behavior-suppressed actions"
              ancestor={suppressionLabel(snapshot.ancestor)}
              descendant={suppressionLabel(snapshot.descendant)}
            />
            <ReviewRow
              label="Recurring cost"
              ancestor={costLabel(review.ancestorRecurringCostQPerOrganismHour)}
              descendant={costLabel(review.descendantRecurringCostQPerOrganismHour)}
            />
          </tbody>
        </table>
      </div>

      <aside className="activation-review">
        <div>
          <span>Acquired-capability evidence</span>
          <strong>{activationLabel(review, snapshot.addedReactionFlow)}</strong>
        </div>
        <small>
          Flow evidence is limited to the latest completed interval on currently live tiles. Birth
          and death counts use retained controlled-lineage events; the current projection does not
          expose ancestor event history. Missing or unobserved evidence is never treated as zero.
        </small>
      </aside>
    </section>
  );
}

function ReviewRow({ label, ancestor, descendant }: {
  readonly label: string;
  readonly ancestor: string;
  readonly descendant: string;
}) {
  return (
    <tr>
      <th scope="row">{label}</th>
      <td>{ancestor}</td>
      <td>{descendant}</td>
    </tr>
  );
}

function populationLabel(
  observation: LineageConsequenceObservation,
  baseline: bigint,
): string {
  if (!observation.available || observation.population === null) return "Not currently observed";
  if (observation.populationScope !== SpeciesPopulationScope.WORLD_EXACT) {
    return `${formatInteger(observation.population)} · ${populationScopeLabel(observation.populationScope)}; ` +
      "no delta from the exact baseline is inferred";
  }
  const delta = observation.population - baseline;
  return `${formatInteger(observation.population)} (${formatDelta(delta)}) · ${populationScopeLabel(observation.populationScope)}`;
}

function populationScopeLabel(scope: SpeciesPopulationScope | null): string {
  if (scope === SpeciesPopulationScope.WORLD_EXACT) return "world exact";
  if (scope === SpeciesPopulationScope.LIVE_TILES_OBSERVED) return "live tiles observed";
  return "scope unavailable";
}

function currentRatioLabel(
  value: number | null,
  observation: LineageConsequenceObservation,
): string {
  if (!observation.available || value === null) return "Not currently observed";
  return `${formatRatio(value)} · ${observation.populationScope === SpeciesPopulationScope.WORLD_EXACT
    ? "current lineage"
    : "visible organisms"}`;
}

function baselineAndCurrentRatio(
  baseline: number | null,
  current: number | null,
  observation: LineageConsequenceObservation,
  baselineScope: string,
): string {
  const currentLabel = currentRatioLabel(current, observation);
  if (baseline === null) return currentLabel;
  return `${formatRatio(baseline)} ${baselineScope} → ${currentLabel}`;
}

function eventCountsLabel(observation: LineageConsequenceObservation): string {
  if (observation.birthsSinceBranch === null || observation.deathsSinceBranch === null) {
    return "Unavailable in current actor projection";
  }
  return `${observation.birthsSinceBranch.toLocaleString("en-US")} / ` +
    `${observation.deathsSinceBranch.toLocaleString("en-US")} retained events`;
}

function tileLabel(
  tileIds: readonly number[],
  observation: LineageConsequenceObservation,
): string {
  if (!observation.available) return "Not currently observed";
  return tileIds.length === 0 ? "No visible occupancy" : tileIds.join(", ");
}

function behaviorLabel(observation: LineageConsequenceObservation): string {
  if (!observation.available) return "Not currently observed";
  if (observation.behaviorCounts.length === 0) return "No visible behavior counts";
  return observation.behaviorCounts.map((item) =>
    `${behaviorName(item.behavior)} ${formatInteger(item.count)}`).join(" · ");
}

function suppressionLabel(observation: LineageConsequenceObservation): string {
  if (observation.organismsWithSuppressedActions === null) {
    return observation.available ? "No visible organism evidence" : "Not currently observed";
  }
  return `${observation.organismsWithSuppressedActions.toLocaleString("en-US")} visible organisms`;
}

function costLabel(value: bigint | null): string {
  return value === null ? "Not captured in proposal" : `${formatInteger(value)} q / organism-hour`;
}

function activationLabel(
  review: ConsequenceReview,
  flow: ReturnType<typeof buildConsequenceSnapshot>["addedReactionFlow"],
): string {
  if (review.addedReactionIds.length > 0) {
    const reactions = review.addedReactionIds.map((id) => `reaction ${id}`).join(", ");
    return flow.observed
      ? `${formatInteger(flow.amountQ)} q uptake through ${reactions} across live tile${flow.tileIds.length === 1 ? "" : "s"} ${flow.tileIds.join(", ")} in the latest ${flow.periodHours ?? "unknown"}-hour interval.`
      : `No authorized uptake through ${reactions} appears in the latest completed interval.`;
  }
  if (review.resourceConservationAdded) {
    return "The proposal added pressure-gated conservation, so behavior and suppression evidence—not a new reaction—is the relevant signal.";
  }
  return review.benefitTiming === EvolutionBenefitTiming.PREPARATORY
    ? "The proposal was preparatory and added no active reaction; this window checks path opening and side effects, not immediate population gain."
    : "The proposal added no new active reaction; current population, condition, and behavior are the available signals.";
}

function behaviorName(value: number): string {
  switch (value) {
    case OrganismBehavior.BASELINE: return "baseline";
    case OrganismBehavior.CONSERVING: return "conserving";
    case OrganismBehavior.FORAGING: return "foraging";
    case OrganismBehavior.DISPERSING: return "dispersing";
    case OrganismBehavior.FLEEING: return "fleeing";
    default: return "unspecified";
  }
}

function benefitTimingLabel(value: EvolutionBenefitTiming): string {
  switch (value) {
    case EvolutionBenefitTiming.IMMEDIATE: return "Immediate";
    case EvolutionBenefitTiming.MATURING: return "Maturing";
    case EvolutionBenefitTiming.CONDITIONAL: return "Conditional";
    case EvolutionBenefitTiming.PREPARATORY: return "Preparatory";
    default: return "Unspecified";
  }
}

function strategicIntentLabel(value: EvolutionStrategicIntent): string {
  switch (value) {
    case EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE: return "Exploit current niche";
    case EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE: return "Endure pressure";
    case EvolutionStrategicIntent.ALTER_DISPERSAL: return "Alter dispersal";
    case EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS: return "Diversify access";
    case EvolutionStrategicIntent.BIOLOGICAL_INTERACTION: return "Biological interaction";
    case EvolutionStrategicIntent.INVEST_IN_COMPLEXITY: return "Invest in complexity";
    default: return "Unspecified intent";
  }
}

function formatRatio(value: number): string {
  return `${(value / 10_000).toFixed(1)}%`;
}

function formatInteger(value: bigint): string {
  return value.toLocaleString("en-US");
}

function formatDelta(value: bigint): string {
  if (value === 0n) return "no population change";
  return `${value > 0n ? "+" : "−"}${formatInteger(value > 0n ? value : -value)} since branch`;
}
