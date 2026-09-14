import { useState } from "react";
import type {
  EvolutionDecisionSurface,
  EvolutionHabitatProfile,
  EvolutionOccupiedTile,
  EvolutionRelevantResource,
} from "../generated/lyfe/v1/evolution_pb";
import {
  OrganismJourneyEventFamily,
  ResourceFlowKind,
  type ActorWorldProjection,
  type LiveTile,
  type ResourceDefinition,
  type TileProjection,
} from "../generated/lyfe/v1/projection_pb";
import { deathCauseCopy } from "./deathCauses";

interface TileInspectorPanelProps {
  readonly world: ActorWorldProjection;
  readonly evolution?: EvolutionDecisionSurface | null;
  readonly selectedTileId: number | null;
}

const DEATH_WINDOWS = [72, 168, 336, 720] as const;

export function TileInspectorPanel({ world, evolution = null, selectedTileId }: TileInspectorPanelProps) {
  const [deathWindowTicks, setDeathWindowTicks] = useState<number>(72);
  const tile = world.tiles.find((candidate) => candidate.tileId === selectedTileId);
  if (tile === undefined) return null;

  return (
    <section className={`tile-inspector ${tileState(tile)}`} aria-labelledby="tile-inspector-title">
      <header className="tile-inspector-header">
        <div>
          <p className="section-label">Selected location · {scopeLabel(tile)}</p>
          <h2 id="tile-inspector-title">Tile {tile.tileId}</h2>
          <p>Grid coordinate {tile.x}, {tile.y}</p>
        </div>
        <span className="tile-scope-badge">{scopeLabel(tile)}</span>
      </header>
      <TileInspectorBody
        world={world}
        evolution={evolution}
        tile={tile}
        deathWindowTicks={deathWindowTicks}
        onDeathWindowChange={setDeathWindowTicks}
      />
    </section>
  );
}

function TileInspectorBody({
  world,
  evolution,
  tile,
  deathWindowTicks,
  onDeathWindowChange,
}: {
  readonly world: ActorWorldProjection;
  readonly evolution: EvolutionDecisionSurface | null;
  readonly tile: TileProjection;
  readonly deathWindowTicks: number;
  readonly onDeathWindowChange: (value: number) => void;
}) {
  if (tile.detail.case === "unknown" || tile.detail.case === undefined) {
    return (
      <div className="tile-knowledge-message unknown">
        <strong>No observation is available</strong>
        <p>
          Geography, conditions, resources, organisms, and remnants are withheld because this
          location is outside the controlled lineage’s current or retained knowledge.
        </p>
      </div>
    );
  }

  if (tile.detail.case === "reduced") {
    const age = observationAge(
      world.completedTick,
      tile.detail.value.observedAtTick,
      world.tickDurationHours,
    );
    const knownResources = tile.detail.value.knownPresentResourceIds.map((resourceId) =>
      resourceName(world.resourceDefinitions, resourceId));
    return (
      <>
        <div className="tile-knowledge-message reduced">
          <strong>Retained observation — not current</strong>
          <p>
            Last observed at tick {tile.detail.value.observedAtTick.toLocaleString("en-US")};
            {" "}{formatObservationAge(age)} ago. No current organisms, remnants, exact stocks,
            flows, or weather are implied.
          </p>
        </div>
        <dl className="tile-fact-grid">
          <div><dt>Recorded elevation</dt><dd>{formatElevation(tile.detail.value.elevationMeters)}</dd></div>
          <div><dt>Known present then</dt><dd>{knownResources.length}</dd></div>
        </dl>
        <KnownResourceList resources={knownResources} empty="No retained resource identities." />
      </>
    );
  }

  const live = tile.detail.value;
  const occupied = evolution?.occupiedTiles.find((candidate) => candidate.tileId === tile.tileId);
  const habitat = evolution?.habitatProfile;
  const resources = habitat === undefined
    ? []
    : buildRelevantResourceRows(world, live, habitat);
  const deaths = summarizeTileDeaths(world, tile.tileId, deathWindowTicks);
  const leadingDeath = deaths.causes[0];

  return (
    <>
      <div className={`tile-knowledge-message live habitat-${habitatTone(occupied)}`}>
        <strong>{habitatHeadline(occupied)}</strong>
        <p>
          Conditions are compared with the controlled species’ current DNA at completed tick{" "}
          {world.completedTick.toLocaleString("en-US")}.
        </p>
      </div>

      <HabitatFacts live={live} occupied={occupied} habitat={habitat} />

      <section className="tile-resource-snapshot relevant-resources" aria-labelledby="relevant-resources-title">
        <div className="tile-section-heading">
          <div>
            <p className="section-label">What this lineage uses or must endure</p>
            <h3 id="relevant-resources-title">Relevant compounds</h3>
          </div>
        </div>
        {habitat === undefined ? (
          <p>DNA-linked resource requirements are unavailable for this held view.</p>
        ) : resources.length === 0 ? (
          <p>No DNA-linked compounds are defined for this species.</p>
        ) : (
          <ul>
            {resources.map((resource) => (
              <li className={`relevant-resource ${resource.tone}`} key={resource.resourceId}>
                <span>
                  <strong>{resource.name}</strong>
                  <small>{resource.roles.join(" · ")}</small>
                  {resource.hazardLabel === null ? null : <small>{resource.hazardLabel}</small>}
                </span>
                <span className="resource-suitability">
                  <b>{resource.status}</b>
                  <small>{formatQuantity(resource.quantityQ)}</small>
                </span>
              </li>
            ))}
          </ul>
        )}
        <p className="tile-method-note">
          “Abundant” means at least seven days of stock at this species’ last observed uptake rate
          on this tile. Without recent uptake, the panel reports presence without estimating supply.
        </p>
      </section>

      <section className="tile-mortality" aria-labelledby="tile-mortality-title">
        <div className="tile-section-heading">
          <div>
            <p className="section-label">Controlled-species outcomes</p>
            <h3 id="tile-mortality-title">Why life is dying here</h3>
          </div>
          <label>
            Window
            <select
              aria-label="Death history window"
              value={deathWindowTicks}
              onChange={(event) => onDeathWindowChange(Number(event.target.value))}
            >
              {DEATH_WINDOWS.map((ticks) => (
                <option key={ticks} value={ticks}>
                  {formatDeathWindow(ticks, world.tickDurationHours)}
                </option>
              ))}
            </select>
          </label>
        </div>
        {deaths.total === 0 ? (
          <div className="mortality-empty">
            <strong>No deaths recorded in this window</strong>
            <p>
              This does not guarantee growth: low habitat fit or missing resources may still
              suppress health and reproduction before a death occurs.
            </p>
          </div>
        ) : (
          <>
            <p className="mortality-summary">
              <strong>{deaths.total.toLocaleString("en-US")} death{deaths.total === 1 ? "" : "s"}</strong>
              {" "}in the last {deathWindowTicks} ticks. The leading recorded cause is{" "}
              <strong>{leadingDeath.label.toLowerCase()}</strong>.
            </p>
            <ul className="mortality-causes">
              {deaths.causes.map((cause) => (
                <li key={cause.cause}>
                  <span><strong>{cause.label}</strong><small>{cause.explanation}</small></span>
                  <b>{cause.count.toLocaleString("en-US")}</b>
                </li>
              ))}
            </ul>
          </>
        )}
      </section>
    </>
  );
}

function HabitatFacts({
  live,
  occupied,
  habitat,
}: {
  readonly live: LiveTile;
  readonly occupied?: EvolutionOccupiedTile;
  readonly habitat?: EvolutionHabitatProfile;
}) {
  const isAquatic = live.elevationMeters < 0;
  const terrainFit = isAquatic || habitat?.canOccupyTerrestrial === true;
  return (
    <dl className="tile-fact-grid habitat-facts">
      <div className={habitatTone(occupied)}>
        <dt>Overall habitat fit</dt>
        <dd>{occupied === undefined ? "Unavailable" : formatPercent(occupied.averageEnvironmentalFactorQ)}</dd>
        <small>{habitatHeadline(occupied)}</small>
      </div>
      <div className={terrainFit ? "favorable" : "danger"}>
        <dt>Habitat</dt>
        <dd>{isAquatic ? `Aquatic · ${formatDepth(live.elevationMeters)}` : `Land · ${formatElevation(live.elevationMeters)}`}</dd>
        <small>{terrainFit ? "Compatible with current DNA" : "Current DNA cannot occupy land"}</small>
      </div>
      <div className={temperatureTone(occupied, habitat)}>
        <dt>Temperature</dt>
        <dd>{occupied?.hasCurrentClimate ? formatTemperature(occupied.currentTemperatureMilliC) : "Unavailable"}</dd>
        <small>{habitat === undefined ? "DNA range unavailable" :
          `${occupied?.hasCurrentClimate ? `Tile seasonal ${formatTemperature(occupied.seasonalTemperatureMinimumMilliC)}–${formatTemperature(occupied.seasonalTemperatureMaximumMilliC)} · ` : ""}DNA preferred ${formatTemperature(habitat.preferredTemperatureMinimumMilliC)}–${formatTemperature(habitat.preferredTemperatureMaximumMilliC)} · hard limits ${formatTemperature(habitat.hardTemperatureMinimumMilliC)}–${formatTemperature(habitat.hardTemperatureMaximumMilliC)}`}</small>
      </div>
      <div>
        <dt>Surface moisture</dt>
        <dd>{occupied?.hasCurrentClimate ? formatPercent(occupied.currentSurfaceMoistureQ) : "Unavailable"}</dd>
        <small>{isAquatic ? "Water-covered habitat" : "Current surface moisture"}</small>
      </div>
      <div className={habitat?.requiresLight && occupied?.currentAccessibleLightQ === 0 ? "danger" : "favorable"}>
        <dt>Usable light</dt>
        <dd>{occupied?.hasCurrentClimate ? formatPercent(occupied.currentAccessibleLightQ) : "Unavailable"}</dd>
        <small>{habitat?.requiresLight ? "Required by current metabolism" : "Not required by current metabolism"}</small>
      </div>
      <div>
        <dt>Cloud cover</dt>
        <dd>{occupied?.hasCurrentClimate ? formatPercent(occupied.currentCloudQ) : "Unavailable"}</dd>
        <small>{occupied?.hasCurrentClimate ? `${occupied.currentPrecipitationMicrometersPerHour.toLocaleString("en-US")} µm/h precipitation` : "Current climate unavailable"}</small>
      </div>
      <div>
        <dt>Surface light</dt>
        <dd>{occupied?.hasCurrentClimate ? formatPercent(occupied.currentSurfaceLightQ) : "Unavailable"}</dd>
        <small>Before depth and turbidity losses</small>
      </div>
      <div>
        <dt>Volcanic activity</dt>
        <dd>{occupied === undefined ? formatPercent(live.baselineVolcanismQ) : formatPercent(occupied.baselineVolcanismQ)}</dd>
        <small>Stable geological baseline</small>
      </div>
    </dl>
  );
}

interface RelevantResourceRow {
  readonly resourceId: number;
  readonly name: string;
  readonly quantityQ: bigint;
  readonly roles: readonly string[];
  readonly status: string;
  readonly tone: "favorable" | "caution" | "danger" | "neutral";
  readonly hazardLabel: string | null;
}

export function buildRelevantResourceRows(
  world: ActorWorldProjection,
  live: LiveTile,
  habitat: EvolutionHabitatProfile,
): RelevantResourceRow[] {
  return habitat.relevantResources.map((relevance) => {
    const quantityQ = live.resourceStocks.find((stock) => stock.resourceId === relevance.resourceId)
      ?.quantityQ ?? 0n;
    const uptakeQ = live.resourceFlowContributors
      .filter((flow) => flow.resourceId === relevance.resourceId &&
        flow.kind === ResourceFlowKind.ORGANISM_UPTAKE &&
        flow.speciesId === world.controlledSpeciesId)
      .reduce((total, flow) => total + flow.amountQ, 0n);
    const supply = resourceSupplyStatus(quantityQ, uptakeQ, live.resourceFlowPeriodHours, relevance);
    return {
      resourceId: relevance.resourceId,
      name: resourceName(world.resourceDefinitions, relevance.resourceId),
      quantityQ,
      roles: relevanceRoles(relevance),
      status: supply.status,
      tone: supply.tone,
      hazardLabel: hazardStatus(quantityQ, relevance),
    };
  }).sort((left, right) => {
    const rank = { danger: 0, caution: 1, favorable: 2, neutral: 3 } as const;
    return rank[left.tone] - rank[right.tone] || left.resourceId - right.resourceId;
  });
}

function resourceSupplyStatus(
  quantityQ: bigint,
  uptakeQ: bigint,
  periodHours: number,
  relevance: EvolutionRelevantResource,
): { readonly status: string; readonly tone: RelevantResourceRow["tone"] } {
  const isNeeded = relevance.metabolismInput || relevance.growthInput || relevance.healthRequirement;
  const hazard = hazardTone(quantityQ, relevance);
  if (relevance.environmentalHazard && hazard === "danger") {
    return { status: "Dangerous excess", tone: "danger" };
  }
  if (relevance.environmentalHazard && hazard === "caution") {
    return { status: "Stressful excess", tone: "caution" };
  }
  if (!isNeeded) {
    return { status: "Below stress level", tone: hazard };
  }
  if (quantityQ <= 0n) return { status: "Missing", tone: "danger" };
  if (uptakeQ <= 0n || periodHours <= 0) return { status: "Present", tone: "neutral" };
  const coverageHours = quantityQ * BigInt(periodHours) / uptakeQ;
  if (coverageHours >= 168n) return { status: "Abundant", tone: "favorable" };
  if (coverageHours >= 24n) return { status: "Available", tone: "favorable" };
  return { status: "Limited", tone: "caution" };
}

function relevanceRoles(resource: EvolutionRelevantResource): string[] {
  const roles: string[] = [];
  if (resource.metabolismInput) roles.push("energy metabolism");
  if (resource.growthInput) roles.push("growth");
  if (resource.healthRequirement) roles.push("cell health");
  if (resource.environmentalHazard) roles.push("environmental hazard");
  return roles;
}

function hazardStatus(quantityQ: bigint, resource: EvolutionRelevantResource): string | null {
  if (!resource.environmentalHazard) return null;
  if (quantityQ >= resource.hardHazardThresholdQ) {
    return `Above hard exposure limit (${formatQuantity(resource.hardHazardThresholdQ)})`;
  }
  if (quantityQ >= resource.softHazardThresholdQ) {
    return `Above preferred exposure limit (${formatQuantity(resource.softHazardThresholdQ)})`;
  }
  return `Below stress threshold (${formatQuantity(resource.softHazardThresholdQ)})`;
}

function hazardTone(
  quantityQ: bigint,
  resource: EvolutionRelevantResource,
): RelevantResourceRow["tone"] {
  if (!resource.environmentalHazard) return "neutral";
  if (quantityQ >= resource.hardHazardThresholdQ) return "danger";
  if (quantityQ >= resource.softHazardThresholdQ) return "caution";
  return "favorable";
}

export interface TileDeathSummary {
  readonly total: number;
  readonly causes: readonly {
    readonly cause: number;
    readonly count: number;
    readonly label: string;
    readonly explanation: string;
  }[];
}

export function summarizeTileDeaths(
  world: ActorWorldProjection,
  tileId: number,
  windowTicks: number,
): TileDeathSummary {
  const cutoff = world.completedTick > BigInt(windowTicks)
    ? world.completedTick - BigInt(windowTicks)
    : 0n;
  const counts = new Map<number, number>();
  for (const event of world.journeyEvents) {
    if (event.family !== OrganismJourneyEventFamily.DEATH ||
      event.subjectSpeciesId !== world.controlledSpeciesId ||
      event.tileId !== tileId || event.tick <= cutoff) continue;
    counts.set(event.detailId, (counts.get(event.detailId) ?? 0) + 1);
  }
  const causes = [...counts.entries()]
    .map(([cause, count]) => ({ cause, count, ...deathCauseCopy(cause) }))
    .sort((left, right) => right.count - left.count || left.cause - right.cause);
  return { total: causes.reduce((total, cause) => total + cause.count, 0), causes };
}

function habitatHeadline(occupied?: EvolutionOccupiedTile): string {
  if (occupied === undefined) return "Habitat fit unavailable";
  if (occupied.averageEnvironmentalFactorQ >= 850_000) return "Favorable for our current DNA";
  if (occupied.averageEnvironmentalFactorQ >= 600_000) return "Livable, but under environmental strain";
  if (occupied.averageEnvironmentalFactorQ > 0) return "Harsh for our current DNA";
  return "Incompatible with our current DNA";
}

function habitatTone(occupied?: EvolutionOccupiedTile): "favorable" | "caution" | "danger" | "neutral" {
  if (occupied === undefined) return "neutral";
  if (occupied.averageEnvironmentalFactorQ >= 850_000) return "favorable";
  if (occupied.averageEnvironmentalFactorQ >= 600_000) return "caution";
  return "danger";
}

function temperatureTone(
  occupied?: EvolutionOccupiedTile,
  habitat?: EvolutionHabitatProfile,
): "favorable" | "caution" | "danger" | "neutral" {
  if (occupied?.hasCurrentClimate !== true || habitat === undefined) return "neutral";
  const temperature = occupied.currentTemperatureMilliC;
  if (temperature < habitat.hardTemperatureMinimumMilliC ||
    temperature > habitat.hardTemperatureMaximumMilliC) return "danger";
  if (temperature < habitat.preferredTemperatureMinimumMilliC ||
    temperature > habitat.preferredTemperatureMaximumMilliC) return "caution";
  return "favorable";
}

function KnownResourceList({ resources, empty }: {
  readonly resources: readonly string[];
  readonly empty: string;
}) {
  return (
    <div className="tile-resource-snapshot retained">
      <h3>Resources known present at that observation</h3>
      {resources.length === 0 ? <p>{empty}</p> : (
        <ul className="known-resource-list">
          {resources.map((resource) => <li key={resource}><span>{resource}</span></li>)}
        </ul>
      )}
    </div>
  );
}

export function observationAge(
  completedTick: bigint,
  observedAtTick: bigint,
  tickDurationHours: number,
) {
  const ticks = completedTick > observedAtTick ? completedTick - observedAtTick : 0n;
  return { ticks, hours: ticks * BigInt(tickDurationHours) };
}

function formatObservationAge(age: { readonly ticks: bigint; readonly hours: bigint }): string {
  if (age.ticks === 0n) return "0 simulation hours";
  const hourLabel = age.hours === 1n ? "hour" : "hours";
  const tickLabel = age.ticks === 1n ? "tick" : "ticks";
  return `${age.hours.toLocaleString("en-US")} simulation ${hourLabel} (${age.ticks.toLocaleString("en-US")} ${tickLabel})`;
}

function tileState(tile: TileProjection): "live" | "reduced" | "unknown" {
  return tile.detail.case === "live" ? "live" : tile.detail.case === "reduced" ? "reduced" : "unknown";
}

function scopeLabel(tile: TileProjection): string {
  return tile.detail.case === "live" ? "Current" : tile.detail.case === "reduced" ? "Last known" : "Unknown";
}

function resourceName(definitions: readonly ResourceDefinition[], resourceId: number): string {
  return definitions.find((candidate) => candidate.resourceId === resourceId)?.displayName ??
    `Resource ${resourceId}`;
}

function formatElevation(value: number): string {
  return `${value.toLocaleString("en-US")} m`;
}

function formatDepth(elevationMeters: number): string {
  return `${Math.abs(elevationMeters).toLocaleString("en-US")} m deep`;
}

function formatTemperature(value: number): string {
  const celsius = value / 1_000;
  return `${celsius.toLocaleString("en-US", { maximumFractionDigits: 1 })} °C`;
}

function formatPercent(value: number): string {
  return `${(value / 10_000).toFixed(1)}%`;
}

function formatDeathWindow(ticks: number, tickDurationHours: number): string {
  const hours = ticks * tickDurationHours;
  if (hours % 24 === 0) {
    const days = hours / 24;
    return `Last ${ticks} ticks (${days} ${days === 1 ? "day" : "days"})`;
  }
  return `Last ${ticks} ticks (${hours.toLocaleString("en-US")} hours)`;
}

function formatQuantity(value: bigint): string {
  return `${value.toLocaleString("en-US")} q`;
}
