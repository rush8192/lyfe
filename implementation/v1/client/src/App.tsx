import { create } from "@bufbuild/protobuf";
import { useEffect, useState } from "react";
import {
  OrganismJourneyEventFamily,
  WorldLifecycle,
  type ActorWorldProjection,
} from "./generated/lyfe/v1/projection_pb";
import {
  SimulationSpeedPreset,
  WorldControlAction,
  WorldControlCommandSchema,
} from "./generated/lyfe/v1/control_pb";
import {
  formatSimulationHour,
  classifyServerFailure,
  createWorld,
  loadWorld,
  readActiveWorldState,
  readWorldCatalogue,
  readWorldSetup,
  saveActiveWorld,
  sendWorldControl,
  unloadWorld,
  type ServerConnection,
} from "./api/server";
import { WorldCanvas } from "./rendering/WorldCanvas";
import { EvolutionPanel } from "./components/EvolutionPanel";
import { ConsequenceReviewPanel } from "./components/ConsequenceReviewPanel";
import { ResourcePressurePanel } from "./components/ResourcePressurePanel";
import { ChroniclePanel } from "./components/ChroniclePanel";
import { AttentionInbox } from "./components/AttentionInbox";
import { WorldTimeControls } from "./components/WorldTimeControls";
import { WorldKnowledgeLegend } from "./components/WorldKnowledgeLegend";
import { SpeciesSummaryPanel } from "./components/SpeciesSummaryPanel";
import { OrganismInfoPanel } from "./components/OrganismInfoPanel";
import { RemnantInfoPanel } from "./components/RemnantInfoPanel";
import { WorldSetupPanel } from "./components/WorldSetupPanel";
import { WorldPersistencePanel } from "./components/WorldPersistencePanel";
import { TileInspectorPanel } from "./components/TileInspectorPanel";
import {
  ActivityFilters,
  DEFAULT_ACTIVITY_FAMILIES,
} from "./components/ActivityJourneyPanel";
import { LossPostmortemPanel } from "./components/LossPostmortemPanel";
import {
  ConnectionStatusPanel,
  type ClientSyncState,
} from "./components/ConnectionStatusPanel";
import type { CreateWorldRequest } from "./generated/lyfe/v1/setup_pb";
import {
  LoadWorldRequestSchema,
  SaveActiveWorldRequestSchema,
  SavedWorldSummarySchema,
  UnloadWorldRequestSchema,
  WorldCatalogueSchema,
  type SavedWorldSummary,
  type WorldCatalogue,
} from "./generated/lyfe/v1/persistence_pb";
import {
  createConsequenceReview,
  readPinnedConsequenceReview,
  writeConsequenceReview,
  type ConsequenceReview,
  type ConsequenceReviewStorage,
} from "./state/consequenceReview";
import {
  readWorldChronicleNotes,
  writeChronicleNote,
  type ChronicleNoteStorage,
} from "./state/chronicleNotes";
import {
  browserWorldViewPreferenceStorage,
  readWorldViewPreferences,
  resolveWorldViewSelection,
  updateWorldViewPreferences,
} from "./state/worldViewPreferences";

const DEBUG_UI = import.meta.env.VITE_LYFE_DEBUG_UI?.toLowerCase() === "true";

export function App() {
  const [connection, setConnection] = useState<ServerConnection>({
    status: "loading",
  });
  const [enabledEventFamilies, setEnabledEventFamilies] = useState<
    ReadonlySet<OrganismJourneyEventFamily>
  >(() => new Set(DEFAULT_ACTIVITY_FAMILIES));
  const [selectedOrganismId, setSelectedOrganismId] = useState<bigint | null>(null);
  const [selectedRemnantId, setSelectedRemnantId] = useState<bigint | null>(null);
  const [selectedOtherSpeciesId, setSelectedOtherSpeciesId] = useState<bigint | null>(null);
  const [selectedEvidenceTileId, setSelectedEvidenceTileId] = useState<number | null>(null);
  const [followingOrganism, setFollowingOrganism] = useState(false);
  const [preferenceWorldId, setPreferenceWorldId] = useState<bigint | null>(null);
  const [mapFocusRevision, setMapFocusRevision] = useState(0);
  const [reloadRevision, setReloadRevision] = useState(0);
  const [pollRevision, setPollRevision] = useState(0);
  const [syncState, setSyncState] = useState<ClientSyncState>({ status: "current" });
  const [controlBusy, setControlBusy] = useState(false);
  const [controlError, setControlError] = useState<string | null>(null);
  const [setupBusy, setSetupBusy] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [catalogue, setCatalogue] = useState<WorldCatalogue | null>(null);
  const [persistenceBusy, setPersistenceBusy] = useState(false);
  const [persistenceError, setPersistenceError] = useState<string | null>(null);
  const [consequenceReview, setConsequenceReview] = useState<ConsequenceReview | null>(null);
  const [chronicleNotes, setChronicleNotes] = useState<ReadonlyMap<bigint, string>>(
    () => new Map(),
  );

  useEffect(() => {
    const controller = new AbortController();

    Promise.all([
      readWorldSetup(undefined, controller.signal),
      readWorldCatalogue(controller.signal),
    ])
      .then(async ([setup, currentCatalogue]) => {
        setCatalogue(currentCatalogue);
        if (!setup.hasActiveWorld) {
          setConnection({ status: "setup", setup });
          setSyncState({ status: "current" });
          return;
        }
        const { cache, evolution, control } = await readActiveWorldState(controller.signal);
        setConnection({ status: "online", cache, evolution, control });
        setCatalogue(alignCatalogueWithBoundary(
          currentCatalogue,
          cache.world.worldId,
          cache.world.worldRevision,
        ));
        setSyncState({ status: "current" });
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          const failure = classifyServerFailure(error);
          setConnection({
            status: "offline",
            message: failure.message,
          });
        }
      });

    return () => controller.abort();
  }, [reloadRevision]);

  async function previewSetupSeed(seed: string) {
    if (setupBusy) return;
    const controller = new AbortController();
    setSetupBusy(true);
    setSetupError(null);
    try {
      const setup = await readWorldSetup(seed, controller.signal);
      setConnection({ status: "setup", setup });
    } catch (error: unknown) {
      setSetupError(error instanceof Error ? error.message : "The seed could not be previewed.");
    } finally {
      setSetupBusy(false);
    }
  }

  async function beginWorld(request: CreateWorldRequest) {
    if (setupBusy) return;
    const controller = new AbortController();
    setSetupBusy(true);
    setSetupError(null);
    try {
      await createWorld(request, controller.signal);
      const [{ cache, evolution, control }, currentCatalogue] = await Promise.all([
        readActiveWorldState(controller.signal),
        readWorldCatalogue(controller.signal),
      ]);
      setConnection({ status: "online", cache, evolution, control });
      setCatalogue(alignCatalogueWithBoundary(
        currentCatalogue,
        cache.world.worldId,
        cache.world.worldRevision,
      ));
      setSyncState({ status: "current" });
    } catch (error: unknown) {
      setSetupError(error instanceof Error ? error.message : "The world could not be created.");
    } finally {
      setSetupBusy(false);
    }
  }

  useEffect(() => {
    if (connection.status !== "online" ||
        connection.control.lifecycle !== WorldLifecycle.RUNNING || controlBusy ||
        syncState.status === "resyncing") return;
    const controller = new AbortController();
    const delay = syncState.status === "recovering"
      ? 2_000
      : Math.min(1_000, Math.max(100, connection.control.targetIntervalMs));
    let timer = window.setTimeout(() => {
      readActiveWorldState(controller.signal)
        .then(({ cache, evolution, control }) => {
          if (!controller.signal.aborted) {
            setConnection({ status: "online", cache, evolution, control });
            setSyncState({ status: "current" });
            setCatalogue((current) => current === null
              ? null
              : alignCatalogueWithBoundary(
                  current,
                  cache.world.worldId,
                  cache.world.worldRevision,
                ));
          }
        })
        .catch((error: unknown) => {
          if (!controller.signal.aborted) {
            const failure = classifyServerFailure(error);
            setSyncState({ status: "recovering", message: failure.message });
            timer = window.setTimeout(
              () => setPollRevision((revision) => revision + 1),
              2_000,
            );
          }
        });
    }, delay);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [connection, controlBusy, pollRevision, syncState.status]);

  async function retryConnection() {
    if (connection.status !== "online") {
      setConnection({ status: "loading" });
      setReloadRevision((revision) => revision + 1);
      return;
    }
    if (syncState.status === "resyncing") return;
    const controller = new AbortController();
    setSyncState({
      status: "resyncing",
      message: syncState.status === "recovering"
        ? syncState.message
        : "Requesting a complete authoritative boundary.",
    });
    try {
      const [{ cache, evolution, control }, currentCatalogue] = await Promise.all([
        readActiveWorldState(controller.signal),
        readWorldCatalogue(controller.signal),
      ]);
      setConnection({ status: "online", cache, evolution, control });
      setCatalogue(alignCatalogueWithBoundary(
        currentCatalogue,
        cache.world.worldId,
        cache.world.worldRevision,
      ));
      setSyncState({ status: "current" });
    } catch (error: unknown) {
      const failure = classifyServerFailure(error);
      setSyncState({ status: "recovering", message: failure.message });
    }
  }

  async function issueWorldControl(
    action: WorldControlAction,
    speed = SimulationSpeedPreset.UNSPECIFIED,
  ) {
    if (connection.status !== "online" || controlBusy || syncState.status !== "current") return;
    const controller = new AbortController();
    setControlBusy(true);
    setControlError(null);
    try {
      await sendWorldControl(create(WorldControlCommandSchema, {
        expectedControlRevision: connection.control.controlRevision,
        action,
        speed,
      }), controller.signal);
      const { cache, evolution, control } = await readActiveWorldState(controller.signal);
      setConnection({ status: "online", cache, evolution, control });
      setCatalogue((current) => current === null
        ? null
        : alignCatalogueWithBoundary(
            current,
            cache.world.worldId,
            cache.world.worldRevision,
          ));
      setSyncState({ status: "current" });
    } catch (error: unknown) {
      const failure = classifyServerFailure(error);
      setControlError(failure.kind === "rejected"
        ? `Command rejected. ${failure.message}`
        : "Command delivery could not be confirmed. The displayed boundary has not been advanced locally.");
      try {
        const { cache, evolution, control } = await readActiveWorldState(controller.signal);
        setConnection({ status: "online", cache, evolution, control });
        setSyncState({ status: "current" });
      } catch (refreshError: unknown) {
        const refreshFailure = classifyServerFailure(refreshError);
        setSyncState({ status: "recovering", message: refreshFailure.message });
      }
    } finally {
      setControlBusy(false);
    }
  }

  async function saveCurrentWorld() {
    if (connection.status !== "online" || persistenceBusy || syncState.status !== "current") return;
    const controller = new AbortController();
    setPersistenceBusy(true);
    setPersistenceError(null);
    try {
      const currentCatalogue = await saveActiveWorld(create(SaveActiveWorldRequestSchema, {
        expectedWorldId: connection.cache.world.worldId,
        expectedWorldRevision: connection.cache.world.worldRevision,
      }), controller.signal);
      const { cache, evolution, control } = await readActiveWorldState(controller.signal);
      setConnection({ status: "online", cache, evolution, control });
      setCatalogue(alignCatalogueWithBoundary(
        currentCatalogue,
        cache.world.worldId,
        cache.world.worldRevision,
      ));
      setSyncState({ status: "current" });
    } catch (error: unknown) {
      const failure = classifyServerFailure(error);
      setPersistenceError(failure.kind === "rejected"
        ? `Save rejected. ${failure.message}`
        : "Save completion could not be confirmed. Resynchronize before trying again.");
      if (failure.kind !== "rejected") {
        setSyncState({ status: "recovering", message: failure.message });
      }
    } finally {
      setPersistenceBusy(false);
    }
  }

  async function loadSavedWorld(world: SavedWorldSummary, confirmDiscardUnsaved: boolean) {
    if (persistenceBusy || syncState.status !== "current") return;
    const controller = new AbortController();
    setPersistenceBusy(true);
    setPersistenceError(null);
    try {
      const currentCatalogue = await loadWorld(create(LoadWorldRequestSchema, {
        worldId: world.worldId,
        expectedActiveWorldId: catalogue?.activeWorldId ?? 0n,
        expectedActiveWorldRevision: catalogue?.activeWorldRevision ?? 0n,
        confirmDiscardUnsaved,
      }), controller.signal);
      const { cache, evolution, control } = await readActiveWorldState(controller.signal);
      setConnection({ status: "online", cache, evolution, control });
      setCatalogue(alignCatalogueWithBoundary(
        currentCatalogue,
        cache.world.worldId,
        cache.world.worldRevision,
      ));
      setSyncState({ status: "current" });
      setSelectedOrganismId(null);
      setSelectedRemnantId(null);
      setSelectedEvidenceTileId(null);
      setFollowingOrganism(false);
    } catch (error: unknown) {
      const failure = classifyServerFailure(error);
      setPersistenceError(failure.kind === "rejected"
        ? `Load rejected. ${failure.message}`
        : "Load completion could not be confirmed. Resynchronize before issuing another command.");
      if (failure.kind !== "rejected" && connection.status === "online") {
        setSyncState({ status: "recovering", message: failure.message });
      }
    } finally {
      setPersistenceBusy(false);
    }
  }

  async function unloadCurrentWorld(confirmDiscardUnsaved: boolean) {
    if (connection.status !== "online" || persistenceBusy || syncState.status !== "current") return;
    const controller = new AbortController();
    setPersistenceBusy(true);
    setPersistenceError(null);
    try {
      const currentCatalogue = await unloadWorld(create(UnloadWorldRequestSchema, {
        expectedWorldId: connection.cache.world.worldId,
        expectedWorldRevision: connection.cache.world.worldRevision,
        confirmDiscardUnsaved,
      }), controller.signal);
      const setup = await readWorldSetup(undefined, controller.signal);
      setCatalogue(currentCatalogue);
      setConnection({ status: "setup", setup });
      setSyncState({ status: "current" });
      setSelectedOrganismId(null);
      setSelectedRemnantId(null);
      setSelectedEvidenceTileId(null);
      setFollowingOrganism(false);
    } catch (error: unknown) {
      const failure = classifyServerFailure(error);
      setPersistenceError(failure.kind === "rejected"
        ? `Unload rejected. ${failure.message}`
        : "Unload completion could not be confirmed. Resynchronize before issuing another command.");
      if (failure.kind !== "rejected") {
        setSyncState({ status: "recovering", message: failure.message });
      }
    } finally {
      setPersistenceBusy(false);
    }
  }

  const world = connection.status === "online" ? connection.cache.world : null;
  const syncLocked = connection.status === "online" && syncState.status !== "current";
  const visibleConsequenceReview = world !== null && consequenceReview !== null &&
    consequenceReview.worldId === world.worldId &&
    consequenceReview.descendantSpeciesId === world.controlledSpeciesId &&
    world.simulatedHours >= consequenceReview.applicationHour &&
    world.simulatedHours <= consequenceReview.cooldownEndHour
    ? consequenceReview
    : null;

  useEffect(() => {
    if (world === null) return;
    const storage = browserConsequenceReviewStorage();
    setConsequenceReview(storage === null
      ? null
      : readPinnedConsequenceReview(storage, world));
  }, [world]);
  useEffect(() => {
    if (world === null) return;
    const storage = browserChronicleNoteStorage();
    setChronicleNotes(storage === null
      ? new Map()
      : readWorldChronicleNotes(storage, world.worldId));
  }, [world?.worldId]);
  const activeOrganismId = selectedOrganismId;
  const selectedOrganism = world === null || selectedOrganismId === null
    ? null
    : findVisibleOrganism(world, selectedOrganismId);
  const selectedRemnant = world === null || selectedRemnantId === null
    ? null
    : findVisibleRemnant(world, selectedRemnantId);
  const selectedRemnantDeathEvent = world === null || selectedRemnant === null
    ? undefined
    : world.journeyEvents.find((event) =>
        event.family === OrganismJourneyEventFamily.DEATH &&
        event.relatedRemnantId === selectedRemnant.remnant.remnantId);

  useEffect(() => {
    if (world === null) {
      setPreferenceWorldId(null);
      return;
    }
    const storage = browserWorldViewPreferenceStorage();
    const preferences = storage === null
      ? null
      : readWorldViewPreferences(storage, world.worldId);
    const selection = resolveWorldViewSelection(world, preferences);
    setSelectedEvidenceTileId(selection.selectedTileId);
    setSelectedOrganismId(null);
    setSelectedRemnantId(null);
    setSelectedOtherSpeciesId(null);
    setFollowingOrganism(false);
    setPreferenceWorldId(world.worldId);
  }, [world?.worldId]);

  useEffect(() => {
    if (world === null) return;
    if (selectedOrganismId !== null &&
        (selectedOrganism === null || selectedOrganism.organism.speciesId !== world.controlledSpeciesId)) {
      setSelectedOrganismId(null);
      setFollowingOrganism(false);
    } else if (selectedOrganism !== null && selectedOrganism.tileId !== selectedEvidenceTileId) {
      setSelectedEvidenceTileId(selectedOrganism.tileId);
    }
    if (selectedRemnantId !== null && selectedRemnant === null) {
      setSelectedRemnantId(null);
    } else if (selectedRemnant !== null && selectedRemnant.tileId !== selectedEvidenceTileId) {
      setSelectedEvidenceTileId(selectedRemnant.tileId);
    }
    if (selectedOtherSpeciesId !== null &&
        (selectedOtherSpeciesId === world.controlledSpeciesId ||
          !world.species.some((species) => species.speciesId === selectedOtherSpeciesId))) {
      setSelectedOtherSpeciesId(null);
    }
  }, [selectedEvidenceTileId, selectedOrganism, selectedOrganismId, selectedOtherSpeciesId,
    selectedRemnant, selectedRemnantId, world]);

  useEffect(() => {
    if (world === null || preferenceWorldId !== world.worldId) return;
    const storage = browserWorldViewPreferenceStorage();
    if (storage === null) return;
    updateWorldViewPreferences(storage, world.worldId, {
      selectedTileId: selectedEvidenceTileId ?? undefined,
    });
  }, [
    preferenceWorldId,
    selectedEvidenceTileId,
    world?.worldId,
  ]);
  const journey = world === null
    ? []
    : world.journeyEvents
        .filter((event) => event.subjectOrganismId === activeOrganismId)
        .slice()
        .reverse();
  const routineActivity = world === null
    ? []
    : world.routineActivitySummaries
        .filter((summary) => summary.subjectOrganismId === activeOrganismId)
        .slice()
        .reverse();

  function returnToSimulationOverview() {
    setSelectedOrganismId(null);
    setSelectedRemnantId(null);
    setSelectedOtherSpeciesId(null);
    setFollowingOrganism(false);
  }

  if (connection.status === "setup") {
    return (
      <>
        <a className="skip-link" href="#main-content">Skip to world controls</a>
        <main id="main-content" className="app-shell setup-shell" tabIndex={-1}>
          <WorldSetupPanel
            surface={connection.setup}
            busy={setupBusy}
            error={setupError}
            onPreviewSeed={(seed) => void previewSetupSeed(seed)}
            onCreate={(request) => void beginWorld(request)}
          />
          {catalogue === null ? null : (
            <WorldPersistencePanel
              catalogue={catalogue}
              activeWorldRunning={false}
              busy={persistenceBusy}
              error={persistenceError}
              onSave={() => undefined}
              onLoad={(world, confirmed) => void loadSavedWorld(world, confirmed)}
              onUnload={() => undefined}
            />
          )}
        </main>
      </>
    );
  }

  return (
    <>
      <a className="skip-link" href="#main-content">Skip to world controls</a>
      <main id="main-content" className="app-shell" tabIndex={-1}>
      <header className="masthead">
        <div>
          <p className="eyebrow">A world learning to live</p>
          <h1>LYFE</h1>
        </div>
        <ConnectionBadge connection={connection} syncState={syncState} />
      </header>

      <ConnectionStatusPanel
        phase={connection.status === "loading" || connection.status === "offline"
          ? connection.status
          : syncState.status}
        message={connection.status === "offline" ? connection.message
          : syncState.status === "recovering" || syncState.status === "resyncing"
            ? syncState.message
            : undefined}
        world={world ?? undefined}
        onRetry={() => void retryConnection()}
      />

      {world === null ? null : <LossPostmortemPanel world={world} />}

      <section className="world-panel">
        <WorldCanvas
          world={world}
          enabledEventFamilies={enabledEventFamilies}
          selectedTileId={selectedEvidenceTileId}
          selectedOrganismId={activeOrganismId}
          selectedRemnantId={selectedRemnantId}
          selectedSpeciesId={selectedOtherSpeciesId}
          focusRequestRevision={mapFocusRevision}
          followingOrganism={followingOrganism}
          onFollowingOrganismChange={setFollowingOrganism}
          onSelectTile={(tileId) => {
            setSelectedEvidenceTileId(tileId);
            setSelectedOrganismId(null);
            setSelectedRemnantId(null);
            setSelectedOtherSpeciesId(null);
            setFollowingOrganism(false);
          }}
          onSelectOrganism={(organismId, tileId) => {
            if (world === null) return;
            setSelectedEvidenceTileId(tileId);
            setSelectedRemnantId(null);
            const selected = findVisibleOrganism(world, organismId);
            if (selected?.organism.speciesId === world.controlledSpeciesId) {
              setSelectedOrganismId(organismId);
              setSelectedOtherSpeciesId(null);
            } else if (selected !== null) {
              setSelectedOrganismId(null);
              setSelectedOtherSpeciesId(selected.organism.speciesId);
              setFollowingOrganism(false);
            }
          }}
          onSelectRemnant={(remnantId, tileId) => {
            setSelectedEvidenceTileId(tileId);
            setSelectedOrganismId(null);
            setSelectedOtherSpeciesId(null);
            setSelectedRemnantId(remnantId);
            setFollowingOrganism(false);
          }}
        />
        <div className="world-copy">
          {world === null ? (
            <>
              <p className="section-label">World view unavailable</p>
              <h2>Waiting for a complete boundary</h2>
              <p>
                No map, organism, resource, or clock value is treated as current until
                the host returns one internally consistent authoritative view.
              </p>
            </>
          ) : null}
          {connection.status === "online" && selectedOrganism !== null ? (
            <OrganismInfoPanel
              organism={selectedOrganism.organism}
              tileId={selectedOrganism.tileId}
              journey={journey}
              routineActivity={routineActivity}
              onBack={returnToSimulationOverview}
            />
          ) : connection.status === "online" && selectedRemnant !== null ? (
            <RemnantInfoPanel
              remnant={selectedRemnant.remnant}
              tileId={selectedRemnant.tileId}
              completedTick={connection.cache.world.completedTick}
              tickDurationHours={connection.cache.world.tickDurationHours}
              deathEvent={selectedRemnantDeathEvent}
              resourceDefinitions={connection.cache.world.resourceDefinitions}
              onBack={returnToSimulationOverview}
            />
          ) : connection.status === "online" && selectedOtherSpeciesId !== null ? (
            <SpeciesSummaryPanel
              world={connection.cache.world}
              speciesId={selectedOtherSpeciesId}
              onBack={returnToSimulationOverview}
            />
          ) : connection.status === "online" ? (
            <>
              <WorldTimeControls
                control={connection.control}
                simulationTimeLabel={formatSimulationHour(connection.cache.world.simulatedHours)}
                tickDurationHours={connection.cache.world.tickDurationHours}
                visibleOrganismCount={countVisibleOrganisms(connection.cache.world)}
                showDebug={DEBUG_UI}
                busy={controlBusy || syncLocked}
                error={controlError}
                onCommand={(action, speed) => void issueWorldControl(action, speed)}
              />
              <SpeciesSummaryPanel
                world={connection.cache.world}
                speciesId={connection.cache.world.controlledSpeciesId}
              />
              <WorldKnowledgeLegend />
              <ActivityFilters
                enabled={enabledEventFamilies}
                onToggle={(family) => setEnabledEventFamilies((current) => {
                  const next = new Set(current);
                  if (next.has(family)) next.delete(family);
                  else next.add(family);
                  return next;
                })}
              />
            </>
          ) : null}
        </div>
      </section>
      {world === null ? null : (
        <TileInspectorPanel
          world={world}
          evolution={connection.status === "online" ? connection.evolution : null}
          selectedTileId={selectedEvidenceTileId}
        />
      )}
      {catalogue === null ? null : (
        <WorldPersistencePanel
          catalogue={catalogue}
          activeWorldRunning={connection.status === "online" &&
            connection.control.lifecycle === WorldLifecycle.RUNNING}
          busy={persistenceBusy || syncLocked}
          error={persistenceError}
          onSave={() => void saveCurrentWorld()}
          onLoad={(saved, confirmed) => void loadSavedWorld(saved, confirmed)}
          onUnload={(confirmed) => void unloadCurrentWorld(confirmed)}
        />
      )}
      {world !== null ? (
        <ResourcePressurePanel
          world={world}
          selectedOrganismId={activeOrganismId}
          selectedTileId={selectedEvidenceTileId}
        />
      ) : null}
      {connection.status === "online" && visibleConsequenceReview !== null ? (
        <ConsequenceReviewPanel
          review={visibleConsequenceReview}
          world={connection.cache.world}
          evolution={connection.evolution}
        />
      ) : null}
      {connection.status === "online" ? (
        <AttentionInbox world={connection.cache.world} />
      ) : null}
      {connection.status === "online" ? (
        <ChroniclePanel
          world={connection.cache.world}
          evolution={connection.evolution}
          notes={chronicleNotes}
          onNoteChange={(eventId, text) => {
            setChronicleNotes((current) => {
              const next = new Map(current);
              if (text.trim().length === 0) next.delete(eventId);
              else next.set(eventId, text.normalize("NFC"));
              return next;
            });
            const storage = browserChronicleNoteStorage();
            if (storage !== null) {
              try {
                writeChronicleNote(storage, connection.cache.world.worldId, eventId, text);
              } catch {
                // Local annotation failure cannot alter the authoritative chronicle.
              }
            }
          }}
          onNavigateResourceTile={(tileId) => {
            setSelectedEvidenceTileId(tileId);
            setSelectedOrganismId(null);
            setSelectedRemnantId(null);
            setSelectedOtherSpeciesId(null);
            setFollowingOrganism(false);
            setMapFocusRevision((revision) => revision + 1);
          }}
        />
      ) : null}
      {connection.status === "online" &&
      connection.control.lifecycle === WorldLifecycle.PAUSED_READY ? (
        <EvolutionPanel
          surface={connection.evolution}
          disabled={syncLocked}
          onConnectionFailure={(message) => setSyncState({ status: "recovering", message })}
          onApplied={(response, request) => {
            try {
              const review = createConsequenceReview(
                connection.cache.world,
                response,
                request,
              );
              setConsequenceReview(review);
              const storage = browserConsequenceReviewStorage();
              if (storage !== null) {
                writeConsequenceReview(storage, review);
              }
            } catch {
              // A failed local review cannot change the accepted authoritative branch.
            }
            setReloadRevision((value) => value + 1);
          }}
        />
      ) : connection.status === "online" ? (
        <section className="evolution-panel evolution-paused-note">
          <p className="section-label">Evolution</p>
          <h2>Pause to shape what comes next</h2>
          <p>Evolution proposals are prepared and committed only at a held, completed boundary.</p>
        </section>
      ) : null}
      </main>
    </>
  );
}

function alignCatalogueWithBoundary(
  catalogue: WorldCatalogue,
  worldId: bigint,
  worldRevision: bigint,
): WorldCatalogue {
  const savedWorlds = catalogue.savedWorlds.map((saved) => create(
    SavedWorldSummarySchema,
    {
      ...saved,
      isActive: saved.worldId === worldId,
      isActiveSavedBoundary: saved.worldId === worldId &&
        saved.worldRevision === worldRevision,
    },
  ));
  return create(WorldCatalogueSchema, {
    ...catalogue,
    savedWorlds,
    hasActiveWorld: true,
    activeWorldId: worldId,
    activeWorldRevision: worldRevision,
    activeHasUnsavedChanges: !savedWorlds.some((saved) => saved.isActiveSavedBoundary),
  });
}

function browserConsequenceReviewStorage(): ConsequenceReviewStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function browserChronicleNoteStorage(): ChronicleNoteStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function findVisibleOrganism(
  world: ActorWorldProjection,
  organismId: bigint,
) {
  for (const tile of world.tiles) {
    if (tile.detail.case !== "live") continue;
    const organism = tile.detail.value.organisms.find((candidate) =>
      candidate.organismId === organismId);
    if (organism !== undefined) return { organism, tileId: tile.tileId };
  }
  return null;
}

function findVisibleRemnant(
  world: ActorWorldProjection,
  remnantId: bigint,
) {
  for (const tile of world.tiles) {
    if (tile.detail.case !== "live") continue;
    const remnant = tile.detail.value.remnants.find((candidate) =>
      candidate.remnantId === remnantId);
    if (remnant !== undefined) return { remnant, tileId: tile.tileId };
  }
  return null;
}

function countVisibleOrganisms(
  world: Extract<ServerConnection, { status: "online" }>["cache"]["world"],
): number {
  return world.tiles.reduce(
    (total, tile) =>
      total + (tile.detail.case === "live" ? tile.detail.value.organisms.length : 0),
    0,
  );
}

function ConnectionBadge({
  connection,
  syncState,
}: {
  readonly connection: ServerConnection;
  readonly syncState: ClientSyncState;
}) {
  if (connection.status === "loading") {
    return <span className="connection pending">Connecting</span>;
  }

  if (connection.status === "offline") {
    return (
      <span className="connection offline" title={connection.message}>
        Server offline
      </span>
    );
  }

  if (syncState.status !== "current") {
    return <span className="connection recovering">Reconnecting</span>;
  }

  return <span className="connection online">Server connected</span>;
}
