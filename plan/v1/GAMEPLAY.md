# V1 Gameplay Flows

Status: v1 setup, control, visibility, first Survival opening, and player-loop contract specified; deadline, complete command catalogue, opening-choice calibration, and sandbox acceptance scenario remain

Sources: [GAMEPLAY vision](../../vision/GAMEPLAY.md), [INTERFACE vision](../../vision/INTERFACE.md), and [VISION](../../vision/VISION.md).

# Purpose

Turn free sandbox and survival into explicit setup, play, loss, completion, and command flows. Competitive multiplayer remains future scope.

The nested observe-diagnose-evolve-review loop, attention tools, evolution-goal semantics, consequence review, factual chronicle, and opening-choice quality gate are defined in [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md).

# Challenge arc and balance charter

The v1 opening must express the vision's player-versus-environment premise through ordinary simulation rules:

- Each founder starts in a habitat that can support it long enough to reproduce and reach a meaningful evolution decision, but unbounded unattended growth must not remain indefinitely sustainable.
- Resource throughput, environmental variation, chemical exposure, and basal costs provide the first pressures. A healthy population can temporarily prosper; increasing demand, local depletion, waste, dispersal, or changing conditions should then expose a limiting factor.
- Pressure is density-dependent and causal, not a hidden population cap or difficulty scalar. The same tile ledgers, organism costs, probabilities, and environmental rules apply to controlled and uncontrolled species.
- The paired autonomous founder is an honest ecological peer and comparison point. It receives no scripted attacks or invisible advantage, and its presence must not turn the opening into a conventional head-to-head contest before the simulated populations actually overlap or affect shared flows.
- Founders have no predation capability. Predation becomes available only through the documented evolutionary path and matters only when prey density, proximity, capture, defense, and processing rules make it viable. Its first appearance is a later ecological milestone.
- Failure must be possible, but warning and causal evidence must ordinarily precede an avoidable collapse. The baseline opening should not kill an otherwise viable founder before it can reach its first meaningful decision solely to establish difficulty.

Opening validation therefore has two complementary obligations: prove that both founders can establish a foothold, and prove that the wider generated-world scenario produces comprehensible pressure that eventually demands adaptation. A one-tile viability fixture is not evidence that the complete game is sufficiently challenging. Conversely, a high extinction rate is not evidence of good difficulty if players cannot diagnose or answer its causes.

# World setup

Plan the state machine for:

1. Choose free sandbox or survival.
2. Enter or generate a world seed.
3. Generate and preview the world.
4. Choose sulfide anoxygenic phototrophy or hydrogen acetogenesis.
5. Choose a small set of efficiency-versus-tolerance tradeoffs.
6. Select an eligible volcanic ocean tile.
7. For survival, resolve and preview that an autonomous founder using the other metabolism will occupy its reserved edge-sharing eligible tile; do not expose its hidden post-start state.
8. Confirm final date and any permitted world options.
9. Present the abiogenesis introduction.
10. Finalize the selected world's local-dawn offset and moisture prehistory, atomically create one sandbox founder or both survival founders, and begin the simulation at tick zero.

Define cancellation, regeneration, invalid setup, and deterministic setup-command recording.

The exact opening tradeoff, provisional mutation paths, and paired-region invariants are defined in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).

The canonical paired opening, first mutation decision, seven-day post-speciation viability check, and provisional pacing bands are defined and executable in [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md). It is the normative first Survival acceptance scenario; the isolated founder documents remain reaction-level controls.

# Common play controls

- Pause and resume.
- Select a simulation speed.
- Inspect tiles, organisms, species, resources, and lineage.
- Follow a random controlled-species organism without consuming simulation randomness.
- Pin authorized subjects, compare observations, set an evolution goal, and configure single-player attention/auto-pause policy.
- Propose and commit mutation/speciation.
- Save and load worlds.

There is no direct control of organism movement or actions.

# Free sandbox

- Control may move to any living species.
- Any species may be locked or unlocked against autonomous mutation/speciation. Locking does not stop mutation income or pressure accumulation, but clears any pending autonomous intent; unlocking waits for the next ordinary autonomous evaluation.
- The run ends early only if all life becomes extinct.
- At the final date, the surviving tree of life and world state are summarized.

Define what is selected when the currently inspected species becomes extinct. Command authority, lock behavior, and the controlled/uncontrolled speciation transition follow [EVOLUTION.md](EVOLUTION.md).

# Survival

- The player controls the founding species and follows the selected descendant after speciation.
- An autonomous competitor begins with the other founding metabolism in the paired starting tile and uses ordinary uncontrolled-species rules without bonuses.
- Ancestors and other branches become autonomous.
- The run is lost immediately when the controlled species becomes extinct.
- Competitor extinction does not end the run.
- V1 has no lineage recovery or backtracking.
- The run succeeds when the controlled species survives to the final date.

Define the exact transition when extinction and final-date completion occur on the same tick.

# Exploration and observation

Exploration applies to free sandbox and survival. Player knowledge persists as authoritative actor state, while the knowledge owner materializes live visibility once from the currently controlled species at a completed tick boundary.

| State | Entry rule | Information available |
| --- | --- | --- |
| Unknown | Never discovered | No tile composition or current state |
| Reduced | Edge-sharing neighbor of a live tile, initial neighbor, or previously live tile | Fixed geography and baselines, coarse composition, timestamped last-known observations; no organisms or exact changing values |
| Live | At least one organism of the controlled species occupies the tile | Organisms and remains, exact current conditions, exact stocks and flows, and observed history |

At initialization, the player's starting tile is live and every edge-sharing neighbor is reduced. At the end of each tick, occupation promotes tiles to live and discovers their neighbors; vacated tiles demote to reduced. Player knowledge is monotonic except that exact live state becomes stale on demotion.

Occupation is arrival-mechanism agnostic. Active migration, DNA-scaled Brownian-like population spread, and future directionally environment-carried dispersal all reveal a newly occupied tile through the same rule; none grants advance knowledge along a projected route. Migration history should retain whether displacement was active, passive stochastic, or directional environmental so the client can explain how the lineage spread without implying direct player control. Evolution previews for anchoring and drifting show their passive RMS, active-speed, upkeep, and estimated edge-encounter changes rather than presenting either as a generic movement bonus.

In free sandbox, changing control retains all earlier discoveries but causes the knowledge owner to update the stored live-tile set from the newly controlled species. The control command may therefore reveal every tile that species occupies and their neighbors. In survival, uncontrolled ancestors and the competing lineage do not provide live visibility.

Exact resource charts contain only intervals observed while live. Reduced views carry `observedAtTick` and must show gaps rather than interpolate authoritative hidden history. Exploration changes neither simulation outcomes nor keyed random addresses/results.

# Pacing and deadline

The fastest speed should provide a thriving species with a meaningful mutation decision every few real-world minutes. The detailed plan must connect:

- Default final date and simulated run length.
- Speed presets and tick throughput.
- Mutation income and trait prices.
- Expected lifecycle and seasonal pacing.
- Notification frequency and pause-on-decision options, if any.

“Meaningful” is stricter than affordable: the first decision menu for each founder must include at least two non-dominated, different-intent proposals, including one that changes an active capability or parameter in the validated opening. Preparatory investments remain valid when clearly labeled, but cannot alone satisfy the cadence target.

# End-of-run presentation

Define a result record independent of the narrative skin. It should capture mode, outcome, final tick/date, controlled-species status, surviving species, lineage size, canonical notable-event references, important branch comparisons, terminal causal evidence, and replay/save references. Asteroid and alien endings are presentation variants over this result. Chronicle prose is derived presentation; it must not invent causes or alter the factual record.

# Required artifacts

- Setup state machine and command sequence.
- Sandbox and survival state diagrams.
- Complete v1 command catalogue with authority rules.
- Loss and completion precedence table.
- Default final date and speed-preset proposal.
- Abiogenesis and epilogue data requirements.
- Player-loop, proposal-framing, attention, consequence-review, chronicle, and postmortem contract; first pass in [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md), with wireframes and threshold calibration pending.
- Acceptance scenario for one complete sandbox run; the first Survival opening scenario is defined in [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md), while a final-date Survival run remains pending.
- Visibility-state transition table and acceptance scenarios for initialization, migration, vacancy, and sandbox control transfer.
