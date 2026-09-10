import { create } from "@bufbuild/protobuf";
import {
  EvolutionActivationWarningKind,
  EvolutionActivationWarningSeverity,
  EvolutionBenefitTiming,
  EvolutionReactionOpportunityStatus,
  EvolutionRecurringCostChannel,
  EvolutionResourceFlowForecastStatus,
  EvolutionStrategicIntent,
  SpeciationProposalRequestSchema,
  type EvolutionDecisionSurface,
  type EvolutionPhenotypeSummary,
  type EvolutionResourceFlowForecast,
  type SpeciationProposal,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";

const EVOLUTION_GOALS_STORAGE_KEY = "lyfe.v1.evolution-goals";
const MAX_SAVED_EVOLUTION_GOALS = 256;
const MAX_UINT64 = (1n << 64n) - 1n;

export interface EvolutionGoal {
  readonly worldId: bigint;
  readonly controlledSpeciesId: bigint;
  readonly traitIds: readonly number[];
  readonly tileIds: readonly number[];
}

export interface EvolutionGoalStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export type EvolutionGoalAffordability =
  | { readonly status: "affordable"; readonly remainingQ: 0n; readonly estimatedHours: 0n }
  | { readonly status: "pending-first-tick"; readonly remainingQ: bigint }
  | { readonly status: "no-current-income"; readonly remainingQ: bigint }
  | { readonly status: "estimated"; readonly remainingQ: bigint; readonly estimatedHours: bigint };

export function toggleTraitSelection(
  surface: EvolutionDecisionSurface,
  selected: ReadonlySet<number>,
  traitId: number,
): ReadonlySet<number> {
  const next = new Set(selected);
  if (next.has(traitId)) {
    next.delete(traitId);
    let changed = true;
    while (changed) {
      changed = false;
      for (const selectedId of [...next]) {
        const trait = surface.traits.find((candidate) => candidate.traitId === selectedId);
        if (trait?.prerequisiteTraitIds.some((id) =>
          !surface.traits.some((candidate) => candidate.traitId === id && candidate.acquired) &&
          !next.has(id))) {
          next.delete(selectedId);
          changed = true;
        }
      }
    }
    return next;
  }

  const addWithPrerequisites = (id: number): boolean => {
    const trait = surface.traits.find((candidate) => candidate.traitId === id);
    if (trait?.acquired || next.has(id)) return true;
    if (trait === undefined || !trait.selectable) return false;
    for (const prerequisiteId of trait.prerequisiteTraitIds) {
      if (!addWithPrerequisites(prerequisiteId)) return false;
    }
    next.add(id);
    return true;
  };
  addWithPrerequisites(traitId);
  return next;
}

export function createEvolutionGoal(
  surface: EvolutionDecisionSurface,
  selectedTraits: ReadonlySet<number>,
  selectedTiles: ReadonlySet<number>,
): EvolutionGoal {
  const traitIds = closeGoalPrerequisites(surface, selectedTraits);
  const tileIds = [...selectedTiles].sort((left, right) => left - right);
  if (traitIds.length === 0) {
    throw new Error("An evolution goal requires at least one new trait.");
  }
  if (tileIds.length < 1 || tileIds.length > 4 ||
      new Set(tileIds).size !== tileIds.length ||
      tileIds.some((id) => !surface.occupiedTiles.some((tile) => tile.tileId === id))) {
    throw new Error("An evolution goal requires one to four currently occupied tiles.");
  }

  const selected = new Set(traitIds);
  const acquired = new Set(surface.traits.filter((trait) => trait.acquired)
    .map((trait) => trait.traitId));
  let complexity = 0;
  for (const traitId of traitIds) {
    const trait = surface.traits.find((candidate) => candidate.traitId === traitId)!;
    complexity += trait.changeComplexity;
    if (trait.incompatibleTraitIds.some((id) => acquired.has(id) || selected.has(id))) {
      throw new Error("An evolution goal cannot contain incompatible traits.");
    }
  }
  if (complexity > surface.maximumChangeComplexity) {
    throw new Error("An evolution goal exceeds the lineage's current change capacity.");
  }

  return {
    worldId: surface.worldId,
    controlledSpeciesId: surface.controlledSpeciesId,
    traitIds,
    tileIds,
  };
}

export function evolutionGoalPriceQ(
  surface: EvolutionDecisionSurface,
  goal: EvolutionGoal,
): bigint {
  return goal.traitIds.reduce((total, traitId) => {
    const trait = surface.traits.find((candidate) => candidate.traitId === traitId);
    if (trait === undefined) throw new Error(`Evolution goal references unknown trait ${traitId}.`);
    return total + trait.mutationPointCostQ;
  }, 0n);
}

export function resolveEvolutionGoal(
  surface: EvolutionDecisionSurface,
  goal: EvolutionGoal,
): EvolutionGoal | null {
  if (goal.worldId !== surface.worldId ||
      goal.controlledSpeciesId !== surface.controlledSpeciesId) {
    return null;
  }
  try {
    return createEvolutionGoal(surface, new Set(goal.traitIds), new Set(goal.tileIds));
  } catch {
    return null;
  }
}

export function estimateEvolutionGoalAffordability(
  surface: EvolutionDecisionSurface,
  priceQ: bigint,
): EvolutionGoalAffordability {
  const remainingQ = priceQ > surface.mutationBalanceQ
    ? priceQ - surface.mutationBalanceQ
    : 0n;
  if (remainingQ === 0n) {
    return { status: "affordable", remainingQ: 0n, estimatedHours: 0n };
  }
  if (surface.completedTick === 0n) {
    return { status: "pending-first-tick", remainingQ };
  }
  if (surface.lastMutationIncomeQ <= 0n || surface.tickDurationHours <= 0) {
    return { status: "no-current-income", remainingQ };
  }
  const numerator = remainingQ * BigInt(surface.tickDurationHours);
  const estimatedHours = (numerator + surface.lastMutationIncomeQ - 1n) /
    surface.lastMutationIncomeQ;
  return { status: "estimated", remainingQ, estimatedHours };
}

export function evolutionGoalMilestoneTraitIds(
  surface: EvolutionDecisionSurface,
  goal: EvolutionGoal,
): readonly number[] {
  const prerequisites = new Set(goal.traitIds.flatMap((id) =>
    surface.traits.find((trait) => trait.traitId === id)?.prerequisiteTraitIds ?? []));
  return goal.traitIds.filter((id) => !prerequisites.has(id));
}

export function readEvolutionGoal(
  storage: EvolutionGoalStorage,
  surface: EvolutionDecisionSurface,
): EvolutionGoal | null {
  try {
    return readEvolutionGoals(storage).find((goal) =>
      goal.worldId === surface.worldId &&
      goal.controlledSpeciesId === surface.controlledSpeciesId) ?? null;
  } catch {
    return null;
  }
}

export function writeEvolutionGoal(
  storage: EvolutionGoalStorage,
  goal: EvolutionGoal,
): void {
  const goals = readEvolutionGoals(storage).filter((candidate) =>
    candidate.worldId !== goal.worldId ||
    candidate.controlledSpeciesId !== goal.controlledSpeciesId);
  goals.push(goal);
  goals.sort(compareEvolutionGoals);
  if (goals.length > MAX_SAVED_EVOLUTION_GOALS) {
    throw new Error("Too many saved evolution goals.");
  }
  storage.setItem(EVOLUTION_GOALS_STORAGE_KEY, JSON.stringify({
    version: 1,
    goals: goals.map((candidate) => ({
      worldId: candidate.worldId.toString(),
      controlledSpeciesId: candidate.controlledSpeciesId.toString(),
      traitIds: candidate.traitIds,
      tileIds: candidate.tileIds,
    })),
  }));
}

export function removeEvolutionGoal(
  storage: EvolutionGoalStorage,
  worldId: bigint,
  controlledSpeciesId: bigint,
): void {
  const goals = readEvolutionGoals(storage).filter((candidate) =>
    candidate.worldId !== worldId ||
    candidate.controlledSpeciesId !== controlledSpeciesId);
  if (goals.length === 0) {
    storage.removeItem(EVOLUTION_GOALS_STORAGE_KEY);
    return;
  }
  storage.setItem(EVOLUTION_GOALS_STORAGE_KEY, JSON.stringify({
    version: 1,
    goals: goals.map((goal) => ({
      worldId: goal.worldId.toString(),
      controlledSpeciesId: goal.controlledSpeciesId.toString(),
      traitIds: goal.traitIds,
      tileIds: goal.tileIds,
    })),
  }));
}

export function evolutionGoalsEqual(
  left: EvolutionGoal,
  right: EvolutionGoal,
): boolean {
  return left.worldId === right.worldId &&
    left.controlledSpeciesId === right.controlledSpeciesId &&
    sameNumbers(left.traitIds, right.traitIds) &&
    sameNumbers(left.tileIds, right.tileIds);
}

function closeGoalPrerequisites(
  surface: EvolutionDecisionSurface,
  selectedTraits: ReadonlySet<number>,
): readonly number[] {
  const closed = new Set<number>();
  const visiting = new Set<number>();
  const add = (traitId: number): void => {
    const trait = surface.traits.find((candidate) => candidate.traitId === traitId);
    if (trait?.acquired) return;
    if (trait === undefined || !trait.selectable || visiting.has(traitId)) {
      throw new Error(`Evolution goal cannot reach trait ${traitId}.`);
    }
    if (closed.has(traitId)) return;
    visiting.add(traitId);
    for (const prerequisiteId of trait.prerequisiteTraitIds) add(prerequisiteId);
    visiting.delete(traitId);
    closed.add(traitId);
  };
  [...selectedTraits].sort((left, right) => left - right).forEach(add);
  return [...closed].sort((left, right) => left - right);
}

function readEvolutionGoals(storage: EvolutionGoalStorage): EvolutionGoal[] {
  const raw = storage.getItem(EVOLUTION_GOALS_STORAGE_KEY);
  if (raw === null) return [];
  const parsed: unknown = JSON.parse(raw);
  if (!isRecord(parsed) || parsed.version !== 1 || !Array.isArray(parsed.goals) ||
      parsed.goals.length > MAX_SAVED_EVOLUTION_GOALS) {
    throw new Error("Saved evolution goals use an unsupported format.");
  }
  const goals = parsed.goals.map(parseEvolutionGoal);
  const identities = new Set(goals.map((goal) =>
    `${goal.worldId.toString()}:${goal.controlledSpeciesId.toString()}`));
  if (identities.size !== goals.length) {
    throw new Error("Saved evolution goals contain duplicate species identities.");
  }
  return goals;
}

function parseEvolutionGoal(value: unknown): EvolutionGoal {
  if (!isRecord(value) || typeof value.worldId !== "string" ||
      typeof value.controlledSpeciesId !== "string" ||
      !Array.isArray(value.traitIds) || !Array.isArray(value.tileIds)) {
    throw new Error("Saved evolution goal is malformed.");
  }
  const worldId = parsePositiveUint64(value.worldId);
  const controlledSpeciesId = parsePositiveUint64(value.controlledSpeciesId);
  const traitIds = parseCanonicalUint32List(value.traitIds, 64, false);
  const tileIds = parseCanonicalUint32List(value.tileIds, 4, true);
  if (traitIds.length === 0 || tileIds.length === 0) {
    throw new Error("Saved evolution goal is empty.");
  }
  return { worldId, controlledSpeciesId, traitIds, tileIds };
}

function parsePositiveUint64(value: string): bigint {
  if (!/^[1-9][0-9]*$/.test(value)) throw new Error("Saved identity is malformed.");
  const parsed = BigInt(value);
  if (parsed > MAX_UINT64) throw new Error("Saved identity is out of range.");
  return parsed;
}

function parseCanonicalUint32List(
  value: unknown[],
  maximumLength: number,
  allowZero: boolean,
): number[] {
  if (value.length > maximumLength) throw new Error("Saved ID list is too long.");
  const result = value.map((item) => {
    if (typeof item !== "number" || !Number.isInteger(item) ||
        item < (allowZero ? 0 : 1) || item > 0xffff_ffff) {
      throw new Error("Saved ID is out of range.");
    }
    return item;
  });
  if (result.some((item, index) => index > 0 && item <= result[index - 1])) {
    throw new Error("Saved IDs are not canonical.");
  }
  return result;
}

function compareEvolutionGoals(left: EvolutionGoal, right: EvolutionGoal): number {
  if (left.worldId !== right.worldId) return left.worldId < right.worldId ? -1 : 1;
  if (left.controlledSpeciesId === right.controlledSpeciesId) return 0;
  return left.controlledSpeciesId < right.controlledSpeciesId ? -1 : 1;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function validateSpeciationProposal(
  proposal: SpeciationProposal,
  request: SpeciationProposalRequest,
): void {
  const current = proposal.currentPhenotype;
  const proposed = proposal.proposedPhenotype;
  if ((current === undefined) !== (proposed === undefined)) {
    throw new Error("Evolution proposal has an incomplete phenotype comparison.");
  }
  if (current === undefined || proposed === undefined) {
    if (proposal.benefitTiming !== EvolutionBenefitTiming.UNSPECIFIED ||
        proposal.activationWarnings.length > 0) {
      throw new Error("Evolution proposal has activation evidence without a comparison.");
    }
    return;
  }
  if (!validPhenotype(current) || !validPhenotype(proposed) ||
      proposal.benefitTiming < EvolutionBenefitTiming.IMMEDIATE ||
      proposal.benefitTiming > EvolutionBenefitTiming.PREPARATORY ||
      proposal.strategicIntents.length === 0 ||
      proposal.strategicIntents.some((intent, index) =>
        intent < EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE ||
        intent > EvolutionStrategicIntent.INVEST_IN_COMPLEXITY ||
        (index > 0 && intent <= proposal.strategicIntents[index - 1]))) {
    throw new Error("Evolution proposal has an invalid phenotype comparison.");
  }

  const selected = new Set(request.newTraitIds);
  const warningKinds = new Set<EvolutionActivationWarningKind>();
  for (const warning of proposal.activationWarnings) {
    if (warning.kind < EvolutionActivationWarningKind.NO_COMPILED_CHANGE ||
        warning.kind > EvolutionActivationWarningKind.NO_NEW_ACTIVE_REACTION ||
        warning.severity < EvolutionActivationWarningSeverity.INFORMATION ||
        warning.severity > EvolutionActivationWarningSeverity.CAUTION ||
        warning.traitIds.length === 0 ||
        warning.traitIds.some((id, index) => !selected.has(id) ||
          (index > 0 && id <= warning.traitIds[index - 1])) ||
        warningKinds.has(warning.kind)) {
      throw new Error("Evolution proposal has invalid activation warnings.");
    }
    warningKinds.add(warning.kind);
    if (warning.kind === EvolutionActivationWarningKind.NO_COMPILED_CHANGE &&
        (!phenotypeEqual(current, proposed) ||
          proposal.benefitTiming !== EvolutionBenefitTiming.PREPARATORY) ||
        warning.kind === EvolutionActivationWarningKind.RESOURCE_PRESSURE_REQUIRED &&
        (current.resourceConservation || !proposed.resourceConservation) ||
        warning.kind === EvolutionActivationWarningKind.NO_NEW_ACTIVE_REACTION &&
        !sameNumbers(current.activeReactionIds, proposed.activeReactionIds)) {
      throw new Error("Evolution proposal activation warning contradicts its comparison.");
    }
  }
  if (invalidTileActivationEvidence(proposal)) {
    throw new Error("Evolution proposal has invalid tile activation evidence.");
  }
}

function validPhenotype(value: EvolutionPhenotypeSummary): boolean {
  return value.mutationIncomeModifierQ >= 250_000 &&
    value.mutationIncomeModifierQ <= 3_000_000 &&
    value.maximumChangeComplexity > 0 &&
    value.favorableCaptureEfficiencyQ <= 1_000_000 &&
    value.maintenanceCostQPerHour >= 0n &&
    value.chargedReserveCapacityQ > 0n &&
    value.minimumReproductionHealthQ <= 1_000_000 &&
    value.activeReactionIds.length > 0 &&
    value.activeReactionIds.every((id, index) => id > 0 &&
      (index === 0 || id > value.activeReactionIds[index - 1])) &&
    value.recurringCosts.length > 0 &&
    value.recurringCosts.every((cost, index) =>
      cost.channel >= EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE &&
      cost.channel <= EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE &&
      cost.amountQPerOrganismHour >= 0n &&
      (index === 0 || cost.channel > value.recurringCosts[index - 1].channel)) &&
    value.recurringCosts.some((cost) =>
      cost.channel === EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE &&
      cost.amountQPerOrganismHour === value.maintenanceCostQPerHour);
}

function phenotypeEqual(
  left: EvolutionPhenotypeSummary,
  right: EvolutionPhenotypeSummary,
): boolean {
  return left.mutationIncomeModifierQ === right.mutationIncomeModifierQ &&
    left.maximumChangeComplexity === right.maximumChangeComplexity &&
    left.resourceConservation === right.resourceConservation &&
    left.maximumCaptureExtentsPerHour === right.maximumCaptureExtentsPerHour &&
    left.favorableCaptureEfficiencyQ === right.favorableCaptureEfficiencyQ &&
    left.requiresLight === right.requiresLight &&
    left.maintenanceCostQPerHour === right.maintenanceCostQPerHour &&
    left.chargedReserveCapacityQ === right.chargedReserveCapacityQ &&
    left.minimumReproductionHealthQ === right.minimumReproductionHealthQ &&
    sameNumbers(left.activeReactionIds, right.activeReactionIds) &&
    left.recurringCosts.length === right.recurringCosts.length &&
    left.recurringCosts.every((cost, index) =>
      cost.channel === right.recurringCosts[index].channel &&
      cost.amountQPerOrganismHour === right.recurringCosts[index].amountQPerOrganismHour);
}

function sameNumbers(left: readonly number[], right: readonly number[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function invalidTileActivationEvidence(proposal: SpeciationProposal): boolean {
  const founders = new Map(proposal.founderCounts.map((value) => [value.tileId, value.count]));
  const tileIds = new Set<number>();
  if (proposal.tileActivationEvidence.length !== founders.size) return true;
  for (const [tileIndex, tile] of proposal.tileActivationEvidence.entries()) {
    if (!founders.has(tile.tileId) ||
        founders.get(tile.tileId) !== tile.founderCount ||
        tile.founderCount === 0 ||
        tile.averageHealthQ > 1_000_000 ||
        tile.averageReserveQ > 1_000_000 ||
        tile.averageEnvironmentalFactorQ > 1_000_000 ||
        tile.averageResourcePressureQ > 1_000_000 ||
        tile.currentSurfaceMoistureQ > 1_000_000 ||
        tile.currentAccessibleLightQ > 1_000_000 ||
        (!tile.hasCurrentClimate && (tile.currentTemperatureMilliC !== 0 ||
          tile.currentSurfaceMoistureQ !== 0 || tile.currentAccessibleLightQ !== 0)) ||
        tileIds.has(tile.tileId) ||
        (tileIndex > 0 && tile.tileId <= proposal.tileActivationEvidence[tileIndex - 1].tileId)) {
      return true;
    }
    tileIds.add(tile.tileId);
    if (tile.reactionOpportunities.some((reaction, reactionIndex) =>
        reaction.reactionId === 0 ||
        reaction.status < EvolutionReactionOpportunityStatus.AVAILABLE ||
        reaction.status > EvolutionReactionOpportunityStatus.INACCESSIBLE_LIGHT ||
        reaction.stockSupportedExtents < 0n ||
        (reactionIndex > 0 && reaction.reactionId <=
          tile.reactionOpportunities[reactionIndex - 1].reactionId) ||
        (reaction.status === EvolutionReactionOpportunityStatus.RESOURCE_LIMITED &&
          (reaction.stockSupportedExtents !== 0n || reaction.limitingResourceId === 0)) ||
        reaction.inputs.length === 0 ||
        reaction.inputs.some((input, inputIndex) =>
          input.resourceId === 0 ||
          input.tileStockQ < 0n ||
          input.accessibleStockQ < 0n ||
          input.accessibleStockQ > input.tileStockQ ||
          input.requiredPerExtentQ <= 0n ||
          (input.inexhaustible
            ? input.flowForecast !== undefined
            : invalidResourceFlowForecast(input.flowForecast)) ||
          (inputIndex > 0 && input.resourceId <= reaction.inputs[inputIndex - 1].resourceId)) ||
        (reaction.limitingResourceId !== 0 &&
          !reaction.inputs.some((input) => input.resourceId === reaction.limitingResourceId)))) {
      return true;
    }
  }
  return false;
}

function invalidResourceFlowForecast(
  forecast: EvolutionResourceFlowForecast | undefined,
): boolean {
  if (forecast === undefined ||
      forecast.status < EvolutionResourceFlowForecastStatus.NO_HISTORY ||
      forecast.status > EvolutionResourceFlowForecastStatus.NO_RECENT_RENEWAL ||
      forecast.recentEnvironmentalInflowQ < 0n ||
      forecast.recentEnvironmentalOutflowQ < 0n ||
      forecast.recentOrganismUptakeQ < 0n ||
      forecast.proposedCohortDemandQ < 0n ||
      forecast.latestControlledSpeciesUptakeQ < 0n ||
      forecast.latestObservedCompetitorUptakeQ < 0n) {
    return true;
  }
  const latestUptake = forecast.latestControlledSpeciesUptakeQ +
    forecast.latestObservedCompetitorUptakeQ;
  if (forecast.historyPeriodHours === 0) {
    return forecast.status !== EvolutionResourceFlowForecastStatus.NO_HISTORY ||
      forecast.recentEnvironmentalInflowQ !== 0n ||
      forecast.recentEnvironmentalOutflowQ !== 0n ||
      forecast.recentOrganismUptakeQ !== 0n ||
      forecast.proposedCohortDemandQ !== 0n ||
      forecast.latestObservationPeriodHours !== 0 ||
      latestUptake !== 0n;
  }
  if (forecast.status === EvolutionResourceFlowForecastStatus.NO_HISTORY ||
      forecast.latestObservationPeriodHours === 0 ||
      forecast.latestObservationPeriodHours > forecast.historyPeriodHours ||
      latestUptake > forecast.recentOrganismUptakeQ) {
    return true;
  }
  const netRenewal = forecast.recentEnvironmentalInflowQ >
    forecast.recentEnvironmentalOutflowQ
    ? forecast.recentEnvironmentalInflowQ - forecast.recentEnvironmentalOutflowQ
    : 0n;
  return forecast.status === EvolutionResourceFlowForecastStatus.WITHIN_RECENT_RENEWAL
    ? forecast.proposedCohortDemandQ > netRenewal
    : forecast.status === EvolutionResourceFlowForecastStatus.EXCEEDS_RECENT_RENEWAL
      ? netRenewal === 0n || forecast.proposedCohortDemandQ <= netRenewal
      : netRenewal !== 0n || forecast.proposedCohortDemandQ === 0n;
}

export function isTraitReachable(
  surface: EvolutionDecisionSurface,
  traitId: number,
  visited: ReadonlySet<number> = new Set(),
): boolean {
  if (visited.has(traitId)) return false;
  const trait = surface.traits.find((candidate) => candidate.traitId === traitId);
  if (trait === undefined) return false;
  if (trait.acquired) return true;
  if (!trait.selectable) return false;
  const nextVisited = new Set(visited).add(traitId);
  return trait.prerequisiteTraitIds.every((id) =>
    isTraitReachable(surface, id, nextVisited));
}

export function createProposalRequest(
  surface: EvolutionDecisionSurface,
  traitIds: ReadonlySet<number>,
  tileIds: ReadonlySet<number>,
): SpeciationProposalRequest {
  return create(SpeciationProposalRequestSchema, {
    worldId: surface.worldId,
    ancestorSpeciesId: surface.controlledSpeciesId,
    expectedEvolutionRevision: surface.evolutionRevision,
    expectedGenomeHash: surface.genomeHash,
    newTraitIds: [...traitIds].sort((left, right) => left - right),
    selectedTileIds: [...tileIds].sort((left, right) => left - right),
    followDescendantIfPermitted: true,
  });
}
