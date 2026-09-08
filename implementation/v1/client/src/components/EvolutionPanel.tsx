import { create } from "@bufbuild/protobuf";
import { useEffect, useMemo, useState } from "react";
import {
  ApplySpeciationRequestSchema,
  SpeciationFailure,
  type EvolutionDecisionSurface,
  type SpeciationProposal,
} from "../generated/lyfe/v1/evolution_pb";
import { applySpeciation, previewSpeciation } from "../api/server";
import {
  createProposalRequest,
  isTraitReachable,
  toggleTraitSelection,
} from "../state/evolutionPlanner";

interface EvolutionPanelProps {
  readonly surface: EvolutionDecisionSurface;
  readonly onApplied: () => void;
}

type PreviewState =
  | { readonly status: "empty" }
  | { readonly status: "loading" }
  | { readonly status: "ready"; readonly proposal: SpeciationProposal }
  | { readonly status: "error"; readonly message: string };

export function EvolutionPanel({ surface, onApplied }: EvolutionPanelProps) {
  const [selectedTraits, setSelectedTraits] = useState<ReadonlySet<number>>(new Set());
  const [selectedTiles, setSelectedTiles] = useState<ReadonlySet<number>>(
    () => new Set(surface.occupiedTiles.slice(0, 1).map((tile) => tile.tileId)),
  );
  const [preview, setPreview] = useState<PreviewState>({ status: "empty" });
  const [applying, setApplying] = useState(false);

  useEffect(() => {
    setSelectedTraits(new Set());
    setSelectedTiles(new Set(surface.occupiedTiles.slice(0, 1).map((tile) => tile.tileId)));
    setPreview({ status: "empty" });
  }, [surface.controlledSpeciesId, surface.evolutionRevision]);

  const request = useMemo(
    () => createProposalRequest(surface, selectedTraits, selectedTiles),
    [surface, selectedTraits, selectedTiles],
  );

  useEffect(() => {
    if (selectedTraits.size === 0 || selectedTiles.size === 0) {
      setPreview({ status: "empty" });
      return;
    }
    const controller = new AbortController();
    setPreview({ status: "loading" });
    previewSpeciation(request, controller.signal)
      .then((proposal) => setPreview({ status: "ready", proposal }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setPreview({
            status: "error",
            message: error instanceof Error ? error.message : "Preview failed.",
          });
        }
      });
    return () => controller.abort();
  }, [request, selectedTraits.size, selectedTiles.size]);

  const selectable = surface.traits.filter((trait) => trait.selectable && !trait.acquired);
  const acquiredNames = surface.traits.filter((trait) => trait.acquired)
    .map((trait) => trait.displayName).join(", ");
  const proposal = preview.status === "ready" ? preview.proposal : null;

  const apply = async () => {
    if (proposal?.accepted !== true) return;
    setApplying(true);
    const controller = new AbortController();
    try {
      const response = await applySpeciation(create(ApplySpeciationRequestSchema, {
        clientCommandId: crypto.randomUUID(),
        proposal: request,
      }), controller.signal);
      if (response.applied) onApplied();
      else setPreview({ status: "ready", proposal: response.proposal! });
    } catch (error: unknown) {
      setPreview({
        status: "error",
        message: error instanceof Error ? error.message : "Mutation failed.",
      });
    } finally {
      setApplying(false);
    }
  };

  return (
    <section className="evolution-panel" aria-labelledby="evolution-title">
      <div className="evolution-header">
        <div>
          <p className="section-label">Species decision</p>
          <h2 id="evolution-title">Branch the tree of life</h2>
          <p>
            Choose new DNA and the population that will found a descendant species.
            Previewing never spends mutation points.
          </p>
        </div>
        <dl className="evolution-vitals">
          <div><dt>Mutation balance</dt><dd>{formatMutationQ(surface.mutationBalanceQ)} MP</dd></div>
          <div><dt>Current income</dt><dd>{surface.completedTick === 0n ? "Pending first tick" : `${formatMutationQ(surface.lastMutationIncomeQ)} MP/hour`}</dd></div>
          <div><dt>Average health</dt><dd>{surface.completedTick === 0n ? "Pending first tick" : formatRatio(surface.averageHealthQ)}</dd></div>
          <div><dt>Change capacity</dt><dd>{surface.maximumChangeComplexity}</dd></div>
        </dl>
      </div>

      <p className="acquired-traits"><strong>Current DNA:</strong> {acquiredNames}</p>
      <div className="evolution-builder">
        <fieldset className="trait-picker">
          <legend>New traits</legend>
          {selectable.map((trait) => {
            const reachable = isTraitReachable(surface, trait.traitId);
            return (
            <label key={trait.traitId} className={selectedTraits.has(trait.traitId) ? "selected" : !reachable ? "locked" : ""}>
              <input
                type="checkbox"
                checked={selectedTraits.has(trait.traitId)}
                disabled={!reachable}
                onChange={() => setSelectedTraits((current) =>
                  toggleTraitSelection(surface, current, trait.traitId))}
              />
              <span>
                <strong>{trait.displayName}</strong>
                <small>{trait.family} · {formatMutationQ(trait.mutationPointCostQ)} MP · complexity {trait.changeComplexity}</small>
                {trait.prerequisiteTraitIds.length > 0 ? (
                  <small>{reachable ? "Requires" : "Unavailable to this lineage — requires"} {trait.prerequisiteTraitIds.map((id) =>
                    surface.traits.find((candidate) => candidate.traitId === id)?.displayName ?? `trait ${id}`
                  ).join(", ")}</small>
                ) : null}
              </span>
            </label>
          )})}
        </fieldset>

        <fieldset className="tile-picker">
          <legend>Founding tiles</legend>
          {surface.occupiedTiles.map((tile) => (
            <label key={tile.tileId}>
              <input
                type="checkbox"
                checked={selectedTiles.has(tile.tileId)}
                disabled={!selectedTiles.has(tile.tileId) && selectedTiles.size >= 4}
                onChange={() => setSelectedTiles((current) => {
                  const next = new Set(current);
                  if (next.has(tile.tileId)) next.delete(tile.tileId);
                  else next.add(tile.tileId);
                  return next;
                })}
              />
              Tile {tile.tileId} <small>{tile.population.toLocaleString("en-US")} organisms</small>
            </label>
          ))}
        </fieldset>

        <ProposalSummary state={preview} balanceQ={surface.mutationBalanceQ} />
      </div>
      <button
        className="speciate-button"
        disabled={proposal?.accepted !== true || applying}
        onClick={apply}
      >
        {applying ? "Branching…" : "Found descendant species"}
      </button>
    </section>
  );
}

function ProposalSummary({ state, balanceQ }: {
  readonly state: PreviewState;
  readonly balanceQ: bigint;
}) {
  if (state.status === "empty") return <div className="proposal-summary muted">Select a trait to preview a branch.</div>;
  if (state.status === "loading") return <div className="proposal-summary muted">Evaluating proposal…</div>;
  if (state.status === "error") return <div className="proposal-summary rejected">{state.message}</div>;
  const proposal = state.proposal;
  const shortfall = proposal.mutationPriceQ > balanceQ
    ? proposal.mutationPriceQ - balanceQ
    : 0n;
  return (
    <div className={`proposal-summary ${proposal.accepted ? "accepted" : "rejected"}`}>
      <strong>{proposal.accepted ? "Ready to branch" : failureLabel(proposal.failure)}</strong>
      <dl>
        <div><dt>Price</dt><dd>{formatMutationQ(proposal.mutationPriceQ)} MP</dd></div>
        <div><dt>Complexity</dt><dd>{proposal.changeComplexity}</dd></div>
        <div><dt>Descendant founders</dt><dd>{proposal.descendantPopulation.toLocaleString("en-US")}</dd></div>
        <div><dt>Ancestor remains</dt><dd>{proposal.ancestorPopulationAfter.toLocaleString("en-US")}</dd></div>
        {proposal.accepted ? (
          <div><dt>Balance copied to both</dt><dd>{formatMutationQ(proposal.duplicatedBalanceAfterQ)} MP</dd></div>
        ) : shortfall > 0n ? (
          <div><dt>Still needed</dt><dd>{formatMutationQ(shortfall)} MP</dd></div>
        ) : null}
      </dl>
      {proposal.founderCounts.length > 0 ? (
        <small>{proposal.founderCounts.map((founder) =>
          `${founder.count} from tile ${founder.tileId}`
        ).join(" · ")} · both branches receive a 168-hour cooldown</small>
      ) : null}
    </div>
  );
}

function failureLabel(failure: SpeciationFailure): string {
  switch (failure) {
    case SpeciationFailure.INSUFFICIENT_MUTATION_POINTS: return "Not enough mutation points yet";
    case SpeciationFailure.MISSING_PREREQUISITE: return "A prerequisite is missing";
    case SpeciationFailure.CHANGE_COMPLEXITY_EXCEEDED: return "Too much change for one branch";
    case SpeciationFailure.SPECIATION_COOLDOWN: return "This lineage is still recovering";
    case SpeciationFailure.INCOMPATIBLE_TRAIT: return "Selected traits are incompatible";
    case SpeciationFailure.INVALID_TILE_COUNT: return "Choose one to four occupied tiles";
    default: return "The server rejected this proposal";
  }
}

function formatMutationQ(value: bigint): string {
  const whole = value / 1_000_000n;
  const thousandths = (value % 1_000_000n) / 1_000n;
  return thousandths === 0n
    ? whole.toLocaleString("en-US")
    : `${whole.toLocaleString("en-US")}.${thousandths.toString().padStart(3, "0").replace(/0+$/, "")}`;
}

function formatRatio(value: number): string {
  return `${(value / 10_000).toFixed(1)}%`;
}
