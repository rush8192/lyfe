# Founder Setup Packages

Status: first bounded numerical candidate, complete founder material path, and 64-seed generated-world acceptance implemented

Sources: [founding metabolisms](FOUNDING_METABOLISMS.md), [organism health](ORGANISM_HEALTH_CALIBRATION.md), [world/climate calibration](WORLD_CLIMATE_CALIBRATION.md), and [opening strategy validation](OPENING_STRATEGY_VALIDATION.md).

# Purpose

Give the player one small, legible DNA allocation after choosing hydrogen acetogenesis or sulfide anoxygenic phototrophy. The allocation should create a real early tradeoff without erasing the stronger distinction between the two metabolisms, granting a generally superior founder, or pretending that broad environmental adaptation is free.

# First v1 candidate

Each metabolism offers the same three labels. `Balanced` is the canonical reference phenotype already used by the opening fixtures.

| Package | Capture-efficiency multiplier | H₂S/SO₂ threshold multiplier | Intended use |
| --- | ---: | ---: | --- |
| High throughput | `1.0625` | `0.80` | Faster favorable capture, narrower chemical operating range |
| Balanced | `1.00` | `1.00` | Reference compromise |
| Stress tolerant | `0.9375` | `1.25` | Wider chemical operating range, slower favorable capture |

Multipliers apply once to the founder's already distinct values. Capture efficiency is capped at `1.00`; all four chemical soft/hard thresholds are multiplied, but curve shape, temperature tolerance, storage, growth cap, reproduction, and senescence are unchanged. Threshold changes do not create or destroy gas and capture changes do not alter reaction stoichiometry.

The resulting first values are:

| Metabolism/package | Capture efficiency | H₂S soft/hard | SO₂ soft/hard |
| --- | ---: | ---: | ---: |
| Hydrogen / high throughput | `0.85` | `16m / 64m` | `2m / 8m` |
| Hydrogen / balanced | `0.80` | `20m / 80m` | `2.5m / 10m` |
| Hydrogen / stress tolerant | `0.75` | `25m / 100m` | `3.125m / 12.5m` |
| Sulfur / high throughput | `1.00` | `40m / 160m` | `2m / 8m` |
| Sulfur / balanced | `0.95` | `50m / 200m` | `2.5m / 10m` |
| Sulfur / stress tolerant | `0.890625` | `62.5m / 250m` | `3.125m / 12.5m` |

At the explicit opening exposure of `20m H₂S / 5m SO₂`, the high-throughput hydrogen phenotype is just beyond its lowered H₂S soft threshold while the sulfur phenotype remains below its own. The other packages have the same opening factor for both metabolisms:

| Package | Hydrogen environmental/health | Sulfur environmental/health |
| --- | ---: | ---: |
| High throughput | `0.923073 / 0.461536` | `0.925000 / 0.462500` |
| Balanced | `0.966667 / 0.483334` | `0.966667 / 0.483334` |
| Stress tolerant | `0.988000 / 0.494000` | `0.988000 / 0.494000` |

The high-throughput choice remains comfortably above every hard boundary in its intended starting fixture; it is vulnerable, not a disguised difficulty setting. The stress-tolerant choice improves chemical resilience only. It does not authorize land, wider temperature bands, oxygen exposure, non-volcanic metabolism, or missing micronutrient pathways.

# Why these bounds

- A `±6.25%` capture allocation is large enough to matter when capture, substrate contention, or recovery is limiting, but smaller than the founding paths' central throughput and light asymmetry.
- Reciprocal `0.80/1.25` threshold factors make the tolerance exchange easy to explain and preserve ordered soft/hard bands exactly.
- Sulfur's high-throughput path caps at perfect favorable conversion rather than exceeding physical opportunity. Its tolerant path therefore gives up slightly more absolute conversion than hydrogen's; this is an honest consequence of starting near the cap.
- No package changes reaction products, resource capacity, mutation income directly, or hidden difficulty coefficients. Consequences emerge through ordinary reserve, health, reproduction, and mutation-income rules.

# Engine representation

Do not represent these as six unrelated metabolic identities. The persistent domain model should retain:

```text
FounderGenomeId       // hydrogen or sulfur foundation
FounderAllocationId   // high-throughput, balanced, or stress-tolerant
```

The rule compiler resolves that pair into one immutable founder phenotype and includes the allocation in the compiled genome hash. A root genome and its descendants retain the allocation in saves, lineage, projections, and evolution recompilation. `Balanced` is explicit rather than encoded as a missing value. A setup allocation is inherited DNA, but it is not an ordinary zero-cost mutation node and cannot be changed after world creation.

This representation is implemented in official pack `0.9.0`. The stable allocation registry uses `1 = Balanced`, `2 = High throughput`, and `3 = Stress tolerant`; save/payload and world-hash schema `9` persist the selected ID and the resulting committed/free micronutrient state. Survival setup defaults and constrains the autonomous competitor to the scenario-authored Balanced allocation.

This avoids duplicated founder documents, preserves exactly two metabolism cards, and leaves mods free to replace the allocation catalogue or expose different bounded packages. Compilation must reject duplicate IDs, incompatible metabolism restrictions, nonpositive multipliers, capture results above the declared cap, inverted/overflowed thresholds, or a scenario with no allocation for a permitted founder.

# Survival setup

The player chooses one package. The autonomous competitor starts with `Balanced` for the other metabolism in v1; it receives no hidden response to the player's selection. A future scenario may expose an authored competitor allocation policy, but random or counter-picking behavior is not part of the first rules.

The confirmed setup command records both root selections. Preview shows the exact resulting capture efficiency, chemical bands, expected direct health at the selected tile's current stocks, and the warning that climate and future gas flow can change the outcome.

# Validation contract

[founder_setup_package_calibration.py](calibration/founder_setup_package_calibration.py) is the exact fixed-point arithmetic probe for the table above. The production acceptance matrix must then run all six player choices across at least 64 generated seeds with the opposite balanced competitor and require:

1. At least `95%` of each player founder cohort survives to its first reproduction in eligible starts.
2. Every package reaches a meaningful mutation decision; no package turns the intended start into an accidental immediate-loss option.
3. High throughput has strictly greater favorable capture opportunity than Balanced, which is strictly greater than Stress tolerant, before capacity or substrate throttling.
4. Stress tolerant has strictly greater health at the declared elevated H₂S/SO₂ probes than Balanced, which is strictly greater than High throughput.
5. Each package wins at least one authored comparison dimension and loses at least one; population alone is not the only dimension.
6. Hydrogen remains the lower-throughput, easier-escape foundation and sulfur remains the faster, light- and volcanism-dependent specialist under every package.
7. Selection, replay, save/load, worker count, observation, and client subscription do not change results.

If actual population runs make throughput irrelevant because capacity or structural growth always binds, tune these package multipliers or an explicitly disclosed upkeep allocation before shipping. Do not add a hidden fitness bonus to rescue the choice.

# First production-engine probe

The headless `--founder-matrix` scenario constructs all six player choices on seed-generated `32 × 17` worlds, starts the opposite metabolism as a Balanced competitor, and records first reproduction, founder-cohort survival, first 40-point mutation decision, population, health, and state hash. A one-seed, 500-hour probe produced a useful structural failure:

- all six cohorts retained `100/100` founders;
- after correcting a production fixed-point denominator mismatch found by the probe, all six reach the 40-mutation-point scale near the intended hours `303` to `309`;
- no cohort reproduced, and every population remained `100`.

That structural blocker is closed in pack `0.9.0`: mandatory maintenance and conservative
biomass assembly execute after capture, with exact needs-only NH₃/phosphorus/H₂S staging,
compiled-capacity admission, reserve-floor protection, deterministic contention, and balanced
waste. A one-seed, 500-hour generated-world probe passed every choice: hydrogen reproduced at
hour `334`, sulfur at hours `271..272`, `99..100` original founders survived, and each lineage
reached 40 mutation points between hours `336` and `398`.

The complete production acceptance then ran all six player choices across 64 generated
seeds with the opposite Balanced competitor. Persistent fourteen-resource committed/free
inventories were active throughout: founders debited their initial quota, passive keyed
uptake accumulated one additional target quota, and reproduction could not bypass its
material transfer. All `384/384` runs reproduced, all retained at least `95/100` original
founders at first reproduction, and all reached the 40-point mutation decision within the
720-hour horizon. The headless runner uses bounded parallel seed workers with deterministic
world IDs and ordered results; `--include-runs` adds the per-seed evidence and hashes.
