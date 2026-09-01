# Resource Calibration and Numeric Range Proof

Status: first deep-dive draft; reference scales selected, gameplay rates provisional

Sources: [resource model](RESOURCE_MODEL.md), [data model](DATA_MODEL.md), and [validation plan](VALIDATION_AND_PERFORMANCE.md).

# Purpose

Select coherent v1 matter, biomass, and energy scales; demonstrate that they offer useful gameplay precision; and prove that representative and stress-case calculations fit the selected integer representations.

This document distinguishes three kinds of value:

- **Accounting decisions** define units and must remain stable within a rules version.
- **Reference scales** give systems a shared baseline and may change only through an explicit rule-pack revision.
- **Balance examples** exercise the ranges but remain freely tunable before gameplay validation.

# Decision summary

V1 uses game-native stoichiometric quantities rather than literal atoms, grams, or moles.

| Decision | V1 value |
| --- | --- |
| Matter quantity | Signed 64-bit nonnegative integer resource quanta |
| Matter quantum | One game-native stoichiometric unit; no required physical conversion |
| Wide arithmetic | Checked signed 128-bit intermediates and reconciliation totals |
| Structural biomass formula | `C100 H170 O40 N20 P2 S1` |
| Baseline mature structure | 1,000 `StructuralBiomass` units |
| Primitive hard viable-structure floor | Provisional 500 units; `DirectLifecycle` fission still requires 1,000 mature units per result |
| Reserve carrier | `ReserveOrganic`, composition `CH2O` |
| Energy unit | One `ReserveOrganic` quantum stores one energy quantum |
| Baseline reserve capacity | 10,000 energy/reserve quanta |
| Primitive dissolved-macronutrient store | 512 expanded-matter load units |
| Primitive free-micronutrient store | 64 expanded-matter load units |
| Primitive ingested-matter buffer | 0 load units; disabled until unlocked |
| Primitive micronutrient uptake | One-quantum opportunity with `0.5` probability per organism-hour; shared across quota resources |
| Micronutrients | Separate DNA-defined quotas, not embedded in structural biomass |
| Normal tile pool target | Up to approximately `10^12` quanta per resource |
| Normal account target | Below `10^15` quanta |
| Hard configured account ceiling | `10^17` quanta |
| Engineering tick horizon | `10^10` one-hour ticks for range analysis |

The values describe the first rule pack and benchmark. Traits may change an organism's structural target and reserve capacity without changing the unit definitions.

# Why the units are game-native

Individual bacteria span orders of magnitude in dry mass. A representative bacterium contains billions of atoms, while any geographically meaningful tile contains vastly more environmental matter. Literal atomic or molar fixed-point units fine enough for organism-scale transfers would exhaust 64-bit tile accounts; units large enough for tile-scale pools would erase meaningful organism-scale differences.

LYFE therefore preserves stoichiometric relationships without claiming a fixed physical volume or mass for a tile. Scientific measurements guide ratios, metabolic relationships, and plausible pressures, while the authoritative accounting remains normalized.

The client should initially display LYFE resource units, normalized concentrations, percentages, or rates. A future educational physical-scale overlay would be presentation metadata, not authoritative state.

# Reference biological composition

## Structural biomass

One structural unit expands to:

```text
StructuralBiomass:
    C = 100
    H = 170
    N = 20
    O = 40
    P = 2
    S = 1
```

In the canonical `[C,H,N,O,P,S]` vector order this is:

```text
[100, 170, 20, 40, 2, 1]
```

The formula is a rounded game abstraction anchored by reported average dry-bacterial composition around `CH1.7O0.4N0.2`, with smaller phosphorus and sulfur requirements. It is intentionally stable and simple rather than species-specific.

One structural unit contains 333 elemental matter quanta. A baseline mature organism with 1,000 structural units therefore contains:

| Element | Quantity |
| --- | ---: |
| Carbon | 100,000 |
| Hydrogen | 170,000 |
| Nitrogen | 20,000 |
| Oxygen | 40,000 |
| Phosphorus | 2,000 |
| Sulfur | 1,000 |
| **Total** | **333,000** |

Micronutrients remain separate because their requirements are much smaller and differ by metabolic capability. Each compiled DNA definition supplies minimum viable and preferred quotas by micronutrient. Reproduction must allocate at least the minimum quota to both resulting organisms.

## Energy reserve

`ReserveOrganic` has composition `[1,2,0,1,0,0]`. Its energy density defines the energy unit:

```text
1 ReserveOrganic = 1 stored-energy quantum
```

A baseline capacity of 10,000 reserve units contains 40,000 elemental matter quanta. A mature organism at full baseline reserve therefore accounts for:

```text
333,000 structural matter quanta
 40,000 reserve matter quanta
--------------------------------
373,000 total tracked matter quanta
```

Available nutrient stores and micronutrients add to this total but are excluded from the reference minimum because founders begin with zero free inventory. Store use is measured in expanded tracked matter rather than raw resource quanta: one unit for each CHNOPS or micronutrient quantum in the resource composition. Thus one `NH3` quantum occupies four load units and one `H2S` quantum occupies three. The exact capacity contract and founder derivation are in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

# Numeric domains and limits

| Domain | Storage | Required validation |
| --- | --- | --- |
| One authoritative account | Signed 64-bit integer, `0..10^17` configured | Reject negative, overflow, or configured-ceiling violation |
| Entity and tile totals | Checked `Int128` during calculation | Must fit destination `long` before state application |
| Element-vector expansion | Checked `Int128` | Coefficient multiplication must not overflow |
| Claim totals and proportional products | Checked `Int128` | Grant sum must not exceed availability |
| World reconciliation | Checked `Int128` | Exact equality after declared boundaries |
| Historical bucket | Signed 64-bit when bounded by schema | Rotate or reject before overflow |
| Lifetime/cross-world diagnostic total | Checked `Int128` | Never serialize through a JavaScript `number` |

The configured `10^17` account ceiling is 92 times below `long.MaxValue`, leaving room for safe direct additions after checking. Intermediate arithmetic must still use `Int128`: expanding `10^17` structural units by the hydrogen coefficient 170 yields `1.7 × 10^19`, already above signed 64-bit range.

# Scenario 1: smallest meaningful operation

## Matter precision

- One elemental quantum is approximately `1 / 373,000` of the tracked matter in the baseline full-reserve organism: about `0.000268%`.
- One structural unit is `0.1%` of the baseline mature structural target.
- One reserve quantum is `0.01%` of baseline energy capacity.

The hydrogen reference reaction has a minimum whole-number extent of:

```text
4 H2 + 2 CO2
    -> 2 ReserveOrganic + 2 H2O(boundary)
```

One successful extent fills `0.02%` of baseline reserve capacity. This is fine enough for probabilistic per-tick metabolism without fractional matter, while persisted rate/cost remainders cover sub-quantum averages.

## Energy precision

For illustrative maintenance costs:

| Maintenance per hourly tick | Full-reserve buffer | Approximate days |
| ---: | ---: | ---: |
| 10 | 1,000 hours | 41.7 |
| 25 | 400 hours | 16.7 |
| 50 | 200 hours | 8.3 |
| 100 | 100 hours | 4.2 |

This spans the desired day-to-month organism timescale. The table guides later balance work; it does not select the final maintenance cost.

**Result:** the selected quanta provide adequate organism-scale precision.

# Scenario 2: one organism growing and reproducing

This range proof uses the first rule's true split and 500-energy reproductive-work cost.

Before reproduction the parent has grown to 2,000 structural units and holds 10,000 reserve units:

| Element | Before |
| --- | ---: |
| C | 210,000 |
| H | 360,000 |
| N | 40,000 |
| O | 90,000 |
| P | 4,000 |
| S | 2,000 |

Spending 500 reserve units:

- Reduces stored energy from 10,000 to 9,500.
- Converts the carrier matter into generic organic `C500 H1000 O500`.
- Records 500 dissipated energy units.
- Does not remove matter.

An even split then gives each result:

```text
1,000 StructuralBiomass
4,750 ReserveOrganic
OrganicC   250
OrganicH   500
OrganicO   250
```

Combined post-split elemental totals are exactly:

| Element | After | Difference |
| --- | ---: | ---: |
| C | 210,000 | 0 |
| H | 360,000 | 0 |
| N | 40,000 | 0 |
| O | 90,000 | 0 |
| P | 4,000 | 0 |
| S | 2,000 | 0 |

Both organisms meet the 1,000-unit structure target. Micronutrient quotas must pass the same before/after allocation test once their values are selected.

**Result:** ordinary lifecycle arithmetic is many orders of magnitude below account limits and reconciles exactly.

# Scenario 3: heavily populated tile

Use a hotspot of 25,000 organisms, each with baseline mature structure and a full reserve:

| Quantity | Total |
| --- | ---: |
| Structural units | 25,000,000 |
| Elemental matter represented by structure | 8,325,000,000 |
| Reserve units | 250,000,000 |
| Elemental matter represented by reserves | 1,000,000,000 |
| **Total organism matter** | **9,325,000,000** |

A tile with 34 resource slots at the normal maximum of `10^12` each contains at most `3.4 × 10^13` directly stored resource units before composition expansion. No initial environmental compound has more than five total CHNOPS atoms in its composition vector, so `1.7 × 10^14` is a conservative expanded-matter bound. Both environmental accounts and organism accounts fit comfortably in `long`; tile reconciliation should use `Int128`.

For a contention group of 25,000 claims requesting 10,000 units each:

```text
total requested = 250,000,000
```

Even though this total fits in `long`, the proportional term `available × requested` may not at higher configured limits, so the allocation algorithm always uses `Int128` rather than switching types based on observed size.

**Result:** a dense hotspot does not pressure state storage, and the selected claim algorithm remains numerically safe.

# Scenario 4: representative benchmark world

Use the default-world benchmark of 100,000 organisms across `32 × 17 = 544` tiles, with 34 possible environmental resource slots per tile: twelve generic macronutrient forms, eight named gases, and fourteen micronutrients.

## Organisms

| Quantity | Total |
| --- | ---: |
| Structural units | 100,000,000 |
| Elemental matter represented by structure | 33,300,000,000 |
| Reserve units | 1,000,000,000 |
| Elemental matter represented by reserves | 4,000,000,000 |
| **Total organism matter** | **37,300,000,000** |

## Environment

If every environmental slot holds `10^12` resource units, the raw authoritative balances sum to:

```text
544 tiles × 34 slots × 10^12
    = 18,496,000,000,000,000 resource units
```

Because resource units have different composition vectors, that raw sum must not be added directly to expanded organism matter. A conservative environmental expansion bound uses five elemental quanta per environmental resource unit:

```text
5 × 18,496,000,000,000,000
    = 92,480,000,000,000,000 elemental quanta
```

Adding 37,300,000,000 expanded organism-matter quanta yields `92,480,037,300,000,000`, or approximately `9.248 × 10^16`. This conservative aggregate fits signed 64-bit, but the design still requires `Int128` because structural-biomass coefficients, source histories, and future configurations can push expanded totals higher.

## Storage estimate

A dense 28-slot resource vector for 100,000 organisms uses:

```text
28 × 100,000 × 8 bytes = 22.4 MB
```

This excludes entity metadata, alignment, remnants, claims, histories, and snapshots, but confirms that dense organism resource storage is plausible. Compartment-specific slot sets should avoid allocating three full 28-slot vectors per organism when structure and reserve each require only a few slots.

Tile environmental balances are comparatively small:

```text
34 × 544 × 8 bytes = 147,968 bytes
```

**Result:** the reference world fits the selected numeric representation and supports the proposed dense compiled layout.

# Scenario 5: maximum source accumulation

The final gameplay deadline is not yet selected, so the range proof uses an engineering horizon of `10^10` hourly ticks—about 1.14 million simulated years.

For a deliberately high net source of `10^6` quanta per tick with no opposing sink:

```text
per account:
10^10 ticks × 10^6 quanta/tick = 10^16 quanta
```

This is:

- Ten times below the configured `10^17` account ceiling.
- More than 900 times below signed 64-bit maximum.

Across 544 tiles:

```text
544 × 10^16 = 5.44 × 10^18 quanta
```

That still fits signed 64-bit narrowly. Across eight independently sourced resources:

```text
8 × 544 × 10^16 = 4.352 × 10^19 quanta
```

This does not fit signed 64-bit and validates the requirement for `Int128` world and lifetime totals.

The engine must not rely on sinks to prevent numeric overflow. Configuration compilation should prove:

```text
initialAmount + maximumNetInputPerTick × supportedRemainingTicks
    <= configuredAccountCeiling
```

If this cannot be proven because a rate is conditional, world advancement must still check each applied credit and fail the tick cleanly before overflow.

**Result:** `long` accounts remain safe over a deliberately large horizon, while global totals require `Int128`.

# Configuration guardrails

The first implementation should compile and enforce:

```text
NormalTileResourceTarget       = 1_000_000_000_000       // guidance
MaximumConfiguredAccount      = 100_000_000_000_000_000 // hard ceiling
MaximumEngineeringTicks       = 10_000_000_000          // range-proof horizon
BaselineStructuralUnits       = 1_000
BaselineReserveCapacity       = 10_000
ReserveEnergyPerUnit          = 1
```

`NormalTileResourceTarget` is guidance, not a clamp. The hard ceiling is a configuration and runtime validity rule. Numeric literals belong in the versioned rule/configuration schema rather than scattered through simulation code.

Before accepting a rule pack, validate:

- Every starting account is within its permitted range.
- Maximum one-tick source, reaction, transfer, and claim values are bounded.
- Resource coefficients and maximum amounts fit `Int128` when multiplied.
- DNA capacity modifiers cannot exceed organism account ceilings.
- A full reproduction bundle fits intermediate arithmetic.
- History bucket rates cannot overflow before rotation.
- The configured final tick does not exceed the engineering horizon without a new range proof.

# Determinism and serialization guidance

- All calculations shown here are exact integer operations.
- Division occurs only in named allocation/rate operations with specified rounding and persisted remainders.
- State hashes include resource quantities and persisted remainders, not recomputed display values.
- Protocol Buffer 64-bit quantities must become JavaScript `bigint`, strings, or exact long wrappers.
- `Int128` diagnostics must serialize as decimal strings or a defined 128-bit message, never a floating-point number.
- UI charts may down-convert scaled values to `number` after retaining the exact authoritative value.

# Tests generated by this range proof

- `StructuralBiomass` expands to exactly `[100,170,20,40,2,1]`.
- `SpentStructuralResidue` expands to exactly `[68,106,20,8,2,1]` and has zero usable energy.
- Baseline structure expands to exactly 333,000 elemental quanta.
- Full baseline reserve contains 10,000 energy and 40,000 elemental matter quanta.
- Composition-derived storage loads are `NH3 = 4`, `H2S = 3`, inorganic phosphorus `= 1`, and one micronutrient `= 1`.
- The hydrogen and sulfur one-tick assembly bundles occupy exactly `255` and `340` macronutrient-load units and fit under the founder `512` cap.
- The hydrogen and sulfur additional reproduction quota sets occupy exactly `57` and `55` micronutrient-load units and fit under the founder `64` cap.
- Under abundant uncontested micronutrients, expected extra-set acquisition takes `114` ticks for hydrogen and `110` for sulfur and is overwhelmingly likely to finish before their structural reproduction deadlines.
- The worked reproduction scenario has identical before/after CHNOPS totals and exactly 500 dissipated energy.
- The 25,000-organism and 100,000-organism fixture totals match this document.
- Composition expansion above `long.MaxValue` succeeds in `Int128` without truncation.
- Source-horizon validation accepts `10^6` per tick for `10^10` ticks from zero and rejects values exceeding the account ceiling.
- Protocol round trips preserve `long` values above JavaScript's safe-integer limit.
- Runtime credits that cross the configured account ceiling reject the entire tick mutation.

# Remaining balance decisions

The numeric architecture and reference scale are sufficient to implement the ledger and both founder fixtures. Later balance passes must still complete or validate:

- Generated-world and non-starting-tile resource distributions, especially non-gas source, sink, and exchange rates; lifecycle-owned remnant decay and mineralization now have first coefficients.
- The provisional baseline maintenance, action, reproduction, and structure-growth costs under full population fixtures.
- Advanced-trait micronutrient quotas beyond the two founder loadouts.
- Whether DNA changes the minimum structural target in v1.
- Final simulation deadline and therefore the production tick horizon.
- History bucket duration and retention.

# Scientific anchors

- Average dry bacterial biomass is reported near `CH1.7O0.4N0.2`: [Thermodynamic properties of microorganisms](https://pmc.ncbi.nlm.nih.gov/articles/PMC6587057/).
- Single-cell dry mass varies by orders of magnitude, supporting normalized rather than literal whole-world scaling: [Determination of Bacterial Cell Dry Mass](https://pmc.ncbi.nlm.nih.gov/articles/PMC106103/).
- A representative E. coli estimate uses about 0.3 pg dry mass and roughly `10^10` carbon atoms: [Fundamental limits on the rate of bacterial growth](https://pmc.ncbi.nlm.nih.gov/articles/PMC8460600/).
