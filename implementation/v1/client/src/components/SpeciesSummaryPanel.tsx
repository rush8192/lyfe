import {
  SpeciesPopulationScope,
  type ActorWorldProjection,
  type OrganismProjection,
} from "../generated/lyfe/v1/projection_pb";

export interface SpeciesSummary {
  readonly speciesId: bigint;
  readonly controlled: boolean;
  readonly population: bigint;
  readonly averageHealthQ: number | null;
  readonly occupiedTileCount: number;
  readonly populationScope: SpeciesPopulationScope;
}

export function SpeciesSummaryPanel({
  world,
  speciesId,
  onBack,
}: {
  readonly world: ActorWorldProjection;
  readonly speciesId: bigint;
  readonly onBack?: () => void;
}) {
  const summary = summarizeSpecies(world, speciesId);
  if (summary === null) return null;

  return (
    <section className={`species-summary ${summary.controlled ? "controlled" : "observed"}`}
      aria-labelledby="species-summary-title">
      {onBack === undefined ? null : (
        <button className="panel-back" type="button" onClick={onBack}>← Back</button>
      )}
      <div className="species-summary-heading">
        <div>
          <p className="section-label">{summary.controlled ? "Current species" : "Visible encounter"}</p>
          <h3 id="species-summary-title">
            {summary.controlled ? "Our species" : `Other species #${summary.speciesId.toString()}`}
          </h3>
        </div>
        <span className={`species-swatch ${summary.controlled ? "controlled" : "other"}`} aria-hidden="true" />
      </div>
      <dl className="species-metrics">
        <div>
          <dt>{summary.controlled ? "Total living" : "Observed living"}</dt>
          <dd>{summary.population.toLocaleString("en-US")}</dd>
        </div>
        <div>
          <dt>Average health</dt>
          <dd>{summary.averageHealthQ === null ? "Unavailable" : formatRatio(summary.averageHealthQ)}</dd>
        </div>
        <div>
          <dt>{summary.controlled ? "Tiles occupied" : "Observed tiles"}</dt>
          <dd>{summary.occupiedTileCount.toLocaleString("en-US")}</dd>
        </div>
      </dl>
      {summary.controlled ? null : (
        <p className="species-scope-note">
          Current while this species shares at least one live tile with ours. These figures cover
          observed members only; hidden population and locations remain unknown.
        </p>
      )}
    </section>
  );
}

export function summarizeSpecies(
  world: ActorWorldProjection,
  speciesId: bigint,
): SpeciesSummary | null {
  const species = world.species.find((candidate) => candidate.speciesId === speciesId);
  if (species === undefined) return null;
  const controlled = speciesId === world.controlledSpeciesId;
  const observedByTile = world.tiles.flatMap((tile) => {
    if (tile.detail.case !== "live") return [];
    const organisms = tile.detail.value.organisms.filter((organism) => organism.speciesId === speciesId);
    return organisms.length === 0 ? [] : [{ tileId: tile.tileId, organisms }];
  });
  const observed = observedByTile.flatMap((entry) => entry.organisms);
  const averageHealthQ = controlled && species.evolution !== undefined
    ? species.evolution.averageHealthQ
    : average(observed, (organism) => organism.relativeHealthQ);

  return {
    speciesId,
    controlled,
    population: species.population,
    averageHealthQ,
    occupiedTileCount: observedByTile.length,
    populationScope: species.populationScope,
  };
}

function average(
  values: readonly OrganismProjection[],
  select: (value: OrganismProjection) => number,
): number | null {
  if (values.length === 0) return null;
  return Math.round(values.reduce((sum, value) => sum + select(value), 0) / values.length);
}

function formatRatio(valueQ: number): string {
  return `${(valueQ / 10_000).toFixed(1)}%`;
}
