# Population Ecosystem Validation

Status: first v1 mortality-balanced organic-niche, competitive-uptake, and autonomous material-opportunity calibrations fixed; executable multi-seed population validation remains

Sources: [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [organic uptake and fermentation](ORGANIC_UPTAKE_AND_FERMENTATION.md), [organism health calibration](ORGANISM_HEALTH_CALIBRATION.md), [evolution](EVOLUTION.md), and [world/climate calibration](WORLD_CLIMATE_CALIBRATION.md).

# Purpose

Test whether the first complete lifecycle and dissolved-organic rules produce the intended population-level result: primary producers remain necessary, passive decay supports a small secondary heterotroph niche, neighboring tiles receive a marginal opportunity rather than a robust food supply, and an autonomous species values the opportunity from named recent resource flows rather than from generic waste or a misleading stock snapshot.

This is an authoring calculation and fixture contract, not a replacement for the executable simulation. It uses the same hourly lifecycle curve, energy costs, reaction yields, growth and reproduction gates, and decay coefficients as the rule pack. The implementation must reproduce its landmarks with integer state, keyed reaction outcomes, actual contention, and finite populations across many seeds.

# Reference calculation

The first isolated fermenter calculation assumes:

- One-hour ticks and otherwise ideal temperature, moisture, catalyst, and nutrient conditions.
- A newborn-equivalent mature organism with `1,000` structure and `4,000` reserve.
- Primitive biomass assembly at `100` energy per structural quantum, a `3`-extent/hour assembly ceiling, a `4,000` reserve-protection floor, and `PrimitiveFission` at `2,000` structure and `8,500` pre-work reserve.
- The `500` reproduction-work cost and near-even split, so one replacement body plus offspring provisioning requires `104,500` net energy above recurring operation: `100,000` for structure and `4,500` to move from the protected reserve floor to the reproduction gate.
- The exact age-throughput and senescence curves already fixed in the lifecycle plan.
- Expected fermentation successes for the authoring calculation. Runtime still uses the keyed binomial and whole extents.
- No external primary metabolism, environmental stress, movement, direct scavenging, or substrate contention.

The no-other-risk senescence curve has an expected lifetime of approximately `1,399.50 h`, or `58.31 days`. For an organism with recurring cost `C`, ideal fermentation opportunity ceiling `X`, success factor `0.90`, and two stored energy per successful extent, the pre-senescence energy rate is:

```text
expectedSuccessfulExtentsPerHour = 0.90 * X
expectedReserveOutputPerHour     = 2 * expectedSuccessfulExtentsPerHour
youngNetReservePerHour           = expectedReserveOutputPerHour - C
```

# Throughput calibration

The former `64`-opportunity ceiling failed the lifecycle test. Age throughput begins to decline before enough structure and offspring reserve can be rebuilt, making its nominal `83-day` energy-only estimate optimistic. Its actual no-death first division occurs near day `136`, by which point survival under the senescence curve is effectively zero.

The authoring sweep is:

| Opportunity ceiling/hour | Expected successes/hour | Reserve output/hour | First division | Survival to first division | Expected direct offspring over one lifetime |
| ---: | ---: | ---: | ---: | ---: | ---: |
| `64` | `57.6` | `115.2` | `136.21 d` | approximately `4.2e-11` | approximately `0.000` |
| `80` | `72.0` | `144.0` | `59.46 d` | `0.455` | `0.455` |
| `96` | `86.4` | `172.8` | `40.58 d` | `0.920` | `0.920` |
| `104` | `93.6` | `187.2` | `35.67 d` | `0.973` | `0.995` |
| **`120`** | **`108.0`** | **`216.0`** | **`28.88 d`** | **`1.000`** | **`1.381`** |
| `128` | `115.2` | `230.4` | `26.33 d` | `1.000` | `1.603` |

`Expected direct offspring` sums survival to each deterministic division age of one continuing organism. It is a useful replacement diagnostic, not a claim that a finite stochastic population follows a simple branching process exactly.

V1 therefore selects `120` planned opportunities/hour with the existing `0.90` success factor. The hydrogen-derived organic profile pays `65` energy/hour while active, has an ideal young surplus of `151` energy/hour, and reaches its first split at hour `693`, just before senescence begins at hour `720`. Applying the survival-weighted division schedule gives an idealized doubling time near `79.7 days`: the offline authoring tool solves `sum(survivalAtDivision[i] * exp(-r * divisionHour[i])) = 1`, then uses `ln(2) / r`. Runtime simulation does not evaluate this floating-point diagnostic. This is roughly twice the first-division time and many times the opening growth rate of either founder in its preferred volcanic niche.

The fully active sulfur bridge pays at least `80` energy/hour before inherited-pathway upkeep and stress. At the selected ceiling it first divides near hour `769` (`32.04 days`), has approximately `0.993` survival to that division, and produces approximately `1.118` expected direct offspring over one lifetime. It is viable in ideal conditions but has much less margin, preserving the intended cost of its longer route away from volcanism. Regulation must suppress an unavailable founding pathway where permitted; additional stress or inherited upkeep can still make the branch nonviable.

The survival-weighted replacement boundary occurs near `94.01` successful substrate grants/hour for the `65`-cost hydrogen profile and `102.09` for the `80`-cost sulfur profile. The selected ideal grant of `108/hour` leaves about `14.9%` and `5.8%` headroom respectively. A population below those realized per-organism rates may pay maintenance for a while yet still fail to replace senescent members.

# Mortality-fed carrying scale

For the first steady-turnover fixture, hold a primary-producer population at a fixed size by replacing each age-only death, let each death contribute `1,000 StructuralBiomass`, and disable direct scavenging and exchange. This isolates the passive-decay niche. At the `58.31-day` expected lifetime:

```text
producerDeathsPerHour = producerPopulation / 1,399.503
steadyStructuralReleasePerHour = 1,000 * producerDeathsPerHour
steadyLabileProductionPerHour  = 4 * steadyStructuralReleasePerHour
```

The structural-remnant pool approaches this production rate over its 30-day half-life. It reaches `50%`, `75%`, and `87.5%` of the eventual labile-production rate after approximately 30, 60, and 90 days. The organic niche therefore emerges gradually rather than appearing at full strength with the first death.

| Producer turnover | Eventual labile production | No-consumer labile plateau | Hydrogen replacement equivalents | Sulfur replacement equivalents |
| --- | ---: | ---: | ---: | ---: |
| One mature corpse/day | `166.667/h` | `80,867` | `1.77` | `1.63` |
| `100` stable producers | `285.816/h` | `138,678` | `3.04` | `2.80` |
| `300` stable producers | `857.447/h` | `416,035` | `9.12` | `8.40` |

The no-consumer plateau divides production by the `2,061`-per-million hourly labile-loss coefficient. Replacement equivalents divide production by the survival-weighted critical grants above and deliberately ignore self-recycling, which is small and delayed. They are not maintenance equivalents: the maintenance-only threshold remains `32.5` substrate/hour for the hydrogen profile, but merely paying maintenance does not replace a body lost to senescence.

This produces the intended hierarchy. Roughly 100 stable primitive producers sustain only about three hydrogen-derived fermenters through passive dissolved recovery, before scavenging, export, stress, or contention. Three hundred sustain roughly nine. The heterotroph niche is useful but subordinate to primary production.

The result also explains why a large new branch can overshoot its niche. On the 100-producer tile, a 50-organism one-tile founding event has only about one day of no-consumer labile stock at its replacement demand and renewal sufficient for about three organisms. Proportional contention may deplete all founders together rather than smoothly selecting exactly three survivors. Proposal previews and autonomous scoring must expose this risk; speciation does not receive a hidden resource reservation or guaranteed carrying-capacity adjustment.

## Exploratory finite-cohort probe

A 32-seed, 180-day authoring probe applied independent exact `Binomial(ageLimitedOpportunities, 0.90)` reaction outcomes, the primitive senescence draws, organism-level reserve/structure/growth/fission state, and ordinary proportional substrate contention. Its constrained cases began with the no-consumer `138,678` stock and received the constant 100-producer steady-state source of `285.816/hour`. It deliberately omitted exchange, self-recycling, direct scavenging, stress, micronutrient failure, and structural-decay warm-up so it would isolate fermentation throughput and cohort oversubscription.

| Substrate case | Initial fermenters | Population at day 180, median | Seed range | Extinct seeds |
| --- | ---: | ---: | ---: | ---: |
| Unlimited | `10` | `62` | `50..94` | `0 / 32` |
| 100-producer source | `2` | `3.5` | `1..5` | `0 / 32` |
| 100-producer source | `3` | `3` | `0..6` | `1 / 32` |
| 100-producer source | `4` | `2` | `0..4` | `11 / 32` |
| 100-producer source | `50` | `1` | `0..5` | `16 / 32` |

The unlimited case confirms slow positive population growth. The constrained cases cluster around the analytical three-organism scale and demonstrate that starting above carrying capacity increases extinction risk rather than guaranteeing graceful contraction. These are diagnostic landmarks, not normative golden outputs: the executable fixture must add the omitted ledger processes and use the server's keyed RNG, integer rounding, stable remainder allocation, and exact phase order.

# Spatial ecosystem checks

The calibrated dissolved field remains compatible with the larger organism ceiling:

- A one-corpse/day source has total renewal for about `1.77` hydrogen replacement equivalents across the entire field.
- The source tile can supply the selected fermenter ceiling of `108` successful grants/hour to one ideal organism; the previously calculated `165.56/hour` value is the field's uncapped hydraulic drain, not one organism's request ceiling.
- An axial first-ring tile can continuously receive about `39.03/hour`. This exceeds the `32.5/hour` hydrogen maintenance threshold but is far below the `94.01/hour` replacement boundary.
- Every second-ring location remains below even maintenance. A first-ring organism may linger, scavenge a transient pulse, or combine the flow with another metabolism, but dissolved leakage alone cannot support a robust independent lineage.

A closed fermenter-only fixture must eventually decline. One fully decayed `1,000`-structure body yields at most `8,000` reserve through fermentation after costing `100,000` reserve to build, before maintenance. Self-recycling can slow a collapse but cannot make a producer-free energy cycle.

# Dissolved-organic specialization landmarks

`DissolvedOrganicSpecialization` does not increase LDO production or fermentation yield. Its `2×` ordinary contention weight redistributes a scarce pool toward the specialist, while its 12-unit active upkeep raises the conservative replacement demand:

| Profile | Active cost/hour | Conservative LDO replacement demand/hour | 100-producer field equivalents |
| --- | ---: | ---: | ---: |
| Base hydrogen fermenter | `65` | `105.069` | `2.720` by conservative demand |
| Specialized hydrogen fermenter | `77` | `111.069` | `2.573` |
| Base sulfur fermenter | `80` | `112.569` | `2.539` |
| Specialized sulfur fermenter | `92` | `118.569` | `2.411` |

The base-profile equivalents here use the same conservative 720-hour formula as autonomous scoring and therefore differ from the survival-weighted `3.04/2.80` carrying landmarks above. When every consumer has the same weight, the specialist field supports fewer organisms. In a mixed population, the specialist may invade by taking a larger share: one base and one specialist each requesting 120 against 120 available receive 40 and 80 before whole-unit remainder handling.

The other payoff is compatible throughput. A proto-eukaryotic direct respirer with abundant LDO and O2 can use 160 opportunities instead of being capped by base uptake at 120. Its expected 152 successes add 456 carrier units/hour and permit four rather than three whole assembly extents/hour. This is a niche synergy with advanced respiration, not a universal fermentation upgrade.

# Autonomous material-opportunity scoring

Pressure answers why a species needs change; material opportunity answers whether a proposed resource-dependent capability has something concrete to act on. They remain separate. Mutation price never changes with the environment, and opportunity is advisory rather than a genetic validity gate.

## Authored opportunity profile

A trait or complete prerequisite-closed proposal that enables a material-dependent process declares:

```text
MaterialOpportunityProfile:
    required_resource_ids[]
    activation_quota_requirements[]       # optional, exact micronutrients
    reproduction_quota_rule               # optional, compiled complete set
    quota_acquisition_horizon_rule         # optional; 720 h for first oxygenic profile
    output_energy_per_whole_extent
    ideal_success_q
    maximum_opportunities_per_hour
    replacement_capital_energy_rule
    replacement_horizon_rule
    history_window_hours                 # 168 for v1
```

The profile attaches only when the complete proposal can perform useful work. `OrganicResourceUptake` alone does not claim an energetic organic opportunity; the proposal containing an enabled fermentation reaction does. Generic organic elemental pools, `SpentStructuralResidue`, and `ReducedFermentationProducts` never satisfy the labile-substrate profile.

For each current occupied tile, derive the proposed DNA's current recurring non-growth energy obligation from authoritative recent organism costs plus the proposal's compiled active/suppressed cost changes. The first conservative replacement estimate is:

```text
replacementHorizonHours = compiled senescence-onset age
replacementCapitalEnergy = energy to build one replacement mature body
                         + energy to provision the selected reproduction profile

replacementDemandPerFounderHour =
    (recurringEnergyPerHour
     + replacementCapitalEnergy / replacementHorizonHours)
    / storedEnergyPerGrantedSubstrate

expectedMaximumGrantPerHour =
    maximumOpportunitiesPerHour * idealSuccess

processCoverage = clamp01(
    expectedMaximumGrantPerHour / replacementDemandPerFounderHour)
```

For primitive hydrogen fermentation this conservative demand is `(65 + 104,500 / 720) / 2 = 105.069` labile substrate/hour. For the base sulfur bridge it is `112.569/hour`. The calculation intentionally targets replacement by senescence onset, so it is more conservative than the survival-weighted population boundary and slightly penalizes the viable-but-narrow sulfur bridge.

## Stock and renewal

Use the current named-resource stock and authoritative trailing 168-hour tile flows:

```text
potentialRenewalPerHour = max(
    0,
    meanNamedProduction
      + meanInboundExchange
      - meanOutboundExchange
      - meanCompetingBiologicalUptake)
```

Do not subtract passive loss from potential renewal. In a no-consumer steady state, passive loss is the source flux a new consumer can displace as stock falls. Outbound exchange and biological uptake expected to remain outside the proposed founders remain unavailable to the proposed cohort, making the estimate conservative. If selected founders already consume the same resource, estimate and exclude their current share from `meanCompetingBiologicalUptake` so their existing demand is neither lost nor subtracted twice. Source, exchange, and uptake terms must all refer to the exact required resource identity.

For each allowed selected-tile count `k`, use the real v1 founder fraction for that count. On each candidate tile:

```text
founders = max(1, floor(localPopulation * fractionBySelectedTileCount[k]))
aggregateDemand = founders * replacementDemandPerFounderHour

stockBridgeQ = clamp01(
    currentAccessibleStock / (aggregateDemand * 168 hours))

renewalCoverageQ = clamp01(
    potentialRenewalPerHour / aggregateDemand)

localMaterialOpportunityQ = processCoverage
                          * (0.35 * stockBridgeQ
                             + 0.65 * renewalCoverageQ)
```

Select the best `k` eligible tiles by `localMaterialOpportunityQ`, with stable tile-ID tie-breaking, remove ancestor-depleting combinations, and take the remaining tiles' founder-count-weighted mean as `planMaterialOpportunityQ[k]`. The candidate's final `materialOpportunityQ` is the weighted mean of these plan scores under the opportunity-adjusted tile-count distribution below, not the most favorable plan. This is a current-state estimate only: it does not run the future simulation, probe alternate outcomes, count hidden client information, or guarantee survival.

On four otherwise identical tiles containing 100 producers and 100 candidate ancestors each, the hydrogen profile produces these illustrative current-state scores after the decay fields reach steady state:

| Selected tiles | Fraction/tile | Founders/tile | Plan material opportunity | Adjusted selection share |
| ---: | ---: | ---: | ---: | ---: |
| `1` | `50%` | `50` | `0.090` | `21.5%` |
| `2` | `20%` | `20` | `0.226` | `20.6%` |
| `3` | `8%` | `8` | `0.565` | `34.3%` |
| `4` | `3%` | `3` | `0.940` | `23.7%` |

The rounded shares result from the adjusted weights defined below; this candidate's combined `materialOpportunityQ` is approximately `0.482`. This is the intended interaction between niche size and the existing speciation fractions. A four-tile event can seed about the renewal-supported population in each tile; a one-tile event greatly overshoots it. The autonomous policy becomes more willing to spread the branch but retains meaningful variation.

## Candidate and tile-count weights

Resource-independent proposals use `materialOpportunityQ = 1.0`. Resource-dependent proposals receive:

```text
materialOpportunityMultiplierQ = 0.10 + 0.90 * materialOpportunityQ
```

Include this multiplier in both ordinary and exploratory candidate weights. The `0.10` floor preserves rare experimentation without letting a starvation-pressure match turn absent or generic waste into strong evidence for fermentation.

When committing an intent with a material-opportunity profile, adjust the existing autonomous tile-count prior before sampling:

```text
adjustedTileCountWeight[k] = baseTileCountWeight[k]
                           * max(0.10, planMaterialOpportunityQ[k])^2

materialOpportunityQ =
    sum(adjustedTileCountWeight[k] * planMaterialOpportunityQ[k])
    / sum(adjustedTileCountWeight[k])
```

Invalid `k` values have zero weight. If all plans score `1.0`, the existing `80/15/4/1` prior is unchanged. When a broad low-fraction founding plan is materially safer, the squared factor can overcome that prior without deterministically forcing it.

The ordinary local tile weight also gains:

```text
localOpportunityMultiplier = 0.10 + 0.90 * localMaterialOpportunityQ
```

The multiplier composes with population, health, pressure match, and non-material activation. Final selected founders and all account invariants are still revalidated through the ordinary speciation transaction.

The flow window is the 168 simulated hours ending at the latest completed tick. Before world hour 168, missing prehistory is deterministically zero-filled rather than extrapolating from a short initial burst. Only exact resources referenced by selectable `MaterialOpportunityProfile`s require the scoring ring: v1 stores one-hour source, passive-loss, inbound, outbound, and biological-uptake buckets for those resources, plus checked rolling totals. The ring position, buckets, and totals persist in saves so future eviction and scoring remain replay-exact.

## One-time catalytic provisioning

A proposal that adds committed catalytic quotas also estimates whether its founders can activate the machinery and provision one replacement; catalysts are not falsely treated as reaction fuel. For a candidate tile and founder count, use the exact ancestor population mean free/committed inventory with fixed-point arithmetic. For each required micronutrient `r`:

```text
grossQuotaNeed[r] = founders * (
    max(0, proposedCommittedQuota[r] - meanCommittedQuota[r])
    + proposedCommittedQuota[r])

outstandingQuotaNeed[r] = max(
    0,
    grossQuotaNeed[r] - founders * meanFreeQuota[r])

environmentalQuotaSupply[r] = currentAccessibleStock[r]
                            + potentialRenewalPerHour[r]
                              * quotaAcquisitionHorizonHours

resourceQuotaCoverageQ[r] = 1 when outstandingQuotaNeed[r] is zero,
    otherwise clamp01(environmentalQuotaSupply[r]
                      / outstandingQuotaNeed[r])

sharedUptakeCoverageQ = 1 when sum(outstandingQuotaNeed) is zero,
    otherwise clamp01(
        founders * compiledMicronutrientUptakePerHour
                 * quotaAcquisitionHorizonHours
        / sum(outstandingQuotaNeed))

quotaProvisionQ = min(sharedUptakeCoverageQ,
                      min(resourceQuotaCoverageQ[r]))
```

The first term inside `grossQuotaNeed` commissions newly required machinery; the second provisions one complete descendant set. Existing free inventory may satisfy either need only once. `potentialRenewalPerHour` uses the same exact-resource flow definition as recurring substrates. For a random-subset founding event, the population mean is the deterministic pre-draw expectation; the committed event records the actual founders and may begin with a different realized activation fraction.

The local score for a reaction that needs both recurring material and catalytic provisioning is:

```text
localCoupledOpportunityQ = localMaterialOpportunityQ * quotaProvisionQ
```

Traits with no new quota use `quotaProvisionQ = 1`. The score remains advisory: a low score does not invalidate genetically valid DNA, while a zero-score proposal retains only the ordinary exploratory floor. Exact quota resources referenced here receive the same persisted 168-hour resource-flow histories as recurring substrates.

## Persistence and explanation

An autonomous intent stores its initial material-opportunity fit and the per-plan breakdown. Apply the existing reconsideration rule to opportunity as well as pressure: if current material opportunity falls below half its initial value while another valid candidate scores at least twice as highly, the goal may be replaced. A transient daily dip alone does not clear an intent.

The authoritative explanation records, for every material-dependent candidate considered:

- Exact resource IDs and the 168-hour history boundary.
- Current stocks and named production, inbound, outbound, and competing-uptake rates.
- Proposed recurring cost, replacement capital, horizon, per-founder demand, and process coverage.
- Per-tile founder count, stock bridge, renewal coverage, and local opportunity.
- Per-`k` plan score, adjusted tile-count weight, final material multiplier, and selected plan/draw.

The client filters tile detail through ordinary knowledge rules, but hidden information never changes the server decision.

# Required executable fixtures

- The primitive senescence curve reproduces the `1,399.50 h` expected lifetime within fixed-point tolerance.
- The `64`-ceiling counterfactual remains below one expected direct offspring; the selected `120` ceiling produces first hydrogen division at `690..696 h` in the expected-value authoring model.
- Across keyed finite-population seeds with abundant substrate, the hydrogen-derived fermenter persists and grows slowly; the sulfur bridge remains viable but grows more slowly before inherited-pathway and stress costs.
- A 100-producer steady-turnover fixture converges toward `285.816` labile production/hour after the structural-decay warm-up and supports approximately three hydrogen or two-to-three sulfur fermenters, not dozens.
- The targeted finite-cohort fixture remains qualitatively consistent with the authoring probe: two-to-three founders usually persist, four are marginal, and a 50-founder overshoot has a materially greater extinction rate. Exact executable acceptance bands should be pinned only after adding self-recycling and server-exact integer execution.
- Disabling primary production makes every fermenter-only ecosystem eventually lose usable energy and go extinct, apart from a bounded run that ends before its initial stock is exhausted.
- A one-corpse/day first-ring consumer can cover maintenance transiently but cannot replace itself from dissolved import alone.
- Mixed base/specialist contention reproduces the weighted 40/80 landmark, while all-specialist fixtures retain ordinary proportional fairness and lower total carrying capacity through the added upkeep.
- A specialization-only fermentation fixture retains the base `108` expected successes/hour; a supplied specialized proto-eukaryotic direct respirer reaches `152` and `456` new carrier matter/hour without changing reaction stoichiometry.
- Generic organic abundance with zero `LabileDissolvedOrganic` produces `materialOpportunityQ = 0`; adding the named stock and flow raises the score monotonically.
- Candidate scores are invariant under candidate, tile, organism, ledger-bucket, and worker order; save/load preserves the same score breakdown and draws.
- Scoring all valid tile counts reproduces the four illustrative plan values within declared fixed-point rounding tolerance.
- Changing client visibility or subscriptions cannot alter any stock, flow, pressure, opportunity, candidate, or tile-selection result.
- The isolated reference oxygenic producer reproduces the daily light curve, `7,772` CO2/reserve/O2 extents, `90/hour` upkeep, and `446.9 h` expected capital landmark.
- The paired oxygen fixture spans producer-only, basal-anaerobe, direct-respirer, and reduced-product-respirer populations; it verifies source/sink conservation, the revised tolerance bands, and whether realized oxygen and organic-resource carrying capacities match the authoring ratios.

# Recalibration triggers

Rerun the complete population and autonomy suite after changing fermentation yield, success, throughput, upkeep, regulation suppression, biomass-assembly cost, reserve floor/capacity, reproduction allocation or cost, senescence, structural or labile decay, dissolved exchange, speciation fractions, autonomous priors, or resource-history windows.

Direct scavenging, particulate digestion, predation, complex cells, and terrestrial adaptation require additional coupled population fixtures. Aerobic respiration and oxygenic photosynthesis now have paired reaction, cost, quota, light, carrier, gas-source/sink, and replacement-demand landmarks in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md) and [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md), but still need the coupled producer/anaerobe/respirer executable fixture. Every finite-material trait uses the same opportunity-profile interface.
