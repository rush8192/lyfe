# UI-200 Primary-Surface Wireframes

Status: first responsive information-architecture and interaction wireframes

Sources: [Browser client](CLIENT.md), [player loop and narrative](PLAYER_LOOP_AND_NARRATIVE.md),
[interface vision](../../vision/INTERFACE.md), [gameplay flows](GAMEPLAY.md), and
[state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md).

# Purpose

These wireframes define the hierarchy, navigation, evidence scope, and responsive collapse of the
v1 playable client. They are layout contracts rather than final art direction or polished copy.
They preserve the executable thin-client and knowledge-boundary rules: a more prominent panel never
receives facts the actor is not authorized to know, and presentation state never becomes a
simulation input.

`Executable` means a first working version exists. `Partial` means the surface exists but the
wireframe includes named follow-up work. `Planned` means the layout reserves a deliberate home for
the feature without implying that it is currently playable.

# Application shell and navigation

Desktop uses one persistent run header and five view destinations:

```text
┌ LYFE ─ world identity ─ completed hour ─ sync state ─ Pause/Resume · Speed · Save ┐
│ World        Resources        Evolution        Lineage        Chronicle            │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

- `World` owns observation, map camera, tile/organism selection, journey, activity filters, and the
  attention inbox.
- `Resources` keeps the same selected tile and organism and expands diagnostic history and gates.
- `Evolution` owns saved goals, the decision frontier, proposal construction/comparison, founding
  tiles, irreversible preview, and commit.
- `Lineage` owns the tree of life and navigation to authorized living or extinct species evidence.
- `Chronicle` owns factual landmarks, evidence references, and private hypothesis notes.
- Selection, camera, filters, and the active diagnostic context are browser-local and survive view
  changes. The authoritative world boundary does not.
- A recovery banner sits immediately below the run header in every view. It labels the held
  completed boundary and locks authoritative actions without covering retained evidence.
- Keyboard order follows header controls, primary views, local toolbar, primary content, contextual
  inspector, and secondary history. Skip navigation moves directly to the active view.

At widths below `760 CSS px`, header controls wrap, the view destinations become a horizontally
wrapping two-row strip, side rails stack after their primary content, and comparison tables receive
one contained horizontal scroller. The map stays at least `320 CSS px` wide; no diagnostic fact is
hidden behind hover.

# Surface registry

| Surface | Wireframe home | First-slice state | Principal follow-up |
| --- | --- | --- | --- |
| World creation, profile, founding species, start preview | Setup | Executable | premise/dependency polish and broader options |
| Abiogenesis introduction | Setup → World transition | Planned | short dismissible storybook transition |
| World map, layers, tile/organism/remnant view | World | Partial | lenses, x-wrap, follow, density aggregation |
| Tile, organism, and species inspection | World | Partial | full organism/species facts and comparison |
| Resource stock and flow history | Resources | Executable first slice | longer compacted/seasonal windows |
| Tree of life and evolution planner | Evolution + Lineage | Partial | lineage tree and decision-frontier expansion |
| Inbox, watchlist, goal, consequence review | World + Evolution + Review | Partial | watchlist controls and cold retention |
| Chronicle, evidence navigation, private notes | Chronicle | Executable first slice | broader evidence families and cross-device notes |
| Journey and activity filters | World | Executable first slice | aggregation and cold historical segments |
| Clock, deadline, save, run status, identity | Global shell | Partial | deadline, richer save management, durable mailbox |
| End-of-run summary | Ending | Planned | loss postmortem and replay/navigation actions |

# 1. Setup and abiogenesis

```text
┌ Before the first hour                         official profile · rules identity ┐
│ KINDLE A WORLD                                                               │
│ Choose the stakes          Choose the first metabolism                       │
│ [ Free sandbox ]           [ Hydrogen acetogenesis ] [ Sulfide phototrophy ] │
│ [ Survival ]               dependencies · tolerance · reproduction window    │
│                                                                              │
│ Opening emphasis           World seed                    candidate region     │
│ [ balanced / ... ]         [ 32 hexadecimal digits ]    [ 1 ] [ 2 ] [ 3 ]   │
│                                                                              │
│ 100 founders · paired autonomous root disclosed in Survival      [ Begin ]   │
└──────────────────────────────────────────────────────────────────────────────┘
                         ↓ authoritative creation
┌ A moment in the dark ocean ─ factual seed/profile caption ─ [ Enter world ] ┐
└──────────────────────────────────────────────────────────────────────────────┘
```

The candidate-region preview shows only setup-authorized conditions. The transition may be
wonder-forward, but its literal profile, founder count, and mode facts remain available to assistive
technology. Loading, validation, or creation failure replaces the transition action with a direct
status and retry path.

# 2. World observation workspace

```text
┌ global run header + primary views + optional recovery banner                  ┐
├──────────────────────────────────────────────┬────────────────────────────────┤
│ Layer: Environment ▾     −  +  Fit  Focus    │ Selected world summary         │
│ ┌──────────────────────────────────────────┐ │ completed time · run status    │
│ │ authoritative map                       │ │ population · average health    │
│ │ live / last-known / unknown marks       │ │ behavior distribution          │
│ │ exact organisms/remnants or aggregates  │ │                                │
│ │ activity pulse symbols + center reticle │ │ Pause · Step · Speed            │
│ └──────────────────────────────────────────┘ │ Activity filters                │
│ selected tile/organism · exact scope         │ Journey subject ▾               │
├──────────────────────────────────────────────┴────────────────────────────────┤
│ Tile inspector          Organism inspector          Species inspector          │
│ current/aged/unknown    health · reserves · gates   DNA · behavior · pressure  │
├───────────────────────────────────────────────────────────────────────────────┤
│ Attention inbox · coalesced boundary alerts · links to saved evidence         │
└───────────────────────────────────────────────────────────────────────────────┘
```

The map dominates. Selection summary is always text as well as a mark. Unknown tiles do not open a
false empty inspector; reduced tiles show observation time and retained identities only. Selecting
an organism also selects its tile. Evidence links may focus a tile but cannot manufacture a current
selection when its authorization has expired.

# 3. Resource diagnosis

```text
┌ Resources · Tile 145 (current) · Organism #231 · window [ 168 hours ▾ ]       ┐
├──────────────────────────────────────────────┬────────────────────────────────┤
│ Compound stock and net flow history          │ Current organism pressure      │
│ stock line · source/sink intervals · gaps    │ requested / granted            │
│                                              │ limiting input · coverage       │
├──────────────────────────────────────────────┼────────────────────────────────┤
│ Latest completed interval                    │ Action gates                    │
│ source · inflow · capture · reaction ·       │ capture · scavenging · growth  │
│ outflow · decay · environmental attrition    │ reproduction · exact blocker   │
├──────────────────────────────────────────────┴────────────────────────────────┤
│ Typed contributor table: reaction/process · authorized species · exact q      │
└───────────────────────────────────────────────────────────────────────────────┘
```

Stock, gross flows, net flow, demand, and grant remain separate. The graph carries units and gaps;
the table reconciles to its latest interval. A reduced tile substitutes a timestamped retained
history state, never a different live tile. An unknown tile offers navigation back to observation
instead of a zero-valued chart.

# 4. Evolution planning and comparison

```text
┌ Evolution · PAUSED · 63.2 MP · +0.18 MP/hour · cooldown ready                 ┐
├───────────────────────────────────────────────────────────────────────────────┤
│ Saved goal: Low-light capture → prerequisites · current-rate ETA · Load       │
├───────────────────────────┬──────────────────────┬────────────────────────────┤
│ Strategic intent / traits │ Founding tiles       │ Authoritative preview      │
│ prerequisite-closed nodes │ [145] 50 founders   │ price · complexity         │
│ immediate / maturing /    │ [146] ancestor left │ retained branch balances   │
│ conditional / preparatory │ up to four tiles     │ cooldown · risks · timing  │
│ incompatibilities         │                      │ activation evidence         │
├───────────────────────────┴──────────────────────┴────────────────────────────┤
│ Builder proposal versus saved goal · changed facts only · neither ranked      │
├───────────────────────────────────────────────────────────────────────────────┤
│ renewable-flow and competition forecast · current-state estimate              │
│ [ Save/update goal ]                       [ Found descendant species ]        │
└───────────────────────────────────────────────────────────────────────────────┘
```

Only a current paused boundary enables commit. The preview leads with irreversible transaction
facts and labels forecast scope. Preparatory traits cannot inherit their milestone's success
language. Comparison shows differences without a recommendation or synthetic score.

# 5. Lineage tree

```text
┌ Lineage · observed tree of life                                                ┐
│                         ○ abiogenesis                                           │
│                         │                                                       │
│                ● species 1 · controlled                                         │
│                   ├──────── ● species 3 · current branch                        │
│                   │           └── ○ species 5 · extinct                         │
│                   └──────── ○ species 4 · authorized identity only              │
│                                                                                │
│ Selected species: ancestry · controller/lock · known status · [ Chronicle ]    │
└────────────────────────────────────────────────────────────────────────────────┘
```

Every node and edge has a text equivalent. Species identity and ancestry may remain visible after
operational detail becomes unavailable. Sandbox control transfer belongs here only when permitted;
Survival identifies the bound lineage and offers no backtracking control.

# 6. Attention, saved goal, and consequence review

```text
┌ Attention inbox                              ┌ Pinned evolution goal           ┐
│ Critical · sustained low health · hour 312   │ prerequisites · current-rate ETA│
│ Strategic · review boundary complete         │ no reservation · no auto-apply  │
│ Informational · first reaction activated     └─────────────────────────────────┘
│ [ Open chronicle evidence ]                                                    │
├────────────────────────────────────────────────────────────────────────────────┤
│ CONSEQUENCE REVIEW · 120 / 168 hours · evidence, not a success score           │
│ acquired DNA · authored intent · benefit timing                                │
│ Evidence                    Ancestor                   Descendant                │
│ population / health /       exact or scoped value      exact or scoped value    │
│ flow / behavior / tiles / activation                                            │
└────────────────────────────────────────────────────────────────────────────────┘
```

Alerts are boundary facts, not live toasts, and do not pause time. The review remains available
through its authored window, distinguishes exact from observed population scope, and links to its
immutable decision and event-time evidence.

# 7. Chronicle and evidence navigation

```text
┌ Chronicle · factual observed history · significance/window filters             ┐
│ Hour 168  COOLDOWN-BOUNDARY SUMMARY                                             │
│ branch facts · baseline/current evidence · capability activation               │
│ [ Decision facts ] [ Branch comparison ] [ Journey events ] [ Tile resources ] │
│ Private hypothesis [________________________________________]  browser-only     │
│                                                                                │
│ Hour 336  PROPOSAL-SPECIFIC FOLLOW-UP LANDMARK                                 │
│ longer window · cooldown not extended · same evidence/navigation contract      │
└────────────────────────────────────────────────────────────────────────────────┘
```

Navigation validates each destination at activation time. A stale resource reference remains a
factual reference but does not open exact current tile data. Private notes are visually and
semantically separated from simulation facts.

# 8. End-of-run summary

```text
┌ This lineage has ended · hour 1,204 · completed authoritative boundary          ┐
│ Recorded ending: controlled lineage extinct                                    │
│                                                                                │
│ Population arc        realized death mechanisms       final occupied range     │
│ exact/observed scope  risks versus triggering causes  map with knowledge scope │
│                                                                                │
│ Lineage branches · major chronicle landmarks · final saved-world status         │
│ [ Review chronicle ] [ Inspect lineage ] [ Save final boundary ] [ New world ] │
└────────────────────────────────────────────────────────────────────────────────┘
```

The ending explains recorded triggers and contributing pressures without collapsing multi-cause
evidence into one invented story. It never converts a risk into a cause or hidden history into a
complete retrospective. Existing evidence remains navigable; all commands that would advance the
ended world stay disabled.

# Cross-cutting loading and recovery states

```text
initial loading:   no world facts → complete boundary → reveal all active surfaces together
initial offline:   literal failure + retry; no placeholder values presented as current
recovering:        held boundary label + locked commands + automatic/manual retry
resyncing:         held boundary remains visible until a complete replacement validates
rejected command:  server explanation + refreshed boundary; safe correction may continue
unknown delivery:  do not retry mutation; resynchronize before another authoritative command
```

# Acceptance map

- Every primary surface in `CLIENT.md` has one named home above.
- The normal `Notice → Inspect → Hypothesize → Compare → Commit/bank → Observe → Reassess` loop can
  be traversed without a modal dead end.
- Global clock, save, sync, identity, and run status do not drift into per-view duplicates.
- Desktop and narrow layouts have an explicit content order and collapse rule.
- Exact, observed, retained, unknown, estimated, private, and planned information have distinct
  literal labels in every relevant surface.
- Wireframes reserve no direct-control verb for movement, feeding, reproduction, or behavior.

