import { create } from "@bufbuild/protobuf";
import { useEffect, useMemo, useState } from "react";
import { GameMode } from "../generated/lyfe/v1/projection_pb";
import {
  CreateWorldRequestSchema,
  FoundingMetabolism,
  type CreateWorldRequest,
  type WorldSetupSurface,
} from "../generated/lyfe/v1/setup_pb";

export interface WorldSetupPanelProps {
  readonly surface: WorldSetupSurface;
  readonly busy: boolean;
  readonly error: string | null;
  readonly onPreviewSeed: (seed: string) => void;
  readonly onCreate: (request: CreateWorldRequest) => void;
}

export function WorldSetupPanel({
  surface,
  busy,
  error,
  onPreviewSeed,
  onCreate,
}: WorldSetupPanelProps) {
  const baseline = surface.allocations.find((value) => value.isBaseline) ?? surface.allocations[0];
  const [seed, setSeed] = useState(surface.rootSeedHex);
  const [mode, setMode] = useState(GameMode.FREE_SANDBOX);
  const [founderId, setFounderId] = useState(surface.founders[0]?.founderGenomeId ?? 0);
  const [allocationId, setAllocationId] = useState(baseline?.founderAllocationId ?? 0);
  const [regionIndex, setRegionIndex] = useState(surface.startingRegions[0]?.startingPairIndex ?? 0);

  useEffect(() => setSeed(surface.rootSeedHex), [surface.rootSeedHex]);
  useEffect(() => {
    if (!surface.founders.some((value) => value.founderGenomeId === founderId)) {
      setFounderId(surface.founders[0]?.founderGenomeId ?? 0);
    }
    if (!surface.allocations.some((value) => value.founderAllocationId === allocationId)) {
      setAllocationId(baseline?.founderAllocationId ?? 0);
    }
    if (!surface.startingRegions.some((value) => value.startingPairIndex === regionIndex)) {
      setRegionIndex(surface.startingRegions[0]?.startingPairIndex ?? 0);
    }
  }, [surface, founderId, allocationId, regionIndex, baseline]);

  const founder = useMemo(
    () => surface.founders.find((value) => value.founderGenomeId === founderId),
    [surface.founders, founderId],
  );
  const allocation = surface.allocations.find(
    (value) => value.founderAllocationId === allocationId,
  );
  const region = surface.startingRegions.find(
    (value) => value.startingPairIndex === regionIndex,
  );
  const validSeed = /^[0-9a-fA-F]{32}$/.test(seed);
  const hydrogen = founder?.metabolism === FoundingMetabolism.HYDROGEN_ACETOGENESIS;

  return (
    <section className="setup-panel" aria-labelledby="setup-title" aria-busy={busy}>
      <header className="setup-header">
        <div>
          <p className="eyebrow">Before the first hour</p>
          <h1 id="setup-title">Kindle a world</h1>
        </div>
        <div className="setup-world-facts">
          <strong>{surface.worldProfileDisplayName}</strong>
          <span>{surface.width} × {surface.height} tiles · {surface.tickDurationHours} simulated hour per tick</span>
          <small>{surface.worldPackId} · {surface.worldPackVersion}</small>
        </div>
      </header>

      <fieldset className="setup-section setup-modes">
        <legend>Choose the stakes</legend>
        <label className={mode === GameMode.FREE_SANDBOX ? "selected" : ""}>
          <input
            type="radio"
            name="game-mode"
            checked={mode === GameMode.FREE_SANDBOX}
            onChange={() => setMode(GameMode.FREE_SANDBOX)}
          />
          <strong>Free sandbox</strong>
          <span>Your lineage is the only beginning. The run ends if all life is gone.</span>
        </label>
        <label className={mode === GameMode.SURVIVAL ? "selected" : ""}>
          <input
            type="radio"
            name="game-mode"
            checked={mode === GameMode.SURVIVAL}
            onChange={() => setMode(GameMode.SURVIVAL)}
          />
          <strong>Survival</strong>
          <span>The other metabolism begins next door under autonomous control. Your lineage must persist.</span>
        </label>
      </fieldset>

      <fieldset className="setup-section">
        <legend>Choose the first metabolism</legend>
        <div className="setup-card-grid">
          {surface.founders.map((option) => (
            <label
              key={option.founderGenomeId}
              className={founderId === option.founderGenomeId ? "setup-card selected" : "setup-card"}
            >
              <input
                type="radio"
                name="founder"
                checked={founderId === option.founderGenomeId}
                onChange={() => setFounderId(option.founderGenomeId)}
              />
              <strong>{option.displayName}</strong>
              <span>{metabolismDescription(option.metabolism, option.requiresLight)}</span>
              <small>
                {formatRatio(option.favorableCaptureEfficiencyQ)} favorable capture · {option.maintenanceCostQPerHour.toLocaleString("en-US")} q maintenance/hour
              </small>
              <small>
                Reproduction cooldown {option.baseReproductionCooldownHours.toLocaleString("en-US")}–{(option.baseReproductionCooldownHours + BigInt(option.reproductionCooldownJitterMaximumHours)).toLocaleString("en-US")} hours
              </small>
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="setup-section">
        <legend>Choose an opening emphasis</legend>
        <div className="setup-card-grid three">
          {surface.allocations.map((option) => (
            <label
              key={option.founderAllocationId}
              className={allocationId === option.founderAllocationId ? "setup-card selected" : "setup-card"}
            >
              <input
                type="radio"
                name="allocation"
                checked={allocationId === option.founderAllocationId}
                onChange={() => setAllocationId(option.founderAllocationId)}
              />
              <strong>{option.displayName}</strong>
              <span>
                Capture {formatSignedRatio(option.captureEfficiencyMultiplierQ)} · chemical tolerance {formatSignedRatio(option.chemicalToleranceMultiplierQ)}
              </span>
              {option.isBaseline ? <small>Scenario baseline</small> : null}
            </label>
          ))}
        </div>
      </fieldset>

      <section className="setup-section seed-section">
        <div>
          <p className="section-label">World seed</p>
          <h2>Choose the ocean</h2>
          <p>The same 32 hexadecimal digits always produce the same geography and candidate beginnings.</p>
        </div>
        <div className="seed-control">
          <input
            aria-label="World seed"
            value={seed}
            spellCheck={false}
            maxLength={32}
            onChange={(event) => setSeed(event.target.value)}
          />
          <button type="button" disabled={busy || !validSeed} onClick={() => onPreviewSeed(seed)}>
            Preview seed
          </button>
        </div>
      </section>

      <fieldset className="setup-section">
        <legend>Choose a starting region</legend>
        <div className="setup-card-grid two">
          {surface.startingRegions.map((option) => {
            const depth = hydrogen ? option.hydrogenDepthMeters : option.sulfurDepthMeters;
            const temperature = hydrogen
              ? option.hydrogenTemperatureMilliC
              : option.sulfurTemperatureMilliC;
            const volcanism = hydrogen ? option.hydrogenVolcanismQ : option.sulfurVolcanismQ;
            return (
              <label
                key={option.startingPairIndex}
                className={regionIndex === option.startingPairIndex ? "setup-card selected" : "setup-card"}
              >
                <input
                  type="radio"
                  name="region"
                  checked={regionIndex === option.startingPairIndex}
                  onChange={() => setRegionIndex(option.startingPairIndex)}
                />
                <strong>Region {option.startingPairIndex + 1}</strong>
                <span>{depth} m deep · {(temperature / 1_000).toFixed(1)} °C</span>
                <small>{formatRatio(volcanism)} volcanic activity{option.wasRepaired ? " · bounded start repair applied" : ""}</small>
              </label>
            );
          })}
        </div>
        <p className="setup-caveat">
          Setup reveals only the bounded candidate-region conditions. Exact resources and the rest of the map remain for observation.
        </p>
      </fieldset>

      <footer className="setup-commit">
        <div>
          <strong>{surface.founderPopulation.toLocaleString("en-US")} founders</strong>
          <span>
            {founder?.displayName ?? "Founder"} · {allocation?.displayName ?? "allocation"} · region {(region?.startingPairIndex ?? 0) + 1}
          </span>
        </div>
        <button
          type="button"
          disabled={busy || !validSeed || founder === undefined || allocation === undefined || region === undefined}
          onClick={() => onCreate(create(CreateWorldRequestSchema, {
            rootSeedHex: seed,
            mode,
            founderGenomeId: founderId,
            founderAllocationId: allocationId,
            startingPairIndex: regionIndex,
          }))}
        >
          {busy ? "Creating world…" : "Begin at hour zero"}
        </button>
      </footer>
      {error === null ? null : <p className="setup-error" role="alert">{error}</p>}
    </section>
  );
}

function metabolismDescription(value: FoundingMetabolism, requiresLight: boolean): string {
  if (value === FoundingMetabolism.HYDROGEN_ACETOGENESIS) {
    return "Draws energy from hydrogen and carbon dioxide around a deep volcanic vent.";
  }
  if (value === FoundingMetabolism.SULFIDE_PHOTOTROPHY) {
    return `Uses sulfide chemistry${requiresLight ? " and accessible light" : ""} in shallower water.`;
  }
  return "Opening metabolism unavailable.";
}

function formatRatio(value: number): string {
  return `${(value / 10_000).toFixed(1)}%`;
}

function formatSignedRatio(value: number): string {
  const percent = (value - 1_000_000) / 10_000;
  return `${percent >= 0 ? "+" : ""}${percent.toFixed(1)}%`;
}
