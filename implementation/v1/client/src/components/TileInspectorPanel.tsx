import type {
  ActorWorldProjection,
  ResourceDefinition,
  TileProjection,
} from "../generated/lyfe/v1/projection_pb";

interface TileInspectorPanelProps {
  readonly world: ActorWorldProjection;
  readonly selectedTileId: number | null;
}

export function TileInspectorPanel({ world, selectedTileId }: TileInspectorPanelProps) {
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
      <TileInspectorBody world={world} tile={tile} />
    </section>
  );
}

function TileInspectorBody({
  world,
  tile,
}: {
  readonly world: ActorWorldProjection;
  readonly tile: TileProjection;
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
  const presentStocks = live.resourceStocks
    .filter((stock) => stock.quantityQ > 0n)
    .map((stock) => ({
      resourceId: stock.resourceId,
      name: resourceName(world.resourceDefinitions, stock.resourceId),
      quantityQ: stock.quantityQ,
    }))
    .sort((left, right) => left.resourceId - right.resourceId);
  return (
    <>
      <div className="tile-knowledge-message live">
        <strong>Current authorized observation</strong>
        <p>
          Exact projected state at completed tick {world.completedTick.toLocaleString("en-US")}.
          Values update only at completed simulation boundaries.
        </p>
      </div>
      <dl className="tile-fact-grid">
        <div><dt>Elevation</dt><dd>{formatElevation(live.elevationMeters)}</dd></div>
        <div><dt>Visible organisms</dt><dd>{live.organisms.length.toLocaleString("en-US")}</dd></div>
        <div><dt>Visible remnants</dt><dd>{live.remnants.length.toLocaleString("en-US")}</dd></div>
        <div><dt>Present compounds</dt><dd>{presentStocks.length.toLocaleString("en-US")}</dd></div>
      </dl>
      <div className="tile-resource-snapshot">
        <h3>Exact current compounds</h3>
        {presentStocks.length === 0 ? (
          <p>No positive projected stock at this boundary.</p>
        ) : (
          <ul>
            {presentStocks.map((stock) => (
              <li key={stock.resourceId}>
                <span>{stock.name}</span>
                <strong>{formatQuantity(stock.quantityQ)}</strong>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
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

function formatQuantity(value: bigint): string {
  return `${value.toLocaleString("en-US")} q`;
}
