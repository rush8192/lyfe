import { create } from "@bufbuild/protobuf";
import {
  ActorWorldProjectionSchema,
  AttentionAlertClass,
  AttentionAlertKind,
  AcquisitionGateReason,
  AcquisitionProcess,
  GameLossReason,
  GameMode,
  GameRunStatus,
  LineageReviewEvidenceKind,
  LineageReviewEvidenceReferenceKind,
  LineageReviewCapabilityKind,
  LineageReviewLandmarkKind,
  NotableEventFamily,
  NotableEventSignificance,
  OrganismBehavior,
  OrganismActionGateReason,
  OrganismActionProcess,
  OrganismJourneyEventFamily,
  ResourceBiologicalForm,
  ResourceEnvironmentalPhase,
  ResourceFlowKind,
  ResourceFlowProcess,
  SpeciesPopulationScope,
  WorldLifecycle,
  type AcquisitionGateEvidence,
  type OrganismActionGateEvidence,
  type ActorWorldProjection,
  type AttentionAlert,
  type ProjectionBatch,
  type ProjectionSnapshot,
  type LineageReviewLandmark,
  type NotableEvent,
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
  const priorLandmarks = cache.world.lineageReviewLandmarks;
  const appendedLandmarks = batch.lineageReviewLandmarkAppends;
  const priorLastLandmarkId = priorLandmarks.at(-1)?.eventId ?? 0n;
  if (
    appendedLandmarks.some((event, index) =>
      event.eventId <= (index === 0
        ? priorLastLandmarkId
        : appendedLandmarks[index - 1].eventId) ||
      event.completedTick <= batch.fromExclusiveTick ||
      event.completedTick > batch.throughCompletedTick)
  ) {
    return resync(cache, "lineage-review landmark append is duplicated, unordered, or outside the batch");
  }
  const priorNotableEvents = cache.world.notableEvents;
  const appendedNotableEvents = batch.notableEventAppends;
  const priorLastNotableEventId = priorNotableEvents.at(-1)?.eventId ?? 0n;
  if (
    appendedNotableEvents.some((event, index) =>
      event.eventId <= (index === 0
        ? priorLastNotableEventId
        : appendedNotableEvents[index - 1].eventId) ||
      event.completedTick < batch.fromExclusiveTick ||
      event.completedTick > batch.throughCompletedTick)
  ) {
    return resync(cache, "notable event append is duplicated, unordered, or outside the batch");
  }
  const priorAttentionAlerts = cache.world.attentionAlerts;
  const appendedAttentionAlerts = batch.attentionAlertAppends;
  const priorLastAlertId = priorAttentionAlerts.at(-1)?.alertId ?? 0n;
  if (
    appendedAttentionAlerts.some((alert, index) =>
      alert.alertId <= (index === 0
        ? priorLastAlertId
        : appendedAttentionAlerts[index - 1].alertId) ||
      alert.completedTick < batch.fromExclusiveTick ||
      alert.completedTick > batch.throughCompletedTick)
  ) {
    return resync(cache, "attention alert append is duplicated, unordered, or outside the batch");
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
    lineageReviewLandmarks: [...priorLandmarks, ...appendedLandmarks],
    notableEvents: [...priorNotableEvents, ...appendedNotableEvents],
    attentionAlerts: [...priorAttentionAlerts, ...appendedAttentionAlerts],
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
  const reactions = unique(world.reactionDefinitions, (reaction) => reaction.reactionId);
  const gameplay = world.gameplay;
  const roots = gameplay === undefined
    ? null
    : unique(gameplay.roots, (root) => root.speciesId);
  const locks = gameplay === undefined
    ? null
    : uniqueValues(gameplay.mutationLockedSpeciesIds);
  const journeyEventIds = unique(world.journeyEvents, (event) => event.eventId);
  const pulseEventIds = unique(world.activityPulseEvents, (event) => event.eventId);
  const lineageReviewLandmarkIds = unique(
    world.lineageReviewLandmarks,
    (event) => event.eventId,
  );
  const notableEventIds = unique(world.notableEvents, (event) => event.eventId);
  const chronicleEventIds = uniqueValues([
    ...world.lineageReviewLandmarks.map((event) => event.eventId),
    ...world.notableEvents.map((event) => event.eventId),
  ]);
  const notableDeduplicationKeys = unique(
    world.notableEvents,
    (event) => event.deduplicationKey,
  );
  const attentionAlertIds = unique(world.attentionAlerts, (alert) => alert.alertId);
  const attentionDeduplicationKeys = unique(
    world.attentionAlerts,
    (alert) => alert.deduplicationKey,
  );
  if (
    world.worldId === 0n ||
    world.tickDurationHours === 0 ||
    world.width === 0 ||
    world.height === 0 ||
    world.worldRulesHash.length === 0 ||
    (world.lifecycle !== WorldLifecycle.PAUSED_READY &&
      world.lifecycle !== WorldLifecycle.RUNNING) ||
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
    lineageReviewLandmarkIds === null ||
    world.lineageReviewLandmarks.some((event, index) =>
      (index > 0 && event.eventId <= world.lineageReviewLandmarks[index - 1].eventId) ||
      invalidLineageReviewLandmark(event, world)) ||
    notableEventIds === null ||
    chronicleEventIds === null ||
    notableDeduplicationKeys === null ||
    world.notableEvents.some((event, index) =>
      (index > 0 && event.eventId <= world.notableEvents[index - 1].eventId) ||
      invalidNotableEvent(event, world)) ||
    attentionAlertIds === null ||
    attentionDeduplicationKeys === null ||
    world.attentionAlerts.some((alert, index) =>
      (index > 0 && alert.alertId <= world.attentionAlerts[index - 1].alertId) ||
      invalidAttentionAlert(alert, world)) ||
    resources === null ||
    reactions === null ||
    world.resourceDefinitions.some((resource, index) =>
      resource.resourceId === 0 ||
      resource.stableKey.length === 0 ||
      resource.displayName.length === 0 ||
      resource.biologicalForm < ResourceBiologicalForm.INORGANIC ||
      resource.biologicalForm > ResourceBiologicalForm.BOUNDARY ||
      resource.environmentalPhase < ResourceEnvironmentalPhase.GAS ||
      resource.environmentalPhase > ResourceEnvironmentalPhase.PARTICULATE ||
      (index > 0 && resource.resourceId <= world.resourceDefinitions[index - 1].resourceId)) ||
    world.reactionDefinitions.some((reaction, index) =>
      reaction.reactionId === 0 ||
      reaction.stableKey.length === 0 ||
      reaction.displayName.length === 0 ||
      (index > 0 && reaction.reactionId <= world.reactionDefinitions[index - 1].reactionId)) ||
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
      invalidResourceFlowContributors(
        tile.detail.value.resourceFlowContributors,
        tile.detail.value.resourceFlows,
        resources,
        reactions,
        species ?? new Map<bigint, unknown>()) ||
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

function invalidNotableEvent(
  event: NotableEvent,
  world: ActorWorldProjection,
): boolean {
  const expectedSignificance = event.family === NotableEventFamily.SPECIES_EXTINCTION
      || event.family === NotableEventFamily.POPULATION_DANGER_THRESHOLD
      || event.family === NotableEventFamily.POPULATION_DECLINE_THRESHOLD
      || event.family === NotableEventFamily.SUSTAINED_LOW_HEALTH
      || event.family === NotableEventFamily.REALIZED_DEATH_MECHANISM
    ? NotableEventSignificance.CRITICAL
    : event.family === NotableEventFamily.FIRST_REPRODUCTION ||
        event.family === NotableEventFamily.POPULATION_MILESTONE
      ? NotableEventSignificance.INFORMATIONAL
      : NotableEventSignificance.STRATEGIC;
  let expectedKey = "";
  if (event.family === NotableEventFamily.SPECIATION &&
      event.sourceEventId > 0n && event.relatedSpeciesId !== undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.milestoneValue > 0n) {
    expectedKey = `speciation:${event.sourceEventId}`;
  } else if (event.family === NotableEventFamily.FIRST_REPRODUCTION &&
      event.sourceEventId > 0n && event.relatedSpeciesId === undefined &&
      event.tileId !== undefined && event.reactionId === undefined &&
      event.milestoneValue === 1n) {
    expectedKey = `first-reproduction:species:${event.speciesId}`;
  } else if (event.family === NotableEventFamily.POPULATION_MILESTONE &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      [100n, 250n, 500n, 1_000n, 2_500n, 5_000n, 10_000n]
        .includes(event.milestoneValue)) {
    expectedKey = `population:species:${event.speciesId}:threshold:${event.milestoneValue}`;
  } else if (event.family === NotableEventFamily.FIRST_TILE_OCCUPATION &&
      event.sourceEventId > 0n && event.relatedSpeciesId === undefined &&
      event.tileId !== undefined && event.reactionId === undefined &&
      event.milestoneValue > 0n) {
    expectedKey = `first-occupation:species:${event.speciesId}:tile:${event.tileId}`;
  } else if (event.family === NotableEventFamily.FIRST_REACTION_EXECUTION &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId !== undefined && event.reactionId !== undefined &&
      event.milestoneValue === 1n) {
    expectedKey = `first-reaction:species:${event.speciesId}:reaction:${event.reactionId}`;
  } else if (event.family === NotableEventFamily.SPECIES_EXTINCTION &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.milestoneValue > 0n) {
    expectedKey = `extinction:species:${event.speciesId}`;
  } else if (event.family === NotableEventFamily.POPULATION_DANGER_THRESHOLD &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.milestoneValue <= 10n && event.baselineValue === 0n) {
    if (hasCanonicalEpisodeKey(event, "population-danger")) {
      expectedKey = event.deduplicationKey;
    }
  } else if (event.family === NotableEventFamily.POPULATION_DECLINE_THRESHOLD &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.baselineValue > event.milestoneValue &&
      ((event.baselineValue - event.milestoneValue) * 1_000_000n) /
        event.baselineValue >= 250_000n) {
    if (hasCanonicalEpisodeKey(event, "population-decline")) {
      expectedKey = event.deduplicationKey;
    }
  } else if (event.family === NotableEventFamily.SUSTAINED_LOW_HEALTH &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.milestoneValue < 250_000n && event.baselineValue >= 6n) {
    if (hasCanonicalEpisodeKey(event, "low-health")) {
      expectedKey = event.deduplicationKey;
    }
  } else if (event.family === NotableEventFamily.REALIZED_DEATH_MECHANISM &&
      event.sourceEventId > 0n && event.relatedSpeciesId === undefined &&
      event.tileId !== undefined && event.reactionId === undefined &&
      event.milestoneValue >= 1n && event.milestoneValue <= 7n &&
      event.baselineValue === 0n) {
    expectedKey = `death-mechanism:species:${event.speciesId}:cause:${event.milestoneValue}`;
  } else if (event.family === NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE &&
      event.sourceEventId === 0n && event.relatedSpeciesId === undefined &&
      event.tileId === undefined && event.reactionId === undefined &&
      event.milestoneValue >= 750_000n && event.baselineValue >= 6n) {
    if (hasCanonicalEpisodeKey(event, "resource-pressure")) {
      expectedKey = event.deduplicationKey;
    }
  }
  const permitsBaseline = event.family === NotableEventFamily.POPULATION_DECLINE_THRESHOLD ||
    event.family === NotableEventFamily.SUSTAINED_LOW_HEALTH ||
    event.family === NotableEventFamily.SUSTAINED_RESOURCE_PRESSURE;
  return event.eventId === 0n ||
    event.completedTick > world.completedTick ||
    event.simulatedHours > world.simulatedHours ||
    event.speciesId === 0n ||
    event.significanceRuleVersion !== 1 ||
    event.significance !== expectedSignificance ||
    (!permitsBaseline && event.baselineValue !== 0n) ||
    expectedKey.length === 0 ||
    event.deduplicationKey !== expectedKey;
}

function hasCanonicalEpisodeKey(event: NotableEvent, family: string): boolean {
  const prefix = `${family}:species:${event.speciesId}:episode:`;
  const suffix = event.deduplicationKey.startsWith(prefix)
    ? event.deduplicationKey.slice(prefix.length)
    : "";
  return /^[1-9][0-9]*$/.test(suffix) && BigInt(suffix) <= 4_294_967_295n;
}

function invalidAttentionAlert(
  alert: AttentionAlert,
  world: ActorWorldProjection,
): boolean {
  const allChronicleIds = new Set([
    ...world.notableEvents.map((event) => event.eventId),
    ...world.lineageReviewLandmarks.map((event) => event.eventId),
  ]);
  if (alert.alertId === 0n ||
      alert.completedTick > world.completedTick ||
      alert.simulatedHours > world.simulatedHours ||
      alert.speciesId !== world.controlledSpeciesId ||
      alert.chronicleEventIds.length === 0 || alert.chronicleEventIds.length > 256 ||
      uniqueValues(alert.chronicleEventIds) === null ||
      alert.chronicleEventIds.some((eventId) => !allChronicleIds.has(eventId))) {
    return true;
  }
  if (alert.kind === AttentionAlertKind.NOTABLE_EVENT_GROUP &&
      alert.eventFamily !== undefined) {
    const expected = world.notableEvents.filter((event) =>
      event.family === alert.eventFamily &&
      event.speciesId === alert.speciesId &&
      event.completedTick === alert.completedTick);
    const expectedClass = alert.eventFamily === NotableEventFamily.FIRST_REPRODUCTION ||
        alert.eventFamily === NotableEventFamily.POPULATION_MILESTONE
      ? AttentionAlertClass.INFORMATIONAL
      : alert.eventFamily === NotableEventFamily.SPECIES_EXTINCTION ||
          alert.eventFamily === NotableEventFamily.POPULATION_DANGER_THRESHOLD ||
          alert.eventFamily === NotableEventFamily.POPULATION_DECLINE_THRESHOLD ||
          alert.eventFamily === NotableEventFamily.SUSTAINED_LOW_HEALTH ||
          alert.eventFamily === NotableEventFamily.REALIZED_DEATH_MECHANISM
        ? AttentionAlertClass.CRITICAL
        : AttentionAlertClass.STRATEGIC;
    return expected.length === 0 || alert.alertClass !== expectedClass ||
      !alert.chronicleEventIds.every((eventId, index) => eventId === expected[index]?.eventId) ||
      expected.length !== alert.chronicleEventIds.length ||
      alert.deduplicationKey !==
        `notable:${alert.eventFamily}:species:${alert.speciesId}:tick:${alert.completedTick}`;
  }
  if (alert.kind === AttentionAlertKind.LINEAGE_REVIEW_BOUNDARY &&
      alert.eventFamily === undefined &&
      alert.alertClass === AttentionAlertClass.STRATEGIC &&
      alert.chronicleEventIds.length === 1) {
    const eventId = alert.chronicleEventIds[0];
    const landmark = world.lineageReviewLandmarks.find((event) => event.eventId === eventId);
    return landmark === undefined || landmark.perspectiveSpeciesId !== alert.speciesId ||
      landmark.completedTick !== alert.completedTick ||
      alert.deduplicationKey !== `lineage-review:event:${eventId}`;
  }
  return true;
}

function invalidLineageReviewLandmark(
  event: LineageReviewLandmark,
  world: ActorWorldProjection,
): boolean {
  const observations = [
    event.perspectiveBaseline,
    event.comparisonBaseline,
    event.perspectiveCurrent,
    event.comparisonCurrent,
  ];
  const comparisonSpeciesId = event.perspectiveSpeciesId === event.ancestorSpeciesId
    ? event.descendantSpeciesId
    : event.ancestorSpeciesId;
  const references = event.evidenceReferences;
  const fromExclusiveTick = references[0]?.fromExclusiveTick;
  const referenceCount = (kind: LineageReviewEvidenceReferenceKind) =>
    references.filter((reference) => reference.kind === kind).length;
  const tileReferences = references.filter((reference) =>
    reference.kind === LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW);
  const capabilityActivations = event.capabilityActivations;
  const reactionActivations = event.reactionActivations;
  const reactionIds = new Set(world.reactionDefinitions.map((reaction) => reaction.reactionId));
  return event.eventId === 0n ||
    (event.kind !== LineageReviewLandmarkKind.COOLDOWN_BOUNDARY &&
      event.kind !== LineageReviewLandmarkKind.PROPOSAL_FOLLOW_UP) ||
    event.completedTick === 0n ||
    event.completedTick > world.completedTick ||
    event.simulatedHours > world.simulatedHours ||
    event.windowHours === 0n ||
    event.speciationEventId === 0n ||
    event.ancestorSpeciesId === 0n ||
    event.descendantSpeciesId === 0n ||
    (event.perspectiveSpeciesId !== event.ancestorSpeciesId &&
      event.perspectiveSpeciesId !== event.descendantSpeciesId) ||
    event.traitIds.some((traitId, index) =>
      traitId === 0 || (index > 0 && traitId <= event.traitIds[index - 1])) ||
    event.evidenceKind < LineageReviewEvidenceKind.GENERAL_OUTCOMES ||
    event.evidenceKind > LineageReviewEvidenceKind.RESERVE_STORAGE ||
    (event.kind === LineageReviewLandmarkKind.COOLDOWN_BOUNDARY &&
      (event.windowHours !== 168n ||
        event.evidenceKind !== LineageReviewEvidenceKind.GENERAL_OUTCOMES)) ||
    (event.kind === LineageReviewLandmarkKind.PROPOSAL_FOLLOW_UP &&
      event.windowHours <= 168n) ||
    event.perspectiveBaseline?.speciesId !== event.perspectiveSpeciesId ||
    event.perspectiveCurrent?.speciesId !== event.perspectiveSpeciesId ||
    event.comparisonBaseline?.speciesId !== comparisonSpeciesId ||
    event.comparisonCurrent?.speciesId !== comparisonSpeciesId ||
    event.perspectiveBaseline?.populationScope !== SpeciesPopulationScope.WORLD_EXACT ||
    event.perspectiveCurrent?.populationScope !== SpeciesPopulationScope.WORLD_EXACT ||
    event.comparisonBaseline?.populationScope !== SpeciesPopulationScope.LIVE_TILES_OBSERVED ||
    event.comparisonCurrent?.populationScope !== SpeciesPopulationScope.LIVE_TILES_OBSERVED ||
    event.perspectiveBaseline?.activityCountsAvailable === true ||
    event.comparisonBaseline?.activityCountsAvailable === true ||
    event.perspectiveCurrent?.activityCountsAvailable !== true ||
    event.comparisonCurrent?.activityCountsAvailable === true ||
    capabilityActivations.length > 16 ||
    capabilityActivations.some((activation, index) =>
      activation.kind !== LineageReviewCapabilityKind.RESOURCE_CONSERVATION ||
      activation.sourceTraitId === 0 ||
      (!activation.installed && activation.activationCount !== 0n) ||
      (activation.introducedByProposal && !activation.installed) ||
      (index > 0 && activation.kind <= capabilityActivations[index - 1].kind)) ||
    reactionActivations.length > 256 ||
    reactionActivations.some((activation, index) =>
      activation.reactionId === 0 ||
      !reactionIds.has(activation.reactionId) ||
      (!activation.installed && activation.activationCount !== 0n) ||
      (activation.introducedByProposal && !activation.installed) ||
      (index > 0 && activation.reactionId <=
        reactionActivations[index - 1].reactionId)) ||
    references.length < 4 ||
    references.length > 4_096 ||
    references[0].kind !== LineageReviewEvidenceReferenceKind.SPECIATION_DECISION ||
    references[1].kind !== LineageReviewEvidenceReferenceKind.REVIEWED_SPECIES_SUMMARY ||
    references[2].kind !== LineageReviewEvidenceReferenceKind.COMPARISON_SPECIES_SUMMARY ||
    references[3].kind !== LineageReviewEvidenceReferenceKind.REVIEWED_JOURNEY_WINDOW ||
    references.slice(4).some((reference) =>
      reference.kind !== LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW) ||
    referenceCount(LineageReviewEvidenceReferenceKind.SPECIATION_DECISION) !== 1 ||
    referenceCount(LineageReviewEvidenceReferenceKind.REVIEWED_SPECIES_SUMMARY) !== 1 ||
    referenceCount(LineageReviewEvidenceReferenceKind.COMPARISON_SPECIES_SUMMARY) !== 1 ||
    referenceCount(LineageReviewEvidenceReferenceKind.REVIEWED_JOURNEY_WINDOW) !== 1 ||
    fromExclusiveTick === undefined ||
    references.some((reference) =>
      reference.fromExclusiveTick !== fromExclusiveTick ||
      reference.fromExclusiveTick >= reference.throughCompletedTick ||
      reference.throughCompletedTick !== event.completedTick ||
      reference.kind < LineageReviewEvidenceReferenceKind.SPECIATION_DECISION ||
      reference.kind > LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW ||
      invalidLineageReviewEvidenceReference(
        reference,
        event.perspectiveSpeciesId,
        comparisonSpeciesId)) ||
    tileReferences.some((reference, index) =>
      index > 0 && reference.tileId! <= tileReferences[index - 1].tileId!) ||
    observations.some((observation) =>
      observation === undefined ||
      observation.speciesId === 0n ||
      (observation.populationScope !== 1 && observation.populationScope !== 2) ||
      observation.averageHealthQ > 1_000_000 ||
      observation.averageReserveQ > 1_000_000 ||
      observation.averageAcquisitionCoverageQ > 2_000_000 ||
      observation.averageResourcePressureQ > 1_000_000 ||
      invalidLineageReviewBehaviorCounts(observation) ||
      (!observation.activityCountsAvailable &&
        (observation.birthCount !== 0n || observation.deathCount !== 0n ||
          observation.migrationCount !== 0n)));
}

function invalidLineageReviewBehaviorCounts(
  observation: NonNullable<LineageReviewLandmark["perspectiveCurrent"]>,
): boolean {
  const counts = observation.behaviorCounts;
  return counts.length > 5 ||
    counts.some((count, index) =>
      count.behavior < OrganismBehavior.BASELINE ||
      count.behavior > OrganismBehavior.FLEEING ||
      count.count === 0n ||
      (index > 0 && count.behavior <= counts[index - 1].behavior)) ||
    counts.reduce((sum, count) => sum + count.count, 0n) !== observation.population;
}

function invalidLineageReviewEvidenceReference(
  reference: LineageReviewLandmark["evidenceReferences"][number],
  perspectiveSpeciesId: bigint,
  comparisonSpeciesId: bigint,
): boolean {
  switch (reference.kind) {
    case LineageReviewEvidenceReferenceKind.SPECIATION_DECISION:
      return reference.speciesId !== undefined || reference.tileId !== undefined;
    case LineageReviewEvidenceReferenceKind.REVIEWED_SPECIES_SUMMARY:
    case LineageReviewEvidenceReferenceKind.REVIEWED_JOURNEY_WINDOW:
      return reference.speciesId !== perspectiveSpeciesId || reference.tileId !== undefined;
    case LineageReviewEvidenceReferenceKind.COMPARISON_SPECIES_SUMMARY:
      return reference.speciesId !== comparisonSpeciesId || reference.tileId !== undefined;
    case LineageReviewEvidenceReferenceKind.LIVE_TILE_RESOURCE_WINDOW:
      return reference.speciesId !== perspectiveSpeciesId || reference.tileId === undefined;
    default:
      return true;
  }
}

function invalidResourceFlowContributors(
  contributors: readonly {
    readonly resourceId: number;
    readonly kind: ResourceFlowKind;
    readonly process: ResourceFlowProcess;
    readonly reactionId: number;
    readonly speciesId: bigint;
    readonly amountQ: bigint;
  }[],
  flows: readonly {
    readonly resourceId: number;
    readonly kind: ResourceFlowKind;
    readonly amountQ: bigint;
  }[],
  resources: ReadonlyMap<number, unknown>,
  reactions: ReadonlyMap<number, unknown>,
  species: ReadonlyMap<bigint, unknown>,
): boolean {
  const keys = new Set<string>();
  const totals = new Map<string, bigint>();
  for (const value of contributors) {
    const key = `${value.resourceId}:${value.kind}:${value.process}:${value.reactionId}:${value.speciesId}`;
    const totalKey = `${value.resourceId}:${value.kind}`;
    const usesReaction = value.process === ResourceFlowProcess.EXTERNAL_ENERGY_CAPTURE ||
      value.process === ResourceFlowProcess.PARTICULATE_DIGESTION ||
      value.process === ResourceFlowProcess.MANDATORY_MAINTENANCE ||
      value.process === ResourceFlowProcess.BIOMASS_ASSEMBLY;
    const environmental = value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SOURCE ||
      value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SINK ||
      value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_EXCHANGE;
    const validFlowKind = value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SOURCE
      ? value.kind === ResourceFlowKind.ENVIRONMENTAL_SOURCE
      : value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SINK
        ? value.kind === ResourceFlowKind.ENVIRONMENTAL_SINK
        : value.process === ResourceFlowProcess.ENVIRONMENTAL_GAS_EXCHANGE
          ? value.kind === ResourceFlowKind.NEIGHBOR_EXCHANGE_IN ||
            value.kind === ResourceFlowKind.NEIGHBOR_EXCHANGE_OUT
          : value.kind === ResourceFlowKind.ORGANISM_UPTAKE ||
            value.kind === ResourceFlowKind.ORGANISM_RELEASE;
    if (
      !resources.has(value.resourceId) ||
      value.kind < ResourceFlowKind.ENVIRONMENTAL_SOURCE ||
      value.kind > ResourceFlowKind.ORGANISM_RELEASE ||
      value.process < ResourceFlowProcess.EXTERNAL_ENERGY_CAPTURE ||
      value.process > ResourceFlowProcess.MICRONUTRIENT_UPTAKE ||
      value.amountQ <= 0n ||
      usesReaction !== (value.reactionId !== 0) ||
      (value.reactionId !== 0 && !reactions.has(value.reactionId)) ||
      (environmental && value.speciesId !== 0n) ||
      (value.speciesId !== 0n && !species.has(value.speciesId)) ||
      !validFlowKind ||
      keys.has(key)
    ) return true;
    keys.add(key);
    totals.set(totalKey, (totals.get(totalKey) ?? 0n) + value.amountQ);
  }
  return totals.size !== flows.length || flows.some((flow) =>
    totals.get(`${flow.resourceId}:${flow.kind}`) !== flow.amountQ);
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
