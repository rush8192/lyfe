import { create } from "@bufbuild/protobuf";
import { useEffect, useMemo, useState } from "react";
import {
  ApplySpeciationRequestSchema,
  EvolutionActivationWarningKind,
  EvolutionActivationWarningSeverity,
  EvolutionBenefitTiming,
  EvolutionFollowUpEvidenceKind,
  EvolutionReactionOpportunityStatus,
  EvolutionRecurringCostChannel,
  EvolutionResourceFlowForecastStatus,
  EvolutionStrategicIntent,
  SpeciationFailure,
  type EvolutionDecisionSurface,
  type EvolutionPhenotypeSummary,
  type EvolutionResourceFlowForecast,
  type EvolutionTileActivationEvidence,
  type EvolutionTraitOption,
  type ApplySpeciationResponse,
  type SpeciationProposal,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";
import { applySpeciation, previewSpeciation } from "../api/server";
import {
  createProposalRequest,
  createEvolutionGoal,
  estimateEvolutionGoalAffordability,
  evolutionGoalMilestoneTraitIds,
  evolutionGoalPriceQ,
  evolutionGoalsEqual,
  isTraitReachable,
  readEvolutionGoal,
  removeEvolutionGoal,
  resolveEvolutionGoal,
  toggleTraitSelection,
  writeEvolutionGoal,
  type EvolutionGoal,
  type EvolutionGoalAffordability,
  type EvolutionGoalStorage,
} from "../state/evolutionPlanner";

interface EvolutionPanelProps {
  readonly surface: EvolutionDecisionSurface;
  readonly onApplied: (
    response: ApplySpeciationResponse,
    request: SpeciationProposalRequest,
  ) => void;
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
  const [savedPreview, setSavedPreview] = useState<PreviewState>({ status: "empty" });
  const [applying, setApplying] = useState(false);
  const [savedGoal, setSavedGoal] = useState<EvolutionGoal | null>(null);
  const [goalNotice, setGoalNotice] = useState<string | null>(null);

  useEffect(() => {
    const stored = readGoalFromBrowser(surface);
    const resolved = stored === null ? null : resolveEvolutionGoal(surface, stored);
    setSavedGoal(stored);
    setSelectedTraits(new Set(resolved?.traitIds ?? []));
    setSelectedTiles(new Set(resolved?.tileIds ??
      surface.occupiedTiles.slice(0, 1).map((tile) => tile.tileId)));
    setPreview({ status: "empty" });
    setSavedPreview({ status: "empty" });
    setGoalNotice(stored !== null && resolved === null
      ? "The saved goal no longer matches this species' available traits or occupied tiles."
      : null);
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
  const intentGroups = buildStrategicIntentGroups(selectable);
  const acquiredNames = surface.traits.filter((trait) => trait.acquired)
    .map((trait) => trait.displayName).join(", ");
  const proposal = preview.status === "ready" ? preview.proposal : null;
  const currentGoal = useMemo(() => {
    try {
      return createEvolutionGoal(surface, selectedTraits, selectedTiles);
    } catch {
      return null;
    }
  }, [surface, selectedTraits, selectedTiles]);
  const currentMatchesSaved = savedGoal !== null && currentGoal !== null &&
    evolutionGoalsEqual(savedGoal, currentGoal);
  const resolvedSavedGoal = useMemo(
    () => savedGoal === null ? null : resolveEvolutionGoal(surface, savedGoal),
    [surface, savedGoal],
  );
  const savedRequest = useMemo(
    () => resolvedSavedGoal === null
      ? null
      : createProposalRequest(
        surface,
        new Set(resolvedSavedGoal.traitIds),
        new Set(resolvedSavedGoal.tileIds),
      ),
    [surface, resolvedSavedGoal],
  );

  useEffect(() => {
    if (savedRequest === null || currentMatchesSaved) {
      setSavedPreview({ status: "empty" });
      return;
    }
    const controller = new AbortController();
    setSavedPreview({ status: "loading" });
    previewSpeciation(savedRequest, controller.signal)
      .then((proposal) => setSavedPreview({ status: "ready", proposal }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setSavedPreview({
            status: "error",
            message: error instanceof Error ? error.message : "Saved-goal preview failed.",
          });
        }
      });
    return () => controller.abort();
  }, [savedRequest, currentMatchesSaved]);

  const saveGoal = () => {
    if (currentGoal === null) return;
    const storage = browserGoalStorage();
    if (storage === null) {
      setGoalNotice("Browser storage is unavailable, so this goal could not be saved.");
      return;
    }
    try {
      writeEvolutionGoal(storage, currentGoal);
      setSavedGoal(currentGoal);
      setGoalNotice("Evolution goal saved for this species.");
    } catch (error: unknown) {
      setGoalNotice(error instanceof Error ? error.message : "Evolution goal could not be saved.");
    }
  };

  const loadGoal = () => {
    if (savedGoal === null) return;
    const resolved = resolveEvolutionGoal(surface, savedGoal);
    if (resolved === null) {
      setGoalNotice("The saved goal must be revised for the species' current state.");
      return;
    }
    setSelectedTraits(new Set(resolved.traitIds));
    setSelectedTiles(new Set(resolved.tileIds));
    setPreview({ status: "empty" });
    setGoalNotice("Saved goal loaded into the proposal builder.");
  };

  const clearGoal = () => {
    const storage = browserGoalStorage();
    if (storage === null) {
      setGoalNotice("Browser storage is unavailable, so this goal could not be removed.");
      return;
    }
    try {
      removeEvolutionGoal(storage, surface.worldId, surface.controlledSpeciesId);
      setSavedGoal(null);
      setGoalNotice("Saved evolution goal removed.");
    } catch (error: unknown) {
      setGoalNotice(error instanceof Error ? error.message : "Evolution goal could not be removed.");
    }
  };

  const apply = async () => {
    if (proposal?.accepted !== true) return;
    setApplying(true);
    const controller = new AbortController();
    try {
      const response = await applySpeciation(create(ApplySpeciationRequestSchema, {
        clientCommandId: crypto.randomUUID(),
        proposal: request,
      }), controller.signal);
      if (response.applied) onApplied(response, request);
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
      <SavedEvolutionGoal
        surface={surface}
        goal={savedGoal}
        loaded={currentMatchesSaved}
        onLoad={loadGoal}
        onRemove={clearGoal}
      />
      {goalNotice === null ? null : <p className="goal-notice" role="status">{goalNotice}</p>}
      <div className="evolution-builder">
        <fieldset className="trait-picker">
          <legend>New traits</legend>
          {intentGroups.map((group) => (
            <section className="trait-intent-group" key={group.intent}>
              <header>
                <strong>{strategicIntentLabel(group.intent)}</strong>
                <small>{strategicIntentDetail(group.intent)}</small>
              </header>
              {group.traits.map((trait) => {
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
                      {trait.strategicIntents.length > 1 ? (
                        <small>Also serves {trait.strategicIntents.slice(1).map(strategicIntentLabel).join(", ")}</small>
                      ) : null}
                      {trait.prerequisiteTraitIds.length > 0 ? (
                        <small>{reachable ? "Requires" : "Unavailable to this lineage — requires"} {trait.prerequisiteTraitIds.map((id) =>
                          surface.traits.find((candidate) => candidate.traitId === id)?.displayName ?? `trait ${id}`
                        ).join(", ")}</small>
                      ) : null}
                    </span>
                  </label>
                );
              })}
            </section>
          ))}
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

        <ProposalSummary state={preview} surface={surface} />
      </div>
      {resolvedSavedGoal !== null && currentGoal !== null && !currentMatchesSaved ? (
        <ProposalComparison
          surface={surface}
          savedGoal={resolvedSavedGoal}
          currentGoal={currentGoal}
          savedState={savedPreview}
          currentState={preview}
        />
      ) : null}
      <div className="evolution-actions">
        <button
          className="goal-save-button"
          disabled={currentGoal === null || currentMatchesSaved}
          onClick={saveGoal}
        >
          {savedGoal === null ? "Save as evolution goal" :
            currentMatchesSaved ? "Evolution goal saved" : "Update saved goal"}
        </button>
        <button
          className="speciate-button"
          disabled={proposal?.accepted !== true || applying}
          onClick={apply}
        >
          {applying ? "Branching…" : "Found descendant species"}
        </button>
      </div>
    </section>
  );
}

function SavedEvolutionGoal({ surface, goal, loaded, onLoad, onRemove }: {
  readonly surface: EvolutionDecisionSurface;
  readonly goal: EvolutionGoal | null;
  readonly loaded: boolean;
  readonly onLoad: () => void;
  readonly onRemove: () => void;
}) {
  if (goal === null) {
    return (
      <section className="evolution-goal empty" aria-labelledby="evolution-goal-title">
        <div>
          <p className="section-label">Pinned plan</p>
          <h3 id="evolution-goal-title">No evolution goal saved</h3>
        </div>
        <p>Select a prerequisite-closed proposal below to preserve one goal for this species.</p>
      </section>
    );
  }
  const resolved = resolveEvolutionGoal(surface, goal);
  if (resolved === null) {
    return (
      <section className="evolution-goal stale" aria-labelledby="evolution-goal-title">
        <div>
          <p className="section-label">Pinned plan</p>
          <h3 id="evolution-goal-title">Saved goal needs revision</h3>
        </div>
        <p>Its traits or founding tiles are no longer available to this species.</p>
        <div className="goal-actions"><button onClick={onRemove}>Remove goal</button></div>
      </section>
    );
  }

  const priceQ = evolutionGoalPriceQ(surface, resolved);
  const affordability = estimateEvolutionGoalAffordability(surface, priceQ);
  const milestoneIds = new Set(evolutionGoalMilestoneTraitIds(surface, resolved));
  const traitName = (id: number) => surface.traits
    .find((trait) => trait.traitId === id)?.displayName ?? `Trait ${id}`;
  const milestones = resolved.traitIds.filter((id) => milestoneIds.has(id)).map(traitName);
  const prerequisites = resolved.traitIds.filter((id) => !milestoneIds.has(id)).map(traitName);
  const cooldownTicks = surface.speciationNotBeforeTick > surface.completedTick
    ? surface.speciationNotBeforeTick - surface.completedTick
    : 0n;
  const cooldownHours = cooldownTicks * BigInt(surface.tickDurationHours);

  return (
    <section className="evolution-goal" aria-labelledby="evolution-goal-title">
      <header>
        <div>
          <p className="section-label">Pinned plan</p>
          <h3 id="evolution-goal-title">{milestones.join(" + ")}</h3>
        </div>
        <span>{loaded ? "Loaded" : "Saved"}</span>
      </header>
      <dl>
        <div><dt>Total price</dt><dd>{formatMutationQ(priceQ)} MP</dd></div>
        <div><dt>Current balance</dt><dd>{formatMutationQ(surface.mutationBalanceQ)} MP</dd></div>
        <div><dt>Still needed</dt><dd>{formatMutationQ(affordability.remainingQ)} MP</dd></div>
        <div>
          <dt>Estimated affordability at current rate</dt>
          <dd>{affordabilityLabel(affordability)}</dd>
        </div>
      </dl>
      <p><strong>Prerequisite closure:</strong> {prerequisites.length > 0
        ? prerequisites.join(" → ")
        : "No additional traits required."}</p>
      <p><strong>Founding tiles:</strong> {resolved.tileIds.join(", ")}</p>
      {cooldownHours > 0n ? (
        <p><strong>Separate branch cooldown:</strong> {formatSimulatedDuration(cooldownHours)} remaining.</p>
      ) : null}
      <small>
        The estimate assumes the last completed mutation income continues unchanged. This goal
        reserves no points, applies nothing automatically, and is not a survival forecast.
      </small>
      <div className="goal-actions">
        <button disabled={loaded} onClick={onLoad}>{loaded ? "Loaded in builder" : "Load goal"}</button>
        <button onClick={onRemove}>Remove goal</button>
      </div>
    </section>
  );
}

function ProposalSummary({ state, surface }: {
  readonly state: PreviewState;
  readonly surface: EvolutionDecisionSurface;
}) {
  if (state.status === "empty") return <div className="proposal-summary muted">Select a trait to preview a branch.</div>;
  if (state.status === "loading") return <div className="proposal-summary muted">Evaluating proposal…</div>;
  if (state.status === "error") return <div className="proposal-summary rejected">{state.message}</div>;
  const proposal = state.proposal;
  const shortfall = proposal.mutationPriceQ > surface.mutationBalanceQ
    ? proposal.mutationPriceQ - surface.mutationBalanceQ
    : 0n;
  const comparison = proposal.currentPhenotype !== undefined &&
    proposal.proposedPhenotype !== undefined
    ? buildPhenotypeComparisonRows(proposal.currentPhenotype, proposal.proposedPhenotype)
    : [];
  const recurringCosts = proposal.currentPhenotype !== undefined &&
    proposal.proposedPhenotype !== undefined
    ? buildRecurringCostComparisonRows(proposal.currentPhenotype, proposal.proposedPhenotype)
    : [];
  return (
    <div className={`proposal-summary ${proposal.accepted ? "accepted" : "rejected"}`}>
      <div className="proposal-status">
        <strong>{proposal.accepted ? "Ready to branch" : failureLabel(proposal.failure)}</strong>
        {proposal.benefitTiming !== EvolutionBenefitTiming.UNSPECIFIED ? (
          <span className={`benefit-timing timing-${proposal.benefitTiming}`}>
            {benefitTimingLabel(proposal.benefitTiming)}
          </span>
        ) : null}
      </div>
      {proposal.strategicIntents.length > 0 ? (
        <div className="proposal-intents" aria-label="Strategic intent">
          <span>Strategic intent</span>
          {proposal.strategicIntents.map((intent) => (
            <strong key={intent}>{strategicIntentLabel(intent)}</strong>
          ))}
        </div>
      ) : null}
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
      {proposal.consequenceFollowUpHours > 0n ? (
        <p className="proposal-follow-up">
          <strong>Longer chronicle landmark:</strong>{" "}
          {formatFollowUpHours(proposal.consequenceFollowUpHours)} after branching, focused on{" "}
          {followUpEvidenceLabel(proposal.consequenceEvidenceKind)}. This does not extend the
          168-hour speciation cooldown.
        </p>
      ) : null}
      {proposal.activationWarnings.length > 0 ? (
        <ul className="activation-warnings" aria-label="Activation warnings">
          {proposal.activationWarnings.map((warning) => (
            <li
              key={`${warning.kind}:${warning.traitIds.join(":")}`}
              className={warning.severity === EvolutionActivationWarningSeverity.CAUTION
                ? "caution"
                : "information"}
            >
              <strong>{activationWarningTitle(warning.kind)}</strong>
              <span>{activationWarningDetail(warning.kind)}</span>
              {warning.traitIds.length > 0 ? (
                <small>Applies to {warning.traitIds.map((id) =>
                  surface.traits.find((trait) => trait.traitId === id)?.displayName ?? `trait ${id}`
                ).join(", ")}</small>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
      {comparison.length > 0 ? (
        <div className="phenotype-comparison">
          <h4>Compiled organism comparison</h4>
          <p>Current inherited phenotype versus descendant genetic potential. This is not a survival forecast.</p>
          <table>
            <thead><tr><th>Attribute</th><th>Current</th><th>Descendant</th></tr></thead>
            <tbody>
              {comparison.map((row) => (
                <tr key={row.label} className={row.changed ? "changed" : "unchanged"}>
                  <th scope="row">{row.label}</th>
                  <td>{row.current}</td>
                  <td>{row.proposed}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <h5>Recurring cost per organism-hour</h5>
          <table>
            <thead><tr><th>Channel</th><th>Current</th><th>Descendant</th></tr></thead>
            <tbody>
              {recurringCosts.map((row) => (
                <tr key={row.label} className={row.changed ? "changed" : "unchanged"}>
                  <th scope="row">{row.label}</th>
                  <td>{row.current}</td>
                  <td>{row.proposed}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {proposal.tileActivationEvidence.length > 0 ? (
        <TileActivationEvidenceList evidence={proposal.tileActivationEvidence} surface={surface} />
      ) : null}
    </div>
  );
}

function ProposalComparison({
  surface,
  savedGoal,
  currentGoal,
  savedState,
  currentState,
}: {
  readonly surface: EvolutionDecisionSurface;
  readonly savedGoal: EvolutionGoal;
  readonly currentGoal: EvolutionGoal;
  readonly savedState: PreviewState;
  readonly currentState: PreviewState;
}) {
  const savedProposal = savedState.status === "ready" ? savedState.proposal : null;
  const currentProposal = currentState.status === "ready" ? currentState.proposal : null;
  const rows = savedProposal === null || currentProposal === null
    ? []
    : buildProposalComparisonRows(
      surface,
      savedGoal,
      savedProposal,
      currentGoal,
      currentProposal,
    );
  const phenotypeRows = savedProposal?.proposedPhenotype === undefined ||
    currentProposal?.proposedPhenotype === undefined
    ? []
    : buildPhenotypeComparisonRows(
      savedProposal.proposedPhenotype,
      currentProposal.proposedPhenotype,
    );
  const recurringRows = savedProposal?.proposedPhenotype === undefined ||
    currentProposal?.proposedPhenotype === undefined
    ? []
    : buildRecurringCostComparisonRows(
      savedProposal.proposedPhenotype,
      currentProposal.proposedPhenotype,
    );
  return (
    <section className="proposal-comparison" aria-labelledby="proposal-comparison-title">
      <header>
        <div>
          <p className="section-label">Decision workspace</p>
          <h3 id="proposal-comparison-title">Compare branch proposals</h3>
        </div>
        <span>Live authoritative previews</span>
      </header>
      <p>
        The pinned goal is the reference plan. The current builder remains the only proposal that
        can be applied.
      </p>
      {rows.length === 0 ? (
        <div className="proposal-comparison-loading">
          <ProposalComparisonState label="Pinned goal" state={savedState} />
          <ProposalComparisonState label="Current builder" state={currentState} />
        </div>
      ) : (
        <>
          <table>
            <thead>
              <tr><th>Decision evidence</th><th>Pinned goal</th><th>Current builder</th></tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.label} className={row.changed ? "changed" : "unchanged"}>
                  <th scope="row">{row.label}</th>
                  <td>{row.saved}</td>
                  <td>{row.current}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {phenotypeRows.length > 0 ? (
            <div className="proposal-descendant-comparison">
              <h4>Descendant phenotype</h4>
              <table>
                <thead>
                  <tr><th>Compiled attribute</th><th>Pinned descendant</th><th>Builder descendant</th></tr>
                </thead>
                <tbody>
                  {phenotypeRows.map((row) => (
                    <tr key={row.label} className={row.changed ? "changed" : "unchanged"}>
                      <th scope="row">{row.label}</th>
                      <td>{row.current}</td>
                      <td>{row.proposed}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <h4>Recurring cost per organism-hour</h4>
              <table>
                <thead>
                  <tr><th>Channel</th><th>Pinned descendant</th><th>Builder descendant</th></tr>
                </thead>
                <tbody>
                  {recurringRows.map((row) => (
                    <tr key={row.label} className={row.changed ? "changed" : "unchanged"}>
                      <th scope="row">{row.label}</th>
                      <td>{row.current}</td>
                      <td>{row.proposed}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </>
      )}
      <small>
        This comparison ranks neither plan. It contrasts current observed evidence and compiled
        consequences; it does not simulate future randomness, hidden competitors, or survival.
      </small>
    </section>
  );
}

function ProposalComparisonState({ label, state }: {
  readonly label: string;
  readonly state: PreviewState;
}) {
  const detail = state.status === "loading"
    ? "Evaluating…"
    : state.status === "error"
      ? state.message
      : state.status === "empty"
        ? "Waiting for a valid plan."
        : "Ready";
  return <div className={state.status === "error" ? "error" : ""}><strong>{label}</strong><span>{detail}</span></div>;
}

export interface ProposalComparisonRow {
  readonly label: string;
  readonly saved: string;
  readonly current: string;
  readonly changed: boolean;
}

export function buildProposalComparisonRows(
  surface: EvolutionDecisionSurface,
  savedGoal: EvolutionGoal,
  savedProposal: SpeciationProposal,
  currentGoal: EvolutionGoal,
  currentProposal: SpeciationProposal,
): ProposalComparisonRow[] {
  const traitName = (id: number) => surface.traits
    .find((trait) => trait.traitId === id)?.displayName ?? `Trait ${id}`;
  const value = (label: string, saved: string, current: string): ProposalComparisonRow => ({
    label,
    saved,
    current,
    changed: saved !== current,
  });
  const milestones = (goal: EvolutionGoal) => {
    const milestoneIds = new Set(evolutionGoalMilestoneTraitIds(surface, goal));
    return goal.traitIds.filter((id) => milestoneIds.has(id)).map(traitName).join(" + ");
  };
  const prerequisites = (goal: EvolutionGoal) => {
    const milestoneIds = new Set(evolutionGoalMilestoneTraitIds(surface, goal));
    const names = goal.traitIds.filter((id) => !milestoneIds.has(id)).map(traitName);
    return names.length === 0 ? "None" : names.join(" → ");
  };
  return [
    value("Milestone", milestones(savedGoal), milestones(currentGoal)),
    value("Prerequisite closure", prerequisites(savedGoal), prerequisites(currentGoal)),
    value(
      "Strategic intent",
      proposalIntentSummary(savedProposal),
      proposalIntentSummary(currentProposal),
    ),
    value(
      "Benefit timing",
      benefitTimingLabel(savedProposal.benefitTiming),
      benefitTimingLabel(currentProposal.benefitTiming),
    ),
    value(
      "Follow-up landmark",
      proposalFollowUpSummary(savedProposal),
      proposalFollowUpSummary(currentProposal),
    ),
    value(
      "Authoritative status",
      proposalStatusLabel(savedProposal),
      proposalStatusLabel(currentProposal),
    ),
    value(
      "Price",
      `${formatMutationQ(savedProposal.mutationPriceQ)} MP`,
      `${formatMutationQ(currentProposal.mutationPriceQ)} MP`,
    ),
    value(
      "Complexity",
      savedProposal.changeComplexity.toString(),
      currentProposal.changeComplexity.toString(),
    ),
    value(
      "Balance after price",
      proposalBalanceLabel(savedProposal),
      proposalBalanceLabel(currentProposal),
    ),
    value(
      "Founding plan",
      proposalFounderSummary(savedProposal),
      proposalFounderSummary(currentProposal),
    ),
    value(
      "Ancestor remains",
      savedProposal.ancestorPopulationAfter.toLocaleString("en-US"),
      currentProposal.ancestorPopulationAfter.toLocaleString("en-US"),
    ),
    value(
      "Activation warnings",
      proposalWarningSummary(savedProposal),
      proposalWarningSummary(currentProposal),
    ),
    value(
      "Renewable-flow outlook",
      proposalFlowSummary(savedProposal),
      proposalFlowSummary(currentProposal),
    ),
    value(
      "Latest observed competitor uptake",
      proposalCompetitorSummary(savedProposal),
      proposalCompetitorSummary(currentProposal),
    ),
  ];
}

function proposalIntentSummary(proposal: SpeciationProposal): string {
  return proposal.strategicIntents.length === 0
    ? "Unclassified"
    : proposal.strategicIntents.map(strategicIntentLabel).join(" · ");
}

function proposalFollowUpSummary(proposal: SpeciationProposal): string {
  return proposal.consequenceFollowUpHours === 0n
    ? "Cooldown summary only"
    : `${formatFollowUpHours(proposal.consequenceFollowUpHours)} · ${followUpEvidenceLabel(
      proposal.consequenceEvidenceKind,
    )}`;
}

function proposalStatusLabel(proposal: SpeciationProposal): string {
  return proposal.accepted ? "Affordable and valid now" : failureLabel(proposal.failure);
}

function proposalBalanceLabel(proposal: SpeciationProposal): string {
  return proposal.accepted
    ? `${formatMutationQ(proposal.duplicatedBalanceAfterQ)} MP copied to both branches`
    : "Not available until proposal is accepted";
}

function proposalFounderSummary(proposal: SpeciationProposal): string {
  return proposal.founderCounts.length === 0
    ? "No valid founding cohort"
    : proposal.founderCounts.map((founder) =>
      `${founder.count.toLocaleString("en-US")} from tile ${founder.tileId}`
    ).join(" · ");
}

function proposalWarningSummary(proposal: SpeciationProposal): string {
  return proposal.activationWarnings.length === 0
    ? "None"
    : proposal.activationWarnings.map((warning) => activationWarningTitle(warning.kind)).join(" · ");
}

function proposalFlowSummary(proposal: SpeciationProposal): string {
  const forecasts = proposal.tileActivationEvidence.flatMap((tile) =>
    tile.reactionOpportunities.flatMap((reaction) =>
      reaction.inputs.flatMap((input) => input.flowForecast === undefined
        ? []
        : [input.flowForecast])));
  if (forecasts.length === 0) return "No finite capture-input forecast";
  const waiting = forecasts.filter((forecast) =>
    forecast.status === EvolutionResourceFlowForecastStatus.NO_HISTORY).length;
  if (waiting > 0) return `Waiting for history on ${waiting} of ${forecasts.length} inputs`;
  const overshoot = forecasts.filter((forecast) =>
    forecast.status === EvolutionResourceFlowForecastStatus.EXCEEDS_RECENT_RENEWAL ||
    forecast.status === EvolutionResourceFlowForecastStatus.NO_RECENT_RENEWAL).length;
  return overshoot === 0
    ? `${forecasts.length} of ${forecasts.length} inputs within recent renewal`
    : `Likely overshoot on ${overshoot} of ${forecasts.length} inputs`;
}

function proposalCompetitorSummary(proposal: SpeciationProposal): string {
  const observations = new Map<string, bigint>();
  for (const tile of proposal.tileActivationEvidence) {
    for (const reaction of tile.reactionOpportunities) {
      for (const input of reaction.inputs) {
        if (input.flowForecast === undefined) continue;
        observations.set(
          `${tile.tileId}:${input.resourceId}`,
          input.flowForecast.latestObservedCompetitorUptakeQ,
        );
      }
    }
  }
  if (observations.size === 0) return "No finite capture-input observation";
  const total = [...observations.values()].reduce((sum, amount) => sum + amount, 0n);
  return `${total.toLocaleString("en-US")} q across ${observations.size} tile-resource ${observations.size === 1 ? "observation" : "observations"}`;
}

export interface StrategicIntentGroup {
  readonly intent: EvolutionStrategicIntent;
  readonly traits: readonly EvolutionTraitOption[];
}

export function buildStrategicIntentGroups(
  traits: readonly EvolutionTraitOption[],
): StrategicIntentGroup[] {
  const grouped = new Map<EvolutionStrategicIntent, EvolutionTraitOption[]>();
  for (const trait of traits) {
    const primary = trait.strategicIntents[0] ?? EvolutionStrategicIntent.UNSPECIFIED;
    const group = grouped.get(primary) ?? [];
    group.push(trait);
    grouped.set(primary, group);
  }
  return [...grouped.entries()]
    .sort(([left], [right]) => left - right)
    .map(([intent, groupedTraits]) => ({
      intent,
      traits: groupedTraits.sort((left, right) => left.traitId - right.traitId),
    }));
}

export function strategicIntentLabel(intent: EvolutionStrategicIntent): string {
  switch (intent) {
    case EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE: return "Exploit the current niche";
    case EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE: return "Endure pressure";
    case EvolutionStrategicIntent.ALTER_DISPERSAL: return "Expand or alter dispersal";
    case EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS: return "Diversify resource and energy access";
    case EvolutionStrategicIntent.BIOLOGICAL_INTERACTION: return "Compete, hunt, evade, or defend";
    case EvolutionStrategicIntent.INVEST_IN_COMPLEXITY: return "Invest in future capability";
    default: return "Unclassified strategy";
  }
}

function strategicIntentDetail(intent: EvolutionStrategicIntent): string {
  switch (intent) {
    case EvolutionStrategicIntent.EXPLOIT_CURRENT_NICHE: return "Improve performance where this lineage already succeeds.";
    case EvolutionStrategicIntent.ENDURE_ENVIRONMENTAL_PRESSURE: return "Reduce the cost of scarcity, variation, or toxicity.";
    case EvolutionStrategicIntent.ALTER_DISPERSAL: return "Change how organisms remain, roam, or spread.";
    case EvolutionStrategicIntent.DIVERSIFY_RESOURCE_ENERGY_ACCESS: return "Open another material or energetic route.";
    case EvolutionStrategicIntent.BIOLOGICAL_INTERACTION: return "Change ecological interactions with other organisms.";
    case EvolutionStrategicIntent.INVEST_IN_COMPLEXITY: return "Build regulation, storage, organization, or later options.";
    default: return "Presentation metadata is not available for this trait.";
  }
}

export interface PhenotypeComparisonRow {
  readonly label: string;
  readonly current: string;
  readonly proposed: string;
  readonly changed: boolean;
}

export function buildPhenotypeComparisonRows(
  current: EvolutionPhenotypeSummary,
  proposed: EvolutionPhenotypeSummary,
): PhenotypeComparisonRow[] {
  const row = (label: string, currentValue: string, proposedValue: string) => ({
    label,
    current: currentValue,
    proposed: proposedValue,
    changed: currentValue !== proposedValue,
  });
  return [
    row("Active reactions", current.activeReactionIds.length.toString(), proposed.activeReactionIds.length.toString()),
    row("Capture ceiling", `${current.maximumCaptureExtentsPerHour}/hour`, `${proposed.maximumCaptureExtentsPerHour}/hour`),
    row("Capture efficiency", formatRatio(current.favorableCaptureEfficiencyQ), formatRatio(proposed.favorableCaptureEfficiencyQ)),
    row("Light dependence", current.requiresLight ? "Required" : "Not required", proposed.requiresLight ? "Required" : "Not required"),
    row("Energy reserve", `${current.chargedReserveCapacityQ.toLocaleString("en-US")} q`, `${proposed.chargedReserveCapacityQ.toLocaleString("en-US")} q`),
    row("Reproduction health", formatRatio(current.minimumReproductionHealthQ), formatRatio(proposed.minimumReproductionHealthQ)),
    row("Resource conservation", current.resourceConservation ? "Enabled" : "Disabled", proposed.resourceConservation ? "Enabled" : "Disabled"),
    row("Mutation income", formatRatio(current.mutationIncomeModifierQ), formatRatio(proposed.mutationIncomeModifierQ)),
    row("Future change capacity", current.maximumChangeComplexity.toString(), proposed.maximumChangeComplexity.toString()),
  ];
}

export function buildRecurringCostComparisonRows(
  current: EvolutionPhenotypeSummary,
  proposed: EvolutionPhenotypeSummary,
): PhenotypeComparisonRow[] {
  const currentByChannel = new Map(current.recurringCosts.map((cost) => [cost.channel, cost]));
  const proposedByChannel = new Map(proposed.recurringCosts.map((cost) => [cost.channel, cost]));
  const channels = [...new Set([...currentByChannel.keys(), ...proposedByChannel.keys()])]
    .sort((left, right) => left - right);
  return channels.map((channel) => {
    const currentQ = currentByChannel.get(channel)?.amountQPerOrganismHour ?? 0n;
    const proposedQ = proposedByChannel.get(channel)?.amountQPerOrganismHour ?? 0n;
    return {
      label: recurringCostLabel(channel),
      current: `${currentQ.toLocaleString("en-US")} q/hour`,
      proposed: `${proposedQ.toLocaleString("en-US")} q/hour`,
      changed: currentQ !== proposedQ,
    };
  });
}

function recurringCostLabel(channel: EvolutionRecurringCostChannel): string {
  switch (channel) {
    case EvolutionRecurringCostChannel.MANDATORY_MAINTENANCE: return "Mandatory maintenance";
    default: return "Unclassified recurring cost";
  }
}

function TileActivationEvidenceList({ evidence, surface }: {
  readonly evidence: readonly EvolutionTileActivationEvidence[];
  readonly surface: EvolutionDecisionSurface;
}) {
  const resourceName = (id: number) => surface.resourceDefinitions
    .find((definition) => definition.resourceId === id)?.displayName ?? `Resource ${id}`;
  const reactionName = (id: number) => surface.reactionDefinitions
    .find((definition) => definition.reactionId === id)?.displayName ?? `Reaction ${id}`;
  return (
    <div className="tile-activation-evidence">
      <h4>Selected tile activation context</h4>
      <p>Current observed conditions for the founding cohort and its inherited capture pathway.</p>
      <div className="tile-activation-grid">
        {evidence.map((tile) => (
          <article key={tile.tileId}>
            <header><strong>Tile {tile.tileId}</strong><span>{tile.founderCount} founders</span></header>
            <dl>
              <div><dt>Health</dt><dd>{formatRatio(tile.averageHealthQ)}</dd></div>
              <div><dt>Reserve</dt><dd>{formatRatio(tile.averageReserveQ)}</dd></div>
              <div><dt>Environmental fit</dt><dd>{formatRatio(tile.averageEnvironmentalFactorQ)}</dd></div>
              <div><dt>Resource pressure</dt><dd>{formatRatio(tile.averageResourcePressureQ)}</dd></div>
              {tile.hasCurrentClimate ? (
                <>
                  <div><dt>Temperature</dt><dd>{(tile.currentTemperatureMilliC / 1_000).toFixed(1)} °C</dd></div>
                  <div><dt>Surface moisture</dt><dd>{formatRatio(tile.currentSurfaceMoistureQ)}</dd></div>
                  <div><dt>Accessible light</dt><dd>{formatRatio(tile.currentAccessibleLightQ)}</dd></div>
                </>
              ) : null}
            </dl>
            {tile.reactionOpportunities.map((reaction) => {
              const limiting = reaction.limitingResourceId === 0
                ? null
                : resourceName(reaction.limitingResourceId);
              return (
                <div className={`reaction-opportunity opportunity-${reaction.status}`} key={reaction.reactionId}>
                  <strong>{reactionName(reaction.reactionId)}</strong>
                  <span>{reactionOpportunityLabel(reaction.status, limiting)}</span>
                  <small>Current accessible stock supports {reaction.stockSupportedExtents.toLocaleString("en-US")} complete extents before replenishment or competition.</small>
                  <ul>
                    {reaction.inputs.filter((input) => !input.inexhaustible).map((input) => (
                      <li key={input.resourceId}>
                        <span>{resourceName(input.resourceId)}: {input.accessibleStockQ.toLocaleString("en-US")} accessible / {input.tileStockQ.toLocaleString("en-US")} stocked; {input.requiredPerExtentQ.toLocaleString("en-US")} per extent</span>
                        {input.flowForecast === undefined ? null : (
                          <ResourceFlowForecast forecast={input.flowForecast} />
                        )}
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
          </article>
        ))}
      </div>
      <small>Stock is a present-boundary diagnostic. Flow comparisons hold the proposed capture rate and current conditions constant across recent observed hours; they are current-state estimates, not survival guarantees. Competition includes only species observed on the selected live tile, and species attribution covers the latest interval only.</small>
    </div>
  );
}

function ResourceFlowForecast({ forecast }: {
  readonly forecast: EvolutionResourceFlowForecast;
}) {
  if (forecast.status === EvolutionResourceFlowForecastStatus.NO_HISTORY) {
    return <div className="resource-flow-forecast">Current-state flow estimate: waiting for the first completed interval.</div>;
  }
  const netRenewal = forecast.recentEnvironmentalInflowQ >
    forecast.recentEnvironmentalOutflowQ
    ? forecast.recentEnvironmentalInflowQ - forecast.recentEnvironmentalOutflowQ
    : 0n;
  const risk = forecast.status === EvolutionResourceFlowForecastStatus.EXCEEDS_RECENT_RENEWAL ||
    forecast.status === EvolutionResourceFlowForecastStatus.NO_RECENT_RENEWAL;
  return (
    <div className={`resource-flow-forecast${risk ? " flow-overshoot" : ""}`}>
      <strong>{risk ? "Likely niche overshoot" : "Within recent renewal"}</strong>
      <span>Current-state estimate over {forecast.historyPeriodHours.toLocaleString("en-US")} h: proposed cohort demand {forecast.proposedCohortDemandQ.toLocaleString("en-US")} vs {netRenewal.toLocaleString("en-US")} net renewed ({forecast.recentEnvironmentalInflowQ.toLocaleString("en-US")} in − {forecast.recentEnvironmentalOutflowQ.toLocaleString("en-US")} out).</span>
      <span>Observed organism uptake in that window: {forecast.recentOrganismUptakeQ.toLocaleString("en-US")}.</span>
      <span>Latest {forecast.latestObservationPeriodHours.toLocaleString("en-US")} h observed uptake: controlled lineage {forecast.latestControlledSpeciesUptakeQ.toLocaleString("en-US")}; other observed species {forecast.latestObservedCompetitorUptakeQ.toLocaleString("en-US")}.</span>
    </div>
  );
}

function reactionOpportunityLabel(
  status: EvolutionReactionOpportunityStatus,
  limitingResource: string | null,
): string {
  switch (status) {
    case EvolutionReactionOpportunityStatus.AVAILABLE: return "Inputs currently available";
    case EvolutionReactionOpportunityStatus.RESOURCE_LIMITED:
      return limitingResource === null ? "Currently resource-limited" : `Limited by ${limitingResource}`;
    case EvolutionReactionOpportunityStatus.INACCESSIBLE_LIGHT: return "No accessible light at this hour";
    default: return "Opportunity unavailable";
  }
}

function benefitTimingLabel(value: EvolutionBenefitTiming): string {
  switch (value) {
    case EvolutionBenefitTiming.IMMEDIATE: return "Immediate benefit";
    case EvolutionBenefitTiming.MATURING: return "Maturing benefit";
    case EvolutionBenefitTiming.CONDITIONAL: return "Conditional benefit";
    case EvolutionBenefitTiming.PREPARATORY: return "Preparatory step";
    default: return "Unclassified timing";
  }
}

function formatFollowUpHours(hours: bigint): string {
  return hours % 24n === 0n
    ? `${(hours / 24n).toLocaleString("en-US")} days`
    : `${hours.toLocaleString("en-US")} hours`;
}

function followUpEvidenceLabel(kind: EvolutionFollowUpEvidenceKind): string {
  switch (kind) {
    case EvolutionFollowUpEvidenceKind.CAPABILITY_ACTIVATION:
      return "capability activation";
    case EvolutionFollowUpEvidenceKind.CONDITION_AND_PRESSURE:
      return "condition and pressure";
    case EvolutionFollowUpEvidenceKind.GEOGRAPHIC_SPREAD:
      return "geographic spread";
    case EvolutionFollowUpEvidenceKind.RESERVE_STORAGE:
      return "reserve storage";
    default:
      return "proposal-specific evidence";
  }
}

function activationWarningTitle(kind: EvolutionActivationWarningKind): string {
  switch (kind) {
    case EvolutionActivationWarningKind.NO_COMPILED_CHANGE: return "No immediate organism change";
    case EvolutionActivationWarningKind.RESOURCE_PRESSURE_REQUIRED: return "Activates under resource pressure";
    case EvolutionActivationWarningKind.NO_NEW_ACTIVE_REACTION: return "No new active reaction yet";
    default: return "Activation condition";
  }
}

function activationWarningDetail(kind: EvolutionActivationWarningKind): string {
  switch (kind) {
    case EvolutionActivationWarningKind.NO_COMPILED_CHANGE:
      return "This proposal opens a lineage path, but does not yet change any executed phenotype value shown below.";
    case EvolutionActivationWarningKind.RESOURCE_PRESSURE_REQUIRED:
      return "Conservation changes behavior only after reserve or acquisition pressure crosses its compiled thresholds.";
    case EvolutionActivationWarningKind.NO_NEW_ACTIVE_REACTION:
      return "The selected DNA does not install another resource-consuming or resource-producing reaction by itself.";
    default:
      return "Review the compiled comparison before committing this branch.";
  }
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

function affordabilityLabel(value: EvolutionGoalAffordability): string {
  switch (value.status) {
    case "affordable": return "Affordable now";
    case "pending-first-tick": return "Pending the first completed tick";
    case "no-current-income": return "No ETA at the current zero rate";
    case "estimated": return `About ${formatSimulatedDuration(value.estimatedHours)}`;
  }
}

function formatSimulatedDuration(hours: bigint): string {
  const days = hours / 24n;
  const remainingHours = hours % 24n;
  if (days === 0n) return `${hours.toLocaleString("en-US")} simulated hour${hours === 1n ? "" : "s"}`;
  if (remainingHours === 0n) return `${days.toLocaleString("en-US")} simulated day${days === 1n ? "" : "s"}`;
  return `${days.toLocaleString("en-US")}d ${remainingHours.toString()}h simulated`;
}

function browserGoalStorage(): EvolutionGoalStorage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function readGoalFromBrowser(surface: EvolutionDecisionSurface): EvolutionGoal | null {
  const storage = browserGoalStorage();
  return storage === null ? null : readEvolutionGoal(storage, surface);
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
