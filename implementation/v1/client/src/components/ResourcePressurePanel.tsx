import {
  AcquisitionGateReason,
  AcquisitionProcess,
  OrganismActionGateReason,
  OrganismActionProcess,
  ResourceBiologicalForm,
  ResourceEnvironmentalPhase,
  ResourceFlowKind,
  ResourceFlowProcess,
  type ActorWorldProjection,
  type AcquisitionGateEvidence,
  type LiveTile,
  type OrganismActionGateEvidence,
  type ResourceAcquisitionEvidence,
  type ResourceDefinition,
  type ReactionDefinition,
} from "../generated/lyfe/v1/projection_pb";
import { useState } from "react";

interface ResourcePressurePanelProps {
  readonly world: ActorWorldProjection;
  readonly selectedOrganismId: bigint | null;
  readonly selectedTileId?: number | null;
}

export interface ResourceFlowRow {
  readonly resourceId: number;
  readonly displayName: string;
  readonly descriptor: string;
  readonly stockQ: bigint;
  readonly inflowQ: bigint;
  readonly outflowQ: bigint;
  readonly netQ: bigint;
  readonly contributions: readonly { readonly label: string; readonly amountQ: bigint }[];
}

export interface ResourceHistoryPoint {
  readonly simulatedHour: bigint;
  readonly stockQ: bigint;
  readonly inflowQ: bigint;
  readonly outflowQ: bigint;
  readonly netQ: bigint;
}

export interface ResourceContributorRow {
  readonly process: string;
  readonly actor: string;
  readonly direction: string;
  readonly amountQ: bigint;
}

export function ResourcePressurePanel({
  world,
  selectedOrganismId,
  selectedTileId = null,
}: ResourcePressurePanelProps) {
  const [showAbsent, setShowAbsent] = useState(false);
  const [selectedHistoryResourceId, setSelectedHistoryResourceId] = useState<number | null>(null);
  const selection = selectLiveTile(world, selectedOrganismId, selectedTileId);
  if (selection === null) return null;

  const { tileId, tile, organism } = selection;
  const rows = buildResourceFlowRows(tile, world.resourceDefinitions);
  const evidence = organism?.resourceAcquisitionEvidence ?? [];
  const gates = organism?.acquisitionGateEvidence ?? [];
  const actionGates = organism?.actionGateEvidence ?? [];
  const evidenceByResource = new Map(evidence.map((value) => [value.resourceId, value]));
  const limitingAcquisition = selectLimitingAcquisition(evidence);
  const limitingDefinition = limitingAcquisition === undefined
    ? undefined
    : world.resourceDefinitions.find((value) =>
        value.resourceId === limitingAcquisition.resourceId);
  const visibleRows = showAbsent
    ? rows
    : rows.filter((row) =>
        row.stockQ > 0n ||
        row.inflowQ + row.outflowQ > 0n ||
        evidenceByResource.has(row.resourceId) ||
        row.descriptor.includes("boundary"));
  const pressureQ = organism?.resourcePressureQ ?? 0;
  const deficitQ = organism?.limitingMaterialDeficitQ ?? 0;
  const historyResource = rows.find((row) => row.resourceId === selectedHistoryResourceId) ??
    rows.find((row) => row.inflowQ + row.outflowQ > 0n) ?? rows.find((row) => row.stockQ > 0n) ?? rows[0];
  const history = historyResource === undefined
    ? []
    : buildResourceHistorySeries(tile, historyResource.resourceId);
  const contributors = historyResource === undefined
    ? []
    : buildResourceContributorRows(
        tile,
        historyResource.resourceId,
        world.reactionDefinitions,
      );

  return (
    <section className="resource-panel" aria-labelledby="resource-title">
      <header className="resource-header">
        <div>
          <p className="section-label">Material economy · live observation</p>
          <h2 id="resource-title">Tile {tileId} resource pulse</h2>
          {world.completedTick === 0n ? (
            <p>
              Exact compound stocks at the initial boundary. No resource flow exists
              until the first simulation tick completes.
            </p>
          ) : (
            <p>
              Exact compound stocks and balanced ledger flow over the last{" "}
              {tile.resourceFlowPeriodHours} simulation hour
              {tile.resourceFlowPeriodHours === 1 ? "" : "s"}. Inputs and outputs are
              shown separately so a busy equilibrium does not look inert.
            </p>
          )}
        </div>
        <div className={`pressure-card ${pressureTone(pressureQ)}`}>
          <span>Selected organism resource pressure</span>
          <strong>{formatPercent(pressureQ)}</strong>
          <small>
            {organism === undefined
              ? "No controlled organism is selected on this tile."
              : world.completedTick === 0n
                ? "No completed acquisition evidence yet."
                : limitingAcquisition !== undefined
                  ? `${limitingDefinition?.displayName ?? `Resource ${limitingAcquisition.resourceId}`} ` +
                    `was limiting: ${formatQuantity(limitingAcquisition.grantedQ)} of ` +
                    `${formatQuantity(limitingAcquisition.requestedQ)} granted ` +
                    `(${constraintLabel(limitingAcquisition)}).`
                  : gates.length > 0
                    ? describeAcquisitionGate(gates[0])
                  : evidence.length === 0
                    ? "No current acquisition or gate evidence is available."
                    : `No compound constrained the last acquisition; ` +
                      `${formatPercent(deficitQ)} is the stored aggregate material deficit.`}
          </small>
        </div>
      </header>

      {gates.length > 0 && (
        <aside className="acquisition-gates" aria-label="Inactive acquisition paths">
          <strong>Inactive acquisition paths</strong>
          <ul>
            {gates.map((gate) => (
              <li key={`${gate.process}:${gate.reason}`}>
                {describeAcquisitionGate(gate)}
              </li>
            ))}
          </ul>
        </aside>
      )}

      {actionGates.length > 0 && (
        <aside className="acquisition-gates" aria-label="Current organism process blockers">
          <strong>Current process blockers</strong>
          <ul>
            {actionGates.map((gate) => (
              <li key={gate.process}>
                {describeOrganismActionGate(gate, world.resourceDefinitions)}
              </li>
            ))}
          </ul>
        </aside>
      )}


      <section className="resource-history" aria-labelledby="resource-history-title">
        <div className="resource-history-heading">
          <div>
            <p className="section-label">Exact rolling history · seven-day maximum</p>
            <h3 id="resource-history-title">Resource level and net flow</h3>
          </div>
          <label>
            Compound
            <select
              value={historyResource?.resourceId ?? ""}
              onChange={(event) => setSelectedHistoryResourceId(Number(event.target.value))}
            >
              {rows.map((row) => (
                <option key={row.resourceId} value={row.resourceId}>{row.displayName}</option>
              ))}
            </select>
          </label>
        </div>
        {history.length < 2 ? (
          <p className="resource-history-empty">
            History begins after the first completed simulation tick.
          </p>
        ) : (
          <ResourceHistoryChart points={history} />
        )}
        <div className="resource-contributors">
          <h4>Last completed tick contributors</h4>
          {contributors.length === 0 ? (
            <p>No applied flow contributors for this compound.</p>
          ) : (
            <ul>
              {contributors.map((value, index) => (
                <li key={`${value.process}:${value.actor}:${value.direction}:${index}`}>
                  <span>
                    <strong>{value.process}</strong>
                    <small>{value.actor} · {value.direction}</small>
                  </span>
                  <b>{formatQuantity(value.amountQ)}</b>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      <div className="resource-table-toolbar">
        <span>{visibleRows.length} of {rows.length} compounds</span>
        <button
          type="button"
          aria-pressed={showAbsent}
          onClick={() => setShowAbsent((value) => !value)}
        >
          {showAbsent ? "Hide absent compounds" : "Show absent compounds"}
        </button>
      </div>
      <div className="resource-table-wrap">
        <table className="resource-table">
          <thead>
            <tr>
              <th scope="col">Compound</th>
              <th scope="col">Current stock</th>
              <th scope="col">In</th>
              <th scope="col">Out</th>
              <th scope="col">Net</th>
              <th scope="col">Selected demand</th>
              <th scope="col">Last-tick causes</th>
            </tr>
          </thead>
          <tbody>
            {visibleRows.map((row) => (
              <tr
                key={row.resourceId}
                className={row.inflowQ + row.outflowQ > 0n || evidenceByResource.has(row.resourceId)
                  ? "active"
                  : "quiet"}
              >
                <th scope="row">
                  <strong>{row.displayName}</strong>
                  <small>{row.descriptor}</small>
                </th>
                <td>{formatQuantity(row.stockQ)}</td>
                <td className="flow-in">{formatQuantity(row.inflowQ)}</td>
                <td className="flow-out">{formatQuantity(row.outflowQ)}</td>
                <td className={row.netQ > 0n ? "flow-in" : row.netQ < 0n ? "flow-out" : ""}>
                  {formatSigned(row.netQ)}
                </td>
                <td className={evidenceByResource.get(row.resourceId)?.tileSupplyConstrained ||
                    evidenceByResource.get(row.resourceId)?.claimContentionConstrained
                  ? "demand-limited"
                  : ""}>
                  <DemandEvidence value={evidenceByResource.get(row.resourceId)} />
                </td>
                <td>
                  <span className="flow-causes">
                    {row.contributions.length === 0 ? (
                      <small>No applied flow</small>
                    ) : row.contributions.map((cause) => (
                      <small key={cause.label}>
                        {cause.label} {formatQuantity(cause.amountQ)}
                      </small>
                    ))}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ResourceHistoryChart({ points }: { readonly points: readonly ResourceHistoryPoint[] }) {
  const width = 720;
  const height = 190;
  const plotTop = 12;
  const plotBottom = 112;
  const flowBaseline = 151;
  const stockValues = points.map((point) => point.stockQ);
  const minimumStock = stockValues.reduce((minimum, value) => value < minimum ? value : minimum);
  const maximumStock = stockValues.reduce((maximum, value) => value > maximum ? value : maximum);
  const stockRange = maximumStock - minimumStock || 1n;
  const maximumFlow = points.reduce((maximum, point) => {
    const absolute = point.netQ < 0n ? -point.netQ : point.netQ;
    return absolute > maximum ? absolute : maximum;
  }, 1n);
  const x = (index: number) => points.length === 1 ? width / 2 : index * width / (points.length - 1);
  const stockY = (value: bigint) => plotBottom - bigintRatio(value - minimumStock, stockRange) *
    (plotBottom - plotTop);
  const polyline = points.map((point, index) => `${x(index)},${stockY(point.stockQ)}`).join(" ");
  const barWidth = Math.max(1, Math.min(8, width / Math.max(1, points.length - 1) - 1));
  const first = points[0];
  const last = points[points.length - 1];

  return (
    <div className="resource-history-chart">
      <svg viewBox={`0 0 ${width} ${height}`} role="img"
        aria-label={`Stock and net resource flow from hour ${first.simulatedHour} to ${last.simulatedHour}`}>
        <line className="history-grid" x1="0" y1={plotBottom} x2={width} y2={plotBottom} />
        <line className="history-grid" x1="0" y1={flowBaseline} x2={width} y2={flowBaseline} />
        {points.slice(1).map((point, index) => {
          const magnitude = bigintRatio(point.netQ < 0n ? -point.netQ : point.netQ, maximumFlow) * 29;
          return <rect
            key={`${point.simulatedHour}`}
            className={point.netQ >= 0n ? "history-flow-in" : "history-flow-out"}
            x={x(index + 1) - barWidth / 2}
            y={point.netQ >= 0n ? flowBaseline - magnitude : flowBaseline}
            width={barWidth}
            height={magnitude}
          />;
        })}
        <polyline className="history-stock-line" points={polyline} />
      </svg>
      <div className="resource-history-legend">
        <span><i className="stock" /> stock {formatQuantity(last.stockQ)}</span>
        <span><i className="in" /> positive net flow</span>
        <span><i className="out" /> negative net flow</span>
        <span>hours {first.simulatedHour.toString()}–{last.simulatedHour.toString()}</span>
      </div>
    </div>
  );
}

function bigintRatio(numerator: bigint, denominator: bigint): number {
  return Number(numerator * 1_000_000n / denominator) / 1_000_000;
}

function DemandEvidence({ value }: {
  readonly value: ResourceAcquisitionEvidence | undefined;
}) {
  if (value === undefined) return <span aria-label="No selected-organism demand">—</span>;
  const constrained = value.tileSupplyConstrained || value.claimContentionConstrained;
  const detail = constrained
    ? constraintLabel(value)
    : value.grantedQ === value.requestedQ
      ? "fulfilled"
      : "reduced with coupled claim";
  return (
    <span className="demand-evidence">
      <strong>{formatQuantity(value.grantedQ)} / {formatQuantity(value.requestedQ)}</strong>
      <small>{detail}</small>
    </span>
  );
}

export function selectLimitingAcquisition(
  values: readonly ResourceAcquisitionEvidence[],
): ResourceAcquisitionEvidence | undefined {
  return values
    .filter((value) => value.tileSupplyConstrained || value.claimContentionConstrained)
    .slice()
    .sort((left, right) => {
      const leftDeficit = (left.requestedQ - left.grantedQ) * right.requestedQ;
      const rightDeficit = (right.requestedQ - right.grantedQ) * left.requestedQ;
      if (leftDeficit !== rightDeficit) return leftDeficit > rightDeficit ? -1 : 1;
      return left.resourceId - right.resourceId;
    })[0];
}

export function describeAcquisitionGate(value: AcquisitionGateEvidence): string {
  const process = value.process === AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE
    ? "Energy capture"
    : value.process === AcquisitionProcess.SCAVENGING
      ? "Scavenging"
      : "Acquisition";
  switch (value.reason) {
    case AcquisitionGateReason.MISSING_CAPABILITY:
      return `${process} did not run: no compatible pathway is compiled.`;
    case AcquisitionGateReason.INACCESSIBLE_LIGHT:
      return `${process} did not run: accessible light provided no opportunity this tick.`;
    case AcquisitionGateReason.ENVIRONMENTAL_OPPORTUNITY:
      return `${process} did not run: current environmental conditions provided no opportunity.`;
    case AcquisitionGateReason.INTERNAL_CAPACITY:
      if (value.process === AcquisitionProcess.SCAVENGING) {
        return `${process} did not run: ${formatQuantity(value.availableQ)} digestion room was ` +
          `available, but intake requires ${formatQuantity(value.requiredQ)}.`;
      }
      return `${process} did not run: ${formatQuantity(value.availableQ)} reserve room was ` +
        `available, but one reaction extent requires ${formatQuantity(value.requiredQ)}.`;
    case AcquisitionGateReason.COOLDOWN_ACTIVE:
      return `${process} did not run: cooldown remains active until tick ${value.clearsAtTick}.`;
    case AcquisitionGateReason.INSUFFICIENT_ACTION_ENERGY:
      return `${process} did not run: ${formatQuantity(value.availableQ)} reserve energy was ` +
        `available, but the action requires ${formatQuantity(value.requiredQ)}.`;
    default:
      return `${process} did not run for an unknown reason.`;
  }
}

export function describeOrganismActionGate(
  value: OrganismActionGateEvidence,
  definitions: readonly ResourceDefinition[],
): string {
  const process = value.process === OrganismActionProcess.BIOMASS_GROWTH
    ? "Growth"
    : value.process === OrganismActionProcess.REPRODUCTION
      ? "Reproduction"
      : "Process";
  const resource = definitions.find((candidate) => candidate.resourceId === value.resourceId);
  const resourceName = resource?.displayName ?? `Resource ${value.resourceId}`;
  switch (value.reason) {
    case OrganismActionGateReason.MISSING_CAPABILITY:
      return `${process} did not run: no compatible pathway is compiled.`;
    case OrganismActionGateReason.BEHAVIOR_SUPPRESSED:
      return `${process} did not run: the organism's conserving behavior suppressed it.`;
    case OrganismActionGateReason.COOLDOWN_ACTIVE:
      return `${process} did not run: cooldown remains active until tick ${value.clearsAtTick}.`;
    case OrganismActionGateReason.HEALTH_BELOW_MINIMUM:
      return `${process} did not run: health was ${formatPercent(Number(value.availableQ))}; ` +
        `${formatPercent(Number(value.requiredQ))} is required.`;
    case OrganismActionGateReason.STRUCTURE_BELOW_MINIMUM:
      return `${process} did not run: ${formatQuantity(value.availableQ)} structure was ` +
        `available; ${formatQuantity(value.requiredQ)} is required.`;
    case OrganismActionGateReason.RESERVE_BELOW_MINIMUM:
      return `${process} did not run: ${formatQuantity(value.availableQ)} reserve energy was ` +
        `available; ${formatQuantity(value.requiredQ)} is required.`;
    case OrganismActionGateReason.CONSTITUTIVE_MICRONUTRIENT_QUOTA_MISSING:
      return `${process} did not run: its commissioned ${resourceName} quota has ` +
        `${formatQuantity(value.availableQ)} of ${formatQuantity(value.requiredQ)}.`;
    case OrganismActionGateReason.OFFSPRING_MICRONUTRIENT_QUOTA_MISSING:
      return `${process} did not run: free ${resourceName} for offspring has ` +
        `${formatQuantity(value.availableQ)} of ${formatQuantity(value.requiredQ)}.`;
    case OrganismActionGateReason.MAINTENANCE_SHORTFALL:
      return `${process} did not run: maintenance received ${formatQuantity(value.availableQ)} ` +
        `of ${formatQuantity(value.requiredQ)} reserve energy.`;
    case OrganismActionGateReason.RESERVE_PROTECTION_FLOOR:
      return `${process} did not run: ${formatQuantity(value.availableQ)} reserve remained after ` +
        `maintenance; ${formatQuantity(value.requiredQ)} is needed to protect the floor and fund ` +
        `one growth extent.`;
    case OrganismActionGateReason.INTERNAL_CAPACITY:
      return `${process} did not run: ${formatQuantity(value.availableQ)} staging capacity was ` +
        `available; one extent needs ${formatQuantity(value.requiredQ)}.`;
    case OrganismActionGateReason.RESOURCE_SUPPLY:
      return `${process} did not run: ${resourceName} supplied ${formatQuantity(value.availableQ)} ` +
        `of the ${formatQuantity(value.requiredQ)} needed for one extent.`;
    case OrganismActionGateReason.CLAIM_CONTENTION:
      return `${process} did not run: its material claim lost the final contested extent.`;
    case OrganismActionGateReason.LIFECYCLE_INELIGIBLE:
      return `${process} did not run: the current lifecycle phase is ineligible.`;
    default:
      return `${process} did not run for an unknown reason.`;
  }
}

export function buildResourceFlowRows(
  tile: Pick<LiveTile, "resourceStocks" | "resourceFlows">,
  definitions: readonly ResourceDefinition[],
): ResourceFlowRow[] {
  const definitionsById = new Map(definitions.map((definition) => [definition.resourceId, definition]));
  const flowsByResource = new Map<number, LiveTile["resourceFlows"]>();
  for (const flow of tile.resourceFlows) {
    const values = flowsByResource.get(flow.resourceId) ?? [];
    values.push(flow);
    flowsByResource.set(flow.resourceId, values);
  }

  return tile.resourceStocks.map((stock) => {
    const definition = definitionsById.get(stock.resourceId);
    const flows = flowsByResource.get(stock.resourceId) ?? [];
    const inflowQ = sumFlows(flows, [
      ResourceFlowKind.ENVIRONMENTAL_SOURCE,
      ResourceFlowKind.NEIGHBOR_EXCHANGE_IN,
      ResourceFlowKind.ORGANISM_RELEASE,
    ]);
    const outflowQ = sumFlows(flows, [
      ResourceFlowKind.ENVIRONMENTAL_SINK,
      ResourceFlowKind.NEIGHBOR_EXCHANGE_OUT,
      ResourceFlowKind.ORGANISM_UPTAKE,
    ]);

    return {
      resourceId: stock.resourceId,
      displayName: definition?.displayName ?? `Resource ${stock.resourceId}`,
      descriptor: definition === undefined
        ? "unclassified compound"
        : `${biologicalFormLabel(definition.biologicalForm)} · ${phaseLabel(definition.environmentalPhase)}`,
      stockQ: stock.quantityQ,
      inflowQ,
      outflowQ,
      netQ: inflowQ - outflowQ,
      contributions: flows.map((flow) => ({
        label: flowKindLabel(flow.kind),
        amountQ: flow.amountQ,
      })),
    };
  }).sort((left, right) => {
    const leftActive = left.inflowQ + left.outflowQ > 0n;
    const rightActive = right.inflowQ + right.outflowQ > 0n;
    return leftActive === rightActive
      ? left.displayName.localeCompare(right.displayName)
      : leftActive ? -1 : 1;
  });
}

export function buildResourceHistorySeries(
  tile: Pick<LiveTile, "resourceStocks" | "resourceFlowHistory">,
  resourceId: number,
): ResourceHistoryPoint[] {
  const stock = tile.resourceStocks.find((value) => value.resourceId === resourceId);
  if (stock === undefined || tile.resourceFlowHistory.length === 0) return [];

  let endStockQ = stock.quantityQ;
  const endpoints: ResourceHistoryPoint[] = [];
  for (let index = tile.resourceFlowHistory.length - 1; index >= 0; index -= 1) {
    const interval = tile.resourceFlowHistory[index];
    const flows = interval.resourceFlows.filter((flow) => flow.resourceId === resourceId);
    const inflowQ = sumFlows(flows, [
      ResourceFlowKind.ENVIRONMENTAL_SOURCE,
      ResourceFlowKind.NEIGHBOR_EXCHANGE_IN,
      ResourceFlowKind.ORGANISM_RELEASE,
    ]);
    const outflowQ = sumFlows(flows, [
      ResourceFlowKind.ENVIRONMENTAL_SINK,
      ResourceFlowKind.NEIGHBOR_EXCHANGE_OUT,
      ResourceFlowKind.ORGANISM_UPTAKE,
    ]);
    const netQ = inflowQ - outflowQ;
    endpoints.unshift({
      simulatedHour: interval.endSimulatedHour,
      stockQ: endStockQ,
      inflowQ,
      outflowQ,
      netQ,
    });
    endStockQ -= netQ;
  }

  const first = tile.resourceFlowHistory[0];
  return [{
    simulatedHour: first.endSimulatedHour - BigInt(first.periodHours),
    stockQ: endStockQ,
    inflowQ: 0n,
    outflowQ: 0n,
    netQ: 0n,
  }, ...endpoints];
}

export function buildResourceContributorRows(
  tile: Pick<LiveTile, "resourceFlowContributors">,
  resourceId: number,
  reactions: readonly ReactionDefinition[],
): ResourceContributorRow[] {
  const reactionsById = new Map(reactions.map((reaction) => [reaction.reactionId, reaction]));
  return tile.resourceFlowContributors
    .filter((value) => value.resourceId === resourceId)
    .map((value) => ({
      process: value.reactionId === 0
        ? flowProcessLabel(value.process)
        : reactionsById.get(value.reactionId)?.displayName ?? `Reaction ${value.reactionId}`,
      actor: value.speciesId !== 0n
        ? `Species ${value.speciesId}`
        : isEnvironmentalProcess(value.process)
          ? "Environment"
          : "Other biological activity",
      direction: flowKindLabel(value.kind),
      amountQ: value.amountQ,
    }))
    .sort((left, right) => left.amountQ === right.amountQ
      ? left.process.localeCompare(right.process)
      : left.amountQ > right.amountQ ? -1 : 1);
}

function isEnvironmentalProcess(process: ResourceFlowProcess): boolean {
  return process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SOURCE ||
    process === ResourceFlowProcess.ENVIRONMENTAL_RESOURCE_SOURCE ||
    process === ResourceFlowProcess.REMNANT_DECAY ||
    process === ResourceFlowProcess.ENVIRONMENTAL_GAS_SINK ||
    process === ResourceFlowProcess.ENVIRONMENTAL_GAS_EXCHANGE;
}

function flowProcessLabel(process: ResourceFlowProcess): string {
  switch (process) {
    case ResourceFlowProcess.EXTERNAL_ENERGY_CAPTURE: return "External energy capture";
    case ResourceFlowProcess.PARTICULATE_DIGESTION: return "Particulate digestion";
    case ResourceFlowProcess.ENVIRONMENTAL_GAS_SOURCE: return "Volcanic gas source";
    case ResourceFlowProcess.ENVIRONMENTAL_GAS_SINK: return "Environmental gas loss";
    case ResourceFlowProcess.ENVIRONMENTAL_GAS_EXCHANGE: return "Neighbor gas exchange";
    case ResourceFlowProcess.MANDATORY_MAINTENANCE: return "Mandatory maintenance";
    case ResourceFlowProcess.BIOMASS_ASSEMBLY: return "Biomass assembly";
    case ResourceFlowProcess.MICRONUTRIENT_UPTAKE: return "Micronutrient uptake";
    case ResourceFlowProcess.ENVIRONMENTAL_RESOURCE_SOURCE: return "Geological weathering";
    case ResourceFlowProcess.REMNANT_DECAY: return "Remnant breakdown";
    default: return "Unclassified process";
  }
}

export function selectLiveTile(
  world: ActorWorldProjection,
  selectedOrganismId: bigint | null,
  selectedTileId: number | null,
) {
  const liveTiles: { readonly tileId: number; readonly tile: LiveTile }[] = [];
  for (const projected of world.tiles) {
    if (projected.detail.case === "live") {
      liveTiles.push({ tileId: projected.tileId, tile: projected.detail.value });
    }
  }
  if (selectedTileId !== null) {
    const requestedTile = liveTiles.find((candidate) => candidate.tileId === selectedTileId);
    if (requestedTile === undefined) return null;
    return {
      tileId: requestedTile.tileId,
      tile: requestedTile.tile,
      organism: requestedTile.tile.organisms.find((candidate) =>
        candidate.organismId === selectedOrganismId &&
        candidate.speciesId === world.controlledSpeciesId) ??
        requestedTile.tile.organisms.find((candidate) =>
          candidate.speciesId === world.controlledSpeciesId),
    };
  }
  for (const projected of liveTiles) {
    const organism = projected.tile.organisms.find((candidate) =>
      candidate.organismId === selectedOrganismId &&
      candidate.speciesId === world.controlledSpeciesId);
    if (organism !== undefined) {
      return { tileId: projected.tileId, tile: projected.tile, organism };
    }
  }

  const projected = liveTiles.find((candidate) => candidate.tile.organisms.some(
    (organism) => organism.speciesId === world.controlledSpeciesId,
  ));
  if (projected === undefined) return null;
  return {
    tileId: projected.tileId,
    tile: projected.tile,
    organism: projected.tile.organisms.find(
      (organism) => organism.speciesId === world.controlledSpeciesId,
    ),
  };
}

function sumFlows(
  flows: LiveTile["resourceFlows"],
  kinds: readonly ResourceFlowKind[],
): bigint {
  return flows.reduce(
    (sum, flow) => kinds.includes(flow.kind) ? sum + flow.amountQ : sum,
    0n,
  );
}

function flowKindLabel(kind: ResourceFlowKind): string {
  switch (kind) {
    case ResourceFlowKind.ENVIRONMENTAL_SOURCE: return "source";
    case ResourceFlowKind.ENVIRONMENTAL_SINK: return "sink";
    case ResourceFlowKind.NEIGHBOR_EXCHANGE_IN: return "neighbor in";
    case ResourceFlowKind.NEIGHBOR_EXCHANGE_OUT: return "neighbor out";
    case ResourceFlowKind.ORGANISM_UPTAKE: return "organism uptake";
    case ResourceFlowKind.ORGANISM_RELEASE: return "organism release";
    default: return "unclassified";
  }
}

function biologicalFormLabel(form: ResourceBiologicalForm): string {
  switch (form) {
    case ResourceBiologicalForm.INORGANIC: return "inorganic";
    case ResourceBiologicalForm.ORGANIC: return "organic";
    case ResourceBiologicalForm.BOUNDARY: return "inexhaustible boundary";
    default: return "unknown form";
  }
}

function phaseLabel(phase: ResourceEnvironmentalPhase): string {
  switch (phase) {
    case ResourceEnvironmentalPhase.GAS: return "gas";
    case ResourceEnvironmentalPhase.DISSOLVED: return "dissolved";
    case ResourceEnvironmentalPhase.BOUNDARY: return "boundary";
    case ResourceEnvironmentalPhase.PARTICULATE: return "particulate";
    default: return "unknown phase";
  }
}

function pressureTone(valueQ: number): string {
  if (valueQ >= 650_000) return "high";
  if (valueQ >= 300_000) return "medium";
  return "low";
}

function constraintLabel(value: ResourceAcquisitionEvidence): string {
  if (value.tileSupplyConstrained && value.claimContentionConstrained) {
    return "tile supply + claim contention";
  }
  return value.tileSupplyConstrained ? "tile supply" : "claim contention";
}

function formatPercent(valueQ: number): string {
  return `${(valueQ / 10_000).toFixed(1)}%`;
}

function formatQuantity(valueQ: bigint): string {
  return `${valueQ.toLocaleString("en-US")} q`;
}

function formatSigned(valueQ: bigint): string {
  return `${valueQ > 0n ? "+" : ""}${formatQuantity(valueQ)}`;
}
