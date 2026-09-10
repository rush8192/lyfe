import {
  EvolutionBenefitTiming,
  EvolutionStrategicIntent,
  type ApplySpeciationResponse,
  type EvolutionTileActivationEvidence,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";
import {
  OrganismActionGateReason,
  OrganismJourneyEventFamily,
  ResourceFlowKind,
  SpeciesPopulationScope,
  type ActorWorldProjection,
  type OrganismProjection,
} from "../generated/lyfe/v1/projection_pb";

const CONSEQUENCE_REVIEWS_STORAGE_KEY = "lyfe.v1.consequence-reviews";
const MAX_CONSEQUENCE_REVIEWS = 256;
const MAX_UINT64 = (1n << 64n) - 1n;

export interface ConsequenceReview {
  readonly worldId: bigint;
  readonly eventId: bigint;
  readonly ancestorSpeciesId: bigint;
  readonly descendantSpeciesId: bigint;
  readonly applicationTick: bigint;
  readonly applicationHour: bigint;
  readonly cooldownEndTick: bigint;
  readonly cooldownEndHour: bigint;
  readonly ancestorPopulationBefore: bigint;
  readonly ancestorPopulationAfter: bigint;
  readonly descendantPopulation: bigint;
  readonly foundingTileIds: readonly number[];
  readonly acquiredTraitIds: readonly number[];
  readonly addedReactionIds: readonly number[];
  readonly resourceConservationAdded: boolean;
  readonly strategicIntents: readonly EvolutionStrategicIntent[];
  readonly benefitTiming: EvolutionBenefitTiming;
  readonly founderAverageHealthQ: number | null;
  readonly founderAverageReserveQ: number | null;
  readonly founderAverageResourcePressureQ: number | null;
  readonly ancestorRecurringCostQPerOrganismHour: bigint | null;
  readonly descendantRecurringCostQPerOrganismHour: bigint | null;
}

export interface ConsequenceReviewStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export interface LineageConsequenceObservation {
  readonly speciesId: bigint;
  readonly available: boolean;
  readonly population: bigint | null;
  readonly populationScope: SpeciesPopulationScope | null;
  readonly averageHealthQ: number | null;
  readonly averageReserveQ: number | null;
  readonly averageAcquisitionCoverageQ: number | null;
  readonly averageResourcePressureQ: number | null;
  readonly behaviorCounts: readonly { readonly behavior: number; readonly count: bigint }[];
  readonly occupiedTileIds: readonly number[];
  readonly birthsSinceBranch: number | null;
  readonly deathsSinceBranch: number | null;
  readonly organismsWithSuppressedActions: number | null;
}

export interface ConsequenceSnapshot {
  readonly elapsedHours: bigint;
  readonly remainingHours: bigint;
  readonly complete: boolean;
  readonly ancestor: LineageConsequenceObservation;
  readonly descendant: LineageConsequenceObservation;
  readonly addedReactionFlow: {
    readonly observed: boolean;
    readonly amountQ: bigint;
    readonly tileIds: readonly number[];
    readonly periodHours: number | null;
  };
}

export function createConsequenceReview(
  world: ActorWorldProjection,
  response: ApplySpeciationResponse,
  request: SpeciationProposalRequest,
): ConsequenceReview {
  const proposal = response.proposal;
  if (!response.applied || proposal === undefined || !proposal.accepted ||
      response.eventId === 0n || response.descendantSpeciesId === 0n ||
      request.worldId !== world.worldId ||
      request.ancestorSpeciesId !== world.controlledSpeciesId ||
      response.applicationTick !== world.completedTick ||
      world.tickDurationHours === 0 ||
      proposal.resultingSpeciationNotBeforeTick <= response.applicationTick) {
    throw new Error("Applied speciation cannot anchor a consequence review.");
  }
  const cooldownTicks = proposal.resultingSpeciationNotBeforeTick - response.applicationTick;
  const currentReactions = new Set(proposal.currentPhenotype?.activeReactionIds ?? []);
  const addedReactionIds = (proposal.proposedPhenotype?.activeReactionIds ?? [])
    .filter((id) => !currentReactions.has(id));
  const founderWeight = proposal.tileActivationEvidence.reduce(
    (total, tile) => total + BigInt(tile.founderCount),
    0n,
  );

  return {
    worldId: world.worldId,
    eventId: response.eventId,
    ancestorSpeciesId: request.ancestorSpeciesId,
    descendantSpeciesId: response.descendantSpeciesId,
    applicationTick: response.applicationTick,
    applicationHour: world.simulatedHours,
    cooldownEndTick: proposal.resultingSpeciationNotBeforeTick,
    cooldownEndHour: world.simulatedHours + cooldownTicks * BigInt(world.tickDurationHours),
    ancestorPopulationBefore: proposal.ancestorPopulationBefore,
    ancestorPopulationAfter: proposal.ancestorPopulationAfter,
    descendantPopulation: proposal.descendantPopulation,
    foundingTileIds: proposal.founderCounts.map((founder) => founder.tileId),
    acquiredTraitIds: [...request.newTraitIds],
    addedReactionIds,
    resourceConservationAdded:
      proposal.currentPhenotype?.resourceConservation === false &&
      proposal.proposedPhenotype?.resourceConservation === true,
    strategicIntents: [...proposal.strategicIntents],
    benefitTiming: proposal.benefitTiming,
    founderAverageHealthQ: weightedFounderAverage(
      proposal.tileActivationEvidence,
      founderWeight,
      (tile) => tile.averageHealthQ,
    ),
    founderAverageReserveQ: weightedFounderAverage(
      proposal.tileActivationEvidence,
      founderWeight,
      (tile) => tile.averageReserveQ,
    ),
    founderAverageResourcePressureQ: weightedFounderAverage(
      proposal.tileActivationEvidence,
      founderWeight,
      (tile) => tile.averageResourcePressureQ,
    ),
    ancestorRecurringCostQPerOrganismHour:
      sumRecurringCosts(proposal.currentPhenotype?.recurringCosts),
    descendantRecurringCostQPerOrganismHour:
      sumRecurringCosts(proposal.proposedPhenotype?.recurringCosts),
  };
}

export function buildConsequenceSnapshot(
  review: ConsequenceReview,
  world: ActorWorldProjection,
): ConsequenceSnapshot {
  if (world.worldId !== review.worldId) {
    throw new Error("Consequence review belongs to a different world.");
  }
  const elapsedHours = world.simulatedHours > review.applicationHour
    ? world.simulatedHours - review.applicationHour
    : 0n;
  const remainingHours = world.simulatedHours < review.cooldownEndHour
    ? review.cooldownEndHour - world.simulatedHours
    : 0n;
  const addedReactions = new Set(review.addedReactionIds);
  let amountQ = 0n;
  let periodHours: number | null = null;
  const tileIds = new Set<number>();
  if (addedReactions.size > 0) {
    for (const tile of world.tiles) {
      if (tile.detail.case !== "live") continue;
      for (const contributor of tile.detail.value.resourceFlowContributors) {
        if (contributor.speciesId !== review.descendantSpeciesId ||
            !addedReactions.has(contributor.reactionId) ||
            contributor.kind !== ResourceFlowKind.ORGANISM_UPTAKE ||
            contributor.amountQ <= 0n) continue;
        amountQ += contributor.amountQ;
        tileIds.add(tile.tileId);
        periodHours = periodHours === null
          ? tile.detail.value.resourceFlowPeriodHours
          : Math.max(periodHours, tile.detail.value.resourceFlowPeriodHours);
      }
    }
  }

  return {
    elapsedHours,
    remainingHours,
    complete: world.simulatedHours >= review.cooldownEndHour,
    ancestor: observeLineage(review.ancestorSpeciesId, review, world),
    descendant: observeLineage(review.descendantSpeciesId, review, world),
    addedReactionFlow: {
      observed: amountQ > 0n,
      amountQ,
      tileIds: [...tileIds].sort((left, right) => left - right),
      periodHours,
    },
  };
}

export function writeConsequenceReview(
  storage: ConsequenceReviewStorage,
  review: ConsequenceReview,
): void {
  const reviews = readConsequenceReviews(storage).filter((candidate) =>
    candidate.worldId !== review.worldId || candidate.eventId !== review.eventId);
  reviews.push(review);
  reviews.sort(compareReviews);
  if (reviews.length > MAX_CONSEQUENCE_REVIEWS) {
    throw new Error("Too many saved consequence reviews.");
  }
  storage.setItem(CONSEQUENCE_REVIEWS_STORAGE_KEY, JSON.stringify({
    version: 1,
    reviews: reviews.map(serializeReview),
  }));
}

export function readPinnedConsequenceReview(
  storage: ConsequenceReviewStorage,
  world: ActorWorldProjection,
): ConsequenceReview | null {
  try {
    return readConsequenceReviews(storage)
      .filter((review) => review.worldId === world.worldId &&
        review.descendantSpeciesId === world.controlledSpeciesId &&
        world.simulatedHours >= review.applicationHour &&
        world.simulatedHours <= review.cooldownEndHour)
      .sort(compareReviews)
      .at(-1) ?? null;
  } catch {
    return null;
  }
}

function observeLineage(
  speciesId: bigint,
  review: ConsequenceReview,
  world: ActorWorldProjection,
): LineageConsequenceObservation {
  const species = world.species.find((candidate) => candidate.speciesId === speciesId);
  const organisms = visibleOrganisms(world, speciesId);
  const eventHistoryAvailable = speciesId === world.controlledSpeciesId;
  const events = eventHistoryAvailable
    ? world.journeyEvents.filter((event) =>
      event.subjectSpeciesId === speciesId && event.tick > review.applicationTick)
    : [];
  return {
    speciesId,
    available: species !== undefined,
    population: species?.population ?? null,
    populationScope: species?.populationScope ?? null,
    averageHealthQ: species?.evolution?.averageHealthQ ?? average(organisms, (item) => item.relativeHealthQ),
    averageReserveQ: average(organisms, (item) => item.reserveFactorQ),
    averageAcquisitionCoverageQ: average(organisms, (item) => item.recentAcquisitionCoverageQ),
    averageResourcePressureQ: average(organisms, (item) => item.resourcePressureQ),
    behaviorCounts: species?.behaviorCounts.map((item) => ({
      behavior: item.behavior,
      count: item.count,
    })) ?? [],
    occupiedTileIds: world.tiles
      .filter((tile) => tile.detail.case === "live" &&
        tile.detail.value.organisms.some((organism) => organism.speciesId === speciesId))
      .map((tile) => tile.tileId),
    birthsSinceBranch: eventHistoryAvailable
      ? events.filter((event) => event.family === OrganismJourneyEventFamily.BIRTH).length
      : null,
    deathsSinceBranch: eventHistoryAvailable
      ? events.filter((event) => event.family === OrganismJourneyEventFamily.DEATH).length
      : null,
    organismsWithSuppressedActions: organisms.length === 0 ? null : organisms.filter((organism) =>
      organism.actionGateEvidence.some((gate) =>
        gate.reason === OrganismActionGateReason.BEHAVIOR_SUPPRESSED)).length,
  };
}

function visibleOrganisms(world: ActorWorldProjection, speciesId: bigint): OrganismProjection[] {
  return world.tiles.flatMap((tile) => tile.detail.case === "live"
    ? tile.detail.value.organisms.filter((organism) => organism.speciesId === speciesId)
    : []);
}

function average(
  organisms: readonly OrganismProjection[],
  select: (organism: OrganismProjection) => number,
): number | null {
  if (organisms.length === 0) return null;
  return Math.floor(organisms.reduce((total, organism) => total + select(organism), 0) /
    organisms.length);
}

function weightedFounderAverage(
  values: readonly EvolutionTileActivationEvidence[],
  totalWeight: bigint,
  select: (value: EvolutionTileActivationEvidence) => number,
): number | null {
  if (totalWeight === 0n) return null;
  const weighted = values.reduce((total, value) =>
    total + BigInt(select(value)) * BigInt(value.founderCount), 0n);
  return Number(weighted / totalWeight);
}

function sumRecurringCosts(
  costs: readonly { readonly amountQPerOrganismHour: bigint }[] | undefined,
): bigint | null {
  return costs === undefined
    ? null
    : costs.reduce((total, cost) => total + cost.amountQPerOrganismHour, 0n);
}

function readConsequenceReviews(storage: ConsequenceReviewStorage): ConsequenceReview[] {
  const raw = storage.getItem(CONSEQUENCE_REVIEWS_STORAGE_KEY);
  if (raw === null) return [];
  const parsed: unknown = JSON.parse(raw);
  if (!isRecord(parsed) || parsed.version !== 1 || !Array.isArray(parsed.reviews) ||
      parsed.reviews.length > MAX_CONSEQUENCE_REVIEWS) {
    throw new Error("Saved consequence reviews use an unsupported format.");
  }
  const reviews = parsed.reviews.map(parseReview);
  const identities = new Set(reviews.map((review) =>
    `${review.worldId.toString()}:${review.eventId.toString()}`));
  if (identities.size !== reviews.length) {
    throw new Error("Saved consequence reviews contain duplicate events.");
  }
  return reviews;
}

function serializeReview(review: ConsequenceReview) {
  return {
    ...review,
    worldId: review.worldId.toString(),
    eventId: review.eventId.toString(),
    ancestorSpeciesId: review.ancestorSpeciesId.toString(),
    descendantSpeciesId: review.descendantSpeciesId.toString(),
    applicationTick: review.applicationTick.toString(),
    applicationHour: review.applicationHour.toString(),
    cooldownEndTick: review.cooldownEndTick.toString(),
    cooldownEndHour: review.cooldownEndHour.toString(),
    ancestorPopulationBefore: review.ancestorPopulationBefore.toString(),
    ancestorPopulationAfter: review.ancestorPopulationAfter.toString(),
    descendantPopulation: review.descendantPopulation.toString(),
    ancestorRecurringCostQPerOrganismHour:
      review.ancestorRecurringCostQPerOrganismHour?.toString() ?? null,
    descendantRecurringCostQPerOrganismHour:
      review.descendantRecurringCostQPerOrganismHour?.toString() ?? null,
  };
}

function parseReview(value: unknown): ConsequenceReview {
  if (!isRecord(value)) throw new Error("Saved consequence review is malformed.");
  const review: ConsequenceReview = {
    worldId: parseUint64(value.worldId, true),
    eventId: parseUint64(value.eventId, true),
    ancestorSpeciesId: parseUint64(value.ancestorSpeciesId, true),
    descendantSpeciesId: parseUint64(value.descendantSpeciesId, true),
    applicationTick: parseUint64(value.applicationTick),
    applicationHour: parseUint64(value.applicationHour),
    cooldownEndTick: parseUint64(value.cooldownEndTick),
    cooldownEndHour: parseUint64(value.cooldownEndHour),
    ancestorPopulationBefore: parseUint64(value.ancestorPopulationBefore),
    ancestorPopulationAfter: parseUint64(value.ancestorPopulationAfter),
    descendantPopulation: parseUint64(value.descendantPopulation),
    foundingTileIds: parseNumberList(value.foundingTileIds, 4, true),
    acquiredTraitIds: parseNumberList(value.acquiredTraitIds, 64, false),
    addedReactionIds: parseNumberList(value.addedReactionIds, 64, false),
    resourceConservationAdded: parseBoolean(value.resourceConservationAdded),
    strategicIntents: parseNumberList(value.strategicIntents, 16, false) as EvolutionStrategicIntent[],
    benefitTiming: parseEnum(value.benefitTiming, EvolutionBenefitTiming.IMMEDIATE,
      EvolutionBenefitTiming.PREPARATORY) as EvolutionBenefitTiming,
    founderAverageHealthQ: parseRatio(value.founderAverageHealthQ),
    founderAverageReserveQ: parseRatio(value.founderAverageReserveQ),
    founderAverageResourcePressureQ: parseRatio(value.founderAverageResourcePressureQ),
    ancestorRecurringCostQPerOrganismHour: parseNullableUint64(
      value.ancestorRecurringCostQPerOrganismHour),
    descendantRecurringCostQPerOrganismHour: parseNullableUint64(
      value.descendantRecurringCostQPerOrganismHour),
  };
  if (review.cooldownEndTick <= review.applicationTick ||
      review.cooldownEndHour <= review.applicationHour ||
      review.ancestorPopulationAfter > review.ancestorPopulationBefore ||
      review.foundingTileIds.length === 0 || review.acquiredTraitIds.length === 0 ||
      review.strategicIntents.length === 0 || review.strategicIntents.some((intent) =>
        intent < EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE ||
        intent > EvolutionStrategicIntent.INVEST_IN_COMPLEXITY)) {
    throw new Error("Saved consequence review is inconsistent.");
  }
  return review;
}

function parseUint64(value: unknown, positive = false): bigint {
  if (typeof value !== "string" || !/^(0|[1-9][0-9]*)$/.test(value)) {
    throw new Error("Saved consequence value is malformed.");
  }
  const parsed = BigInt(value);
  if (parsed > MAX_UINT64 || positive && parsed === 0n) {
    throw new Error("Saved consequence value is out of range.");
  }
  return parsed;
}

function parseNullableUint64(value: unknown): bigint | null {
  return value === null ? null : parseUint64(value);
}

function parseRatio(value: unknown): number | null {
  if (value === null) return null;
  if (typeof value !== "number" || !Number.isInteger(value) || value < 0 || value > 1_000_000) {
    throw new Error("Saved consequence ratio is out of range.");
  }
  return value;
}

function parseEnum(value: unknown, minimum: number, maximum: number): number {
  if (typeof value !== "number" || !Number.isInteger(value) ||
      value < minimum || value > maximum) {
    throw new Error("Saved consequence enum is out of range.");
  }
  return value;
}

function parseBoolean(value: unknown): boolean {
  if (typeof value !== "boolean") {
    throw new Error("Saved consequence flag is malformed.");
  }
  return value;
}

function parseNumberList(
  value: unknown,
  maximumLength: number,
  allowZero: boolean,
): number[] {
  if (!Array.isArray(value) || value.length > maximumLength) {
    throw new Error("Saved consequence ID list is malformed.");
  }
  const result = value.map((item) => {
    if (typeof item !== "number" || !Number.isInteger(item) ||
        item < (allowZero ? 0 : 1) || item > 0xffff_ffff) {
      throw new Error("Saved consequence ID is out of range.");
    }
    return item;
  });
  if (result.some((item, index) => index > 0 && item <= result[index - 1])) {
    throw new Error("Saved consequence IDs are not canonical.");
  }
  return result;
}

function compareReviews(left: ConsequenceReview, right: ConsequenceReview): number {
  if (left.worldId !== right.worldId) return left.worldId < right.worldId ? -1 : 1;
  if (left.applicationTick !== right.applicationTick) {
    return left.applicationTick < right.applicationTick ? -1 : 1;
  }
  return left.eventId === right.eventId ? 0 : left.eventId < right.eventId ? -1 : 1;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
