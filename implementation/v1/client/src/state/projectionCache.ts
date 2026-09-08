import { create } from "@bufbuild/protobuf";
import {
  ActorWorldProjectionSchema,
  AcquisitionGateReason,
  AcquisitionProcess,
  GameLossReason,
  GameMode,
  GameRunStatus,
  OrganismActionGateReason,
  OrganismActionProcess,
  OrganismJourneyEventFamily,
  ResourceBiologicalForm,
  ResourceEnvironmentalPhase,
  ResourceFlowKind,
  type AcquisitionGateEvidence,
  type OrganismActionGateEvidence,
  type ActorWorldProjection,
  type ProjectionBatch,
  type ProjectionSnapshot,
  type SpeciesProjection,
} from "../generated/lyfe/v1/projection_pb";

export interface ProjectionCache {
  readonly projectionStreamId: bigint;
  readonly streamRevision: bigint;
  readonly world: ActorWorldProjection;
}

export type ProjectionApplyResult =
  | { readonly status: "applied"; readonly cache: ProjectionCache }
  | { readonly status: "duplicate"; readonly cache: ProjectionCache }
  | { readonly status: "resync-required"; readonly cache: ProjectionCache; readonly reason: string };

export function createProjectionCache(snapshot: ProjectionSnapshot): ProjectionCache {
  if (
    snapshot.projectionStreamId === 0n ||
    snapshot.streamRevision === 0n ||
    snapshot.projection === undefined
  ) {
    throw new Error("Projection snapshot identity, revision, and world are required.");
  }

  validateWorld(snapshot.projection);
  return {
    projectionStreamId: snapshot.projectionStreamId,
    streamRevision: snapshot.streamRevision,
    world: snapshot.projection,
  };
}

export function applyProjectionBatch(
  cache: ProjectionCache,
  batch: ProjectionBatch,
): ProjectionApplyResult {
  if (
    batch.projectionStreamId !== cache.projectionStreamId ||
    batch.worldId !== cache.world.worldId ||
    batch.worldRulesHash !== cache.world.worldRulesHash
  ) {
    return resync(cache, "stream, world, or rules identity mismatch");
  }

  if (batch.targetStreamRevision <= cache.streamRevision) {
    return { status: "duplicate", cache };
  }

  if (
    batch.baseStreamRevision !== cache.streamRevision ||
    batch.targetStreamRevision !== batch.baseStreamRevision + 1n ||
    batch.fromExclusiveTick !== cache.world.completedTick ||
    batch.throughCompletedTick < batch.fromExclusiveTick ||
    batch.worldRevision < cache.world.worldRevision ||
    batch.simulatedHours < cache.world.simulatedHours
  ) {
    return resync(cache, "projection revision or boundary gap");
  }
  if (batch.gameplay === undefined) {
    return resync(cache, "gameplay state is missing");
  }

  const priorJourneyEvents = cache.world.journeyEvents;
  const appendedJourneyEvents = batch.journeyEventAppends;
  const priorLastEventId = priorJourneyEvents.at(-1)?.eventId ?? 0n;
  if (
    appendedJourneyEvents.some((event, index) =>
      event.eventId <= (index === 0
        ? priorLastEventId
        : appendedJourneyEvents[index - 1].eventId) ||
      event.tick <= batch.fromExclusiveTick ||
      event.tick > batch.throughCompletedTick)
  ) {
    return resync(cache, "journey event append is duplicated, unordered, or outside the batch");
  }

  const tiles = indexBy(cache.world.tiles, (tile) => tile.tileId);
  const tileReplacements = unique(batch.tileReplacements, (tile) => tile.tileId);
  const removedTileIds = uniqueValues(batch.removedTileIds);
  if (
    tileReplacements === null ||
    removedTileIds === null ||
    intersects(tileReplacements.keys(), removedTileIds)
  ) {
    return resync(cache, "ambiguous tile operations");
  }

  const species = indexBy(cache.world.species, (item) => item.speciesId);
  const speciesReplacements = unique(batch.speciesReplacements, (item) => item.speciesId);
  const removedSpeciesIds = uniqueValues(batch.removedSpeciesIds);
  if (
    speciesReplacements === null ||
    removedSpeciesIds === null ||
    intersects(speciesReplacements.keys(), removedSpeciesIds)
  ) {
    return resync(cache, "ambiguous species operations");
  }

  for (const id of removedTileIds) tiles.delete(id);
  for (const [id, tile] of tileReplacements) tiles.set(id, tile);
  for (const id of removedSpeciesIds) species.delete(id);
  for (const [id, item] of speciesReplacements) species.set(id, item);
  if (!species.has(batch.gameplay.controlledSpeciesId)) {
    return resync(cache, "controlled species disappeared from projection");
  }

  const world = create(ActorWorldProjectionSchema, {
    ...cache.world,
    completedTick: batch.throughCompletedTick,
    worldRevision: batch.worldRevision,
    simulatedHours: batch.simulatedHours,
    lifecycle: batch.lifecycle,
    controlledSpeciesId: batch.gameplay.controlledSpeciesId,
    gameplay: batch.gameplay,
    tiles: [...tiles.values()].sort((left, right) => left.tileId - right.tileId),
    species: [...species.values()].sort(compareSpeciesIds),
    journeyEvents: [...priorJourneyEvents, ...appendedJourneyEvents],
    routineActivitySummaries: batch.routineActivitySummaries,
    activityPulseEvents: batch.activityPulseEvents,
  });
  try {
    validateWorld(world);
  } catch {
    return resync(cache, "replacement produced an invalid projection");
  }
  return {
    status: "applied",
    cache: {
      projectionStreamId: cache.projectionStreamId,
      streamRevision: batch.targetStreamRevision,
      world,
    },
  };
}

function validateWorld(world: ActorWorldProjection) {
  const tiles = unique(world.tiles, (tile) => tile.tileId);
  const species = unique(world.species, (item) => item.speciesId);
  const resources = unique(world.resourceDefinitions, (resource) => resource.resourceId);
  const gameplay = world.gameplay;
  const roots = gameplay === undefined
    ? null
    : unique(gameplay.roots, (root) => root.speciesId);
  const locks = gameplay === undefined
    ? null
    : uniqueValues(gameplay.mutationLockedSpeciesIds);
  const journeyEventIds = unique(world.journeyEvents, (event) => event.eventId);
  const pulseEventIds = unique(world.activityPulseEvents, (event) => event.eventId);
  if (
    world.worldId === 0n ||
    world.tickDurationHours === 0 ||
    world.width === 0 ||
    world.height === 0 ||
    world.worldRulesHash.length === 0 ||
    world.controlledSpeciesId === 0n ||
    gameplay === undefined ||
    (gameplay.mode !== GameMode.FREE_SANDBOX && gameplay.mode !== GameMode.SURVIVAL) ||
    (gameplay.runStatus !== GameRunStatus.ACTIVE &&
      gameplay.runStatus !== GameRunStatus.LOST) ||
    (gameplay.lossReason !== GameLossReason.UNSPECIFIED &&
      gameplay.lossReason !== GameLossReason.ALL_LIFE_EXTINCT &&
      gameplay.lossReason !== GameLossReason.CONTROLLED_SPECIES_EXTINCT) ||
    gameplay.controlledSpeciesId !== world.controlledSpeciesId ||
    gameplay.gameplayRevision === 0n ||
    roots === null ||
    roots.size !== (gameplay.mode === GameMode.FREE_SANDBOX ? 1 : 2) ||
    [...roots.values()].filter((root) => root.playerSelected).length !== 1 ||
    [...roots.values()].some((root) =>
      root.speciesId === 0n || root.founderGenomeId === 0 || root.initialPopulation === 0) ||
    locks === null ||
    locks.has(0n) ||
    journeyEventIds === null ||
    world.journeyEvents.some((event, index) =>
      (index > 0 && event.eventId <= world.journeyEvents[index - 1].eventId) ||
      invalidJourneyEvent(event, world)) ||
    pulseEventIds === null ||
    resources === null ||
    world.resourceDefinitions.some((resource, index) =>
      resource.resourceId === 0 ||
      resource.stableKey.length === 0 ||
      resource.displayName.length === 0 ||
      resource.biologicalForm < ResourceBiologicalForm.INORGANIC ||
      resource.biologicalForm > ResourceBiologicalForm.BOUNDARY ||
      resource.environmentalPhase < ResourceEnvironmentalPhase.GAS ||
      resource.environmentalPhase > ResourceEnvironmentalPhase.PARTICULATE ||
      (index > 0 && resource.resourceId <= world.resourceDefinitions[index - 1].resourceId)) ||
    world.tiles.some((tile) => tile.detail.case === "live" && (
      tile.detail.value.resourceFlowPeriodHours === 0 ||
      unique(tile.detail.value.resourceStocks, (stock) => stock.resourceId) === null ||
      tile.detail.value.resourceStocks.some((stock) =>
        stock.resourceId === 0 || stock.quantityQ < 0n || !resources.has(stock.resourceId)) ||
      unique(tile.detail.value.resourceFlows,
        (flow) => `${flow.resourceId}:${flow.kind}`) === null ||
      tile.detail.value.resourceFlows.some((flow) =>
        !resources.has(flow.resourceId) ||
        flow.amountQ <= 0n ||
        flow.kind < ResourceFlowKind.ENVIRONMENTAL_SOURCE ||
        flow.kind > ResourceFlowKind.ORGANISM_RELEASE) ||
      invalidResourceFlowHistory(
        tile.detail.value.resourceFlowHistory,
        tile.detail.value.resourceFlows,
        world.completedTick,
        world.simulatedHours,
        world.tickDurationHours,
        resources) ||
      tile.detail.value.organisms.some((organism) =>
        unique(organism.resourceAcquisitionEvidence,
          (evidence) => evidence.resourceId) === null ||
        organism.resourceAcquisitionEvidence.some((evidence, index) =>
          !resources.has(evidence.resourceId) ||
          evidence.requestedQ <= 0n ||
          evidence.grantedQ < 0n ||
          evidence.grantedQ > evidence.requestedQ ||
          (index > 0 && evidence.resourceId <=
            organism.resourceAcquisitionEvidence[index - 1].resourceId)) ||
        invalidAcquisitionGateEvidence(
          organism.acquisitionGateEvidence,
          world.completedTick) ||
        invalidOrganismActionGateEvidence(
          organism.actionGateEvidence,
          world.completedTick,
          resources)))) ||
    world.activityPulseEvents.some((event, index) =>
      (index > 0 && event.eventId <= world.activityPulseEvents[index - 1].eventId) ||
      invalidJourneyEvent(event, world)) ||
    world.routineActivitySummaries.some((summary) =>
      summary.periodHours === 0 ||
      summary.subjectOrganismId === 0n ||
      summary.subjectSpeciesId !== world.controlledSpeciesId ||
      summary.tileId >= world.width * world.height ||
      summary.resourceAcquisitions.length === 0 ||
      summary.resourceAcquisitions.some((resource, index) =>
        resource.resourceId === 0 ||
        resource.amountQ <= 0n ||
        (index > 0 && resource.resourceId <=
          summary.resourceAcquisitions[index - 1].resourceId))) ||
    (gameplay.runStatus === GameRunStatus.ACTIVE &&
      (gameplay.lossReason !== GameLossReason.UNSPECIFIED || gameplay.ended)) ||
    (gameplay.runStatus === GameRunStatus.LOST &&
      (gameplay.lossReason === GameLossReason.UNSPECIFIED || !gameplay.ended)) ||
    (gameplay.mode === GameMode.FREE_SANDBOX && gameplay.runStatus === GameRunStatus.LOST &&
      gameplay.lossReason !== GameLossReason.ALL_LIFE_EXTINCT) ||
    (gameplay.mode === GameMode.SURVIVAL && gameplay.runStatus === GameRunStatus.LOST &&
      gameplay.lossReason !== GameLossReason.CONTROLLED_SPECIES_EXTINCT) ||
    tiles === null ||
    species === null ||
    !species.has(world.controlledSpeciesId)
  ) {
    throw new Error("Projection world is structurally invalid.");
  }
}

function invalidResourceFlowHistory(
  history: readonly {
    readonly completedTick: bigint;
    readonly endSimulatedHour: bigint;
    readonly periodHours: number;
    readonly resourceFlows: readonly {
      readonly resourceId: number;
      readonly kind: ResourceFlowKind;
      readonly amountQ: bigint;
    }[];
  }[],
  currentFlows: readonly {
    readonly resourceId: number;
    readonly kind: ResourceFlowKind;
    readonly amountQ: bigint;
  }[],
  completedTick: bigint,
  simulatedHours: bigint,
  tickDurationHours: number,
  resources: Map<number, unknown>,
): boolean {
  if ((completedTick === 0n) !== (history.length === 0) || history.length > 168) return true;
  for (let index = 0; index < history.length; index += 1) {
    const interval = history[index];
    const prior = history[index - 1];
    if (
      interval.completedTick === 0n ||
      interval.periodHours !== tickDurationHours ||
      interval.endSimulatedHour !== interval.completedTick * BigInt(tickDurationHours) ||
      (prior !== undefined && (
        interval.completedTick !== prior.completedTick + 1n ||
        interval.endSimulatedHour !== prior.endSimulatedHour + BigInt(tickDurationHours)
      )) ||
      unique(interval.resourceFlows, (flow) => `${flow.resourceId}:${flow.kind}`) === null ||
      interval.resourceFlows.some((flow) =>
        !resources.has(flow.resourceId) ||
        flow.amountQ <= 0n ||
        flow.kind < ResourceFlowKind.ENVIRONMENTAL_SOURCE ||
        flow.kind > ResourceFlowKind.ORGANISM_RELEASE)
    ) return true;
  }
  const last = history.at(-1);
  const first = history[0];
  return last !== undefined && (
    last.completedTick !== completedTick ||
    last.endSimulatedHour !== simulatedHours ||
    simulatedHours - (first.endSimulatedHour - BigInt(first.periodHours)) > 168n ||
    last.resourceFlows.length !== currentFlows.length ||
    last.resourceFlows.some((flow, index) => {
      const current = currentFlows[index];
      return current === undefined ||
        flow.resourceId !== current.resourceId ||
        flow.kind !== current.kind ||
        flow.amountQ !== current.amountQ;
    })
  );
}

function invalidAcquisitionGateEvidence(
  values: readonly AcquisitionGateEvidence[],
  completedTick: bigint,
): boolean {
  const seen = new Set<string>();
  let priorProcess = AcquisitionProcess.UNSPECIFIED;
  let priorReason = AcquisitionGateReason.UNSPECIFIED;
  return values.some((value) => {
    const key = `${value.process}:${value.reason}`;
    const quantityGate = value.reason === AcquisitionGateReason.INTERNAL_CAPACITY ||
      value.reason === AcquisitionGateReason.INSUFFICIENT_ACTION_ENERGY;
    const invalidAmounts = quantityGate
      ? value.requiredQ <= 0n || value.availableQ < 0n ||
        value.availableQ >= value.requiredQ || value.clearsAtTick !== 0n
      : value.reason === AcquisitionGateReason.COOLDOWN_ACTIVE
        ? value.availableQ !== 0n || value.requiredQ !== 0n ||
          value.clearsAtTick <= completedTick
        : value.availableQ !== 0n || value.requiredQ !== 0n ||
          value.clearsAtTick !== 0n;
    const invalidPair = value.process === AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE
      ? value.reason === AcquisitionGateReason.COOLDOWN_ACTIVE ||
        value.reason === AcquisitionGateReason.INSUFFICIENT_ACTION_ENERGY
      : value.process === AcquisitionProcess.SCAVENGING
        ? value.reason === AcquisitionGateReason.INACCESSIBLE_LIGHT ||
          value.reason === AcquisitionGateReason.ENVIRONMENTAL_OPPORTUNITY
        : true;
    const invalid =
      value.process < AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE ||
      value.process > AcquisitionProcess.SCAVENGING ||
      value.reason < AcquisitionGateReason.MISSING_CAPABILITY ||
      value.reason > AcquisitionGateReason.INSUFFICIENT_ACTION_ENERGY ||
      invalidPair ||
      seen.has(key) ||
      (priorProcess !== AcquisitionProcess.UNSPECIFIED &&
        (value.process < priorProcess ||
          (value.process === priorProcess && value.reason <= priorReason))) ||
      invalidAmounts;
    seen.add(key);
    priorProcess = value.process;
    priorReason = value.reason;
    return invalid;
  });
}

function invalidOrganismActionGateEvidence(
  values: readonly OrganismActionGateEvidence[],
  completedTick: bigint,
  resources: ReadonlyMap<number, unknown>,
): boolean {
  const seen = new Set<OrganismActionProcess>();
  let priorProcess = OrganismActionProcess.UNSPECIFIED;
  return values.length > 2 || values.some((value) => {
    const zeroPayload = value.reason === OrganismActionGateReason.MISSING_CAPABILITY ||
      value.reason === OrganismActionGateReason.BEHAVIOR_SUPPRESSED ||
      value.reason === OrganismActionGateReason.LIFECYCLE_INELIGIBLE;
    const resourcePayload =
      value.reason === OrganismActionGateReason.CONSTITUTIVE_MICRONUTRIENT_QUOTA_MISSING ||
      value.reason === OrganismActionGateReason.OFFSPRING_MICRONUTRIENT_QUOTA_MISSING ||
      value.reason === OrganismActionGateReason.RESOURCE_SUPPLY;
    const invalidPayload = value.reason === OrganismActionGateReason.COOLDOWN_ACTIVE
      ? value.availableQ !== 0n || value.requiredQ !== 0n || value.resourceId !== 0 ||
        value.clearsAtTick <= completedTick
      : zeroPayload
        ? value.availableQ !== 0n || value.requiredQ !== 0n || value.resourceId !== 0 ||
          value.clearsAtTick !== 0n
        : value.requiredQ <= 0n || value.availableQ < 0n ||
          value.availableQ >= value.requiredQ || value.clearsAtTick !== 0n ||
          (resourcePayload ? !resources.has(value.resourceId) : value.resourceId !== 0);
    const validPair = value.process === OrganismActionProcess.BIOMASS_GROWTH
      ? value.reason === OrganismActionGateReason.MISSING_CAPABILITY ||
        value.reason === OrganismActionGateReason.BEHAVIOR_SUPPRESSED ||
        value.reason === OrganismActionGateReason.MAINTENANCE_SHORTFALL ||
        value.reason === OrganismActionGateReason.RESERVE_PROTECTION_FLOOR ||
        value.reason === OrganismActionGateReason.INTERNAL_CAPACITY ||
        value.reason === OrganismActionGateReason.RESOURCE_SUPPLY ||
        value.reason === OrganismActionGateReason.CLAIM_CONTENTION
      : value.process === OrganismActionProcess.REPRODUCTION
        ? value.reason === OrganismActionGateReason.BEHAVIOR_SUPPRESSED ||
          value.reason === OrganismActionGateReason.COOLDOWN_ACTIVE ||
          value.reason === OrganismActionGateReason.HEALTH_BELOW_MINIMUM ||
          value.reason === OrganismActionGateReason.STRUCTURE_BELOW_MINIMUM ||
          value.reason === OrganismActionGateReason.RESERVE_BELOW_MINIMUM ||
          value.reason ===
            OrganismActionGateReason.CONSTITUTIVE_MICRONUTRIENT_QUOTA_MISSING ||
          value.reason === OrganismActionGateReason.OFFSPRING_MICRONUTRIENT_QUOTA_MISSING ||
          value.reason === OrganismActionGateReason.LIFECYCLE_INELIGIBLE
        : false;
    const invalid = !validPair ||
      value.reason < OrganismActionGateReason.MISSING_CAPABILITY ||
      value.reason > OrganismActionGateReason.LIFECYCLE_INELIGIBLE ||
      seen.has(value.process) ||
      (priorProcess !== OrganismActionProcess.UNSPECIFIED && value.process <= priorProcess) ||
      invalidPayload;
    seen.add(value.process);
    priorProcess = value.process;
    return invalid;
  });
}

function invalidJourneyEvent(
  event: ActorWorldProjection["journeyEvents"][number],
  world: ActorWorldProjection,
): boolean {
  return event.eventId === 0n ||
    event.tick > world.completedTick ||
    event.subjectOrganismId === 0n ||
    event.subjectSpeciesId !== world.controlledSpeciesId ||
    event.tileId >= world.width * world.height ||
    event.family < OrganismJourneyEventFamily.BIRTH ||
    event.family > OrganismJourneyEventFamily.BEHAVIOR_TRANSITION ||
    event.amountQ < 0n ||
    event.deathCauseProbabilities.some((cause) => cause.probabilityQ > 1_000_000);
}

function indexBy<T, TKey>(values: readonly T[], key: (value: T) => TKey): Map<TKey, T> {
  return new Map(values.map((value) => [key(value), value]));
}

function unique<T, TKey>(
  values: readonly T[],
  key: (value: T) => TKey,
): Map<TKey, T> | null {
  const result = new Map<TKey, T>();
  for (const value of values) {
    const id = key(value);
    if (result.has(id)) return null;
    result.set(id, value);
  }
  return result;
}

function uniqueValues<T>(values: readonly T[]): Set<T> | null {
  const result = new Set(values);
  return result.size === values.length ? result : null;
}

function intersects<T>(left: Iterable<T>, right: Set<T>): boolean {
  for (const value of left) if (right.has(value)) return true;
  return false;
}

function compareSpeciesIds(left: SpeciesProjection, right: SpeciesProjection): number {
  return left.speciesId < right.speciesId ? -1 : left.speciesId > right.speciesId ? 1 : 0;
}

function resync(cache: ProjectionCache, reason: string): ProjectionApplyResult {
  return { status: "resync-required", cache, reason };
}
