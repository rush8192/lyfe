# V1 Gameplay Flows

Status: scaffold

Sources: [GAMEPLAY vision](../../vision/GAMEPLAY.md), [INTERFACE vision](../../vision/INTERFACE.md), and [VISION](../../vision/VISION.md).

# Purpose

Turn free sandbox and survival into explicit setup, play, loss, completion, and command flows. Competitive multiplayer remains future scope.

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
10. Atomically create one sandbox founder or both survival founders and begin the simulation.

Define cancellation, regeneration, invalid setup, and deterministic setup-command recording.

The exact opening tradeoff, provisional mutation paths, and paired-region invariants are defined in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).

# Common play controls

- Pause and resume.
- Select a simulation speed.
- Inspect tiles, organisms, species, resources, and lineage.
- Follow a random controlled-species organism without consuming simulation randomness.
- Propose and commit mutation/speciation.
- Save and load worlds.

There is no direct control of organism movement or actions.

# Free sandbox

- Control may move to any living species.
- Any species may be locked or unlocked against autonomous mutation/speciation.
- The run ends early only if all life becomes extinct.
- At the final date, the surviving tree of life and world state are summarized.

Define command authority, lock behavior during a pending autonomous decision, and what is selected when the currently inspected species becomes extinct.

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

Exploration applies to free sandbox and survival. Player knowledge persists as authoritative actor state, while live visibility is derived from the currently controlled species at a completed tick boundary.

| State | Entry rule | Information available |
| --- | --- | --- |
| Unknown | Never discovered | No tile composition or current state |
| Reduced | Edge-sharing neighbor of a live tile, initial neighbor, or previously live tile | Fixed geography and baselines, coarse composition, timestamped last-known observations; no organisms or exact changing values |
| Live | At least one organism of the controlled species occupies the tile | Organisms and remains, exact current conditions, exact stocks and flows, and observed history |

At initialization, the player's starting tile is live and every edge-sharing neighbor is reduced. At the end of each tick, occupation promotes tiles to live and discovers their neighbors; vacated tiles demote to reduced. Player knowledge is monotonic except that exact live state becomes stale on demotion.

In free sandbox, changing control retains all earlier discoveries but recomputes live tiles from the newly controlled species. The control command may therefore reveal every tile that species occupies and their neighbors. In survival, uncontrolled ancestors and the competing lineage do not provide live visibility.

Exact resource charts contain only intervals observed while live. Reduced views carry `observedAtTick` and must show gaps rather than interpolate authoritative hidden history. Exploration changes neither simulation outcomes nor random streams.

# Pacing and deadline

The fastest speed should provide a thriving species with a meaningful mutation decision every few real-world minutes. The detailed plan must connect:

- Default final date and simulated run length.
- Speed presets and tick throughput.
- Mutation income and trait prices.
- Expected lifecycle and seasonal pacing.
- Notification frequency and pause-on-decision options, if any.

# End-of-run presentation

Define a result record independent of the narrative skin. It should capture mode, outcome, final tick/date, controlled-species status, surviving species, lineage size, notable environmental and biological milestones, and replay/save references. Asteroid and alien endings are presentation variants over this result.

# Required artifacts

- Setup state machine and command sequence.
- Sandbox and survival state diagrams.
- Complete v1 command catalogue with authority rules.
- Loss and completion precedence table.
- Default final date and speed-preset proposal.
- Abiogenesis and epilogue data requirements.
- Acceptance scenarios for one complete sandbox and survival run.
- Visibility-state transition table and acceptance scenarios for initialization, migration, vacancy, and sandbox control transfer.
