import { fromBinary, toBinary, type Message } from "@bufbuild/protobuf";
import type { GenMessage } from "@bufbuild/protobuf/codegenv2";
import {
  ApplySpeciationRequestSchema,
  ApplySpeciationResponseSchema,
  EvolutionDecisionSurfaceSchema,
  SpeciationProposalRequestSchema,
  SpeciationProposalSchema,
  type ApplySpeciationRequest,
  type ApplySpeciationResponse,
  type EvolutionDecisionSurface,
  type SpeciationProposal,
  type SpeciationProposalRequest,
} from "../generated/lyfe/v1/evolution_pb";
import {
  ProjectionSnapshotSchema,
} from "../generated/lyfe/v1/projection_pb";
import {
  ActiveWorldStateSchema,
  WorldControlCommandSchema,
  WorldControlStateSchema,
  type WorldControlCommand,
  type WorldControlState,
} from "../generated/lyfe/v1/control_pb";
import {
  CreateWorldRequestSchema,
  CreateWorldResponseSchema,
  WorldSetupSurfaceSchema,
  type CreateWorldRequest,
  type CreateWorldResponse,
  type WorldSetupSurface,
} from "../generated/lyfe/v1/setup_pb";
import {
  LoadWorldRequestSchema,
  SaveActiveWorldRequestSchema,
  UnloadWorldRequestSchema,
  WorldCatalogueSchema,
  type LoadWorldRequest,
  type SaveActiveWorldRequest,
  type UnloadWorldRequest,
  type WorldCatalogue,
} from "../generated/lyfe/v1/persistence_pb";
import { createProjectionCache, type ProjectionCache } from "../state/projectionCache";
import { validateSpeciationProposal } from "../state/evolutionPlanner";

const REQUEST_TIMEOUT_MS = 8_000;

export type ServerFailureKind = "rejected" | "unavailable" | "protocol";

export class ServerRequestError extends Error {
  constructor(
    readonly kind: ServerFailureKind,
    message: string,
    readonly status?: number,
  ) {
    super(message);
    this.name = "ServerRequestError";
  }
}

export function classifyServerFailure(error: unknown): {
  readonly kind: ServerFailureKind;
  readonly message: string;
} {
  if (error instanceof ServerRequestError) {
    return { kind: error.kind, message: error.message };
  }
  if (error instanceof TypeError) {
    return { kind: "unavailable", message: "The world host could not be reached." };
  }
  return {
    kind: "protocol",
    message: error instanceof Error ? error.message : "The server response could not be read.",
  };
}

export type ServerConnection =
  | { readonly status: "loading" }
  | { readonly status: "offline"; readonly message: string }
  | { readonly status: "setup"; readonly setup: WorldSetupSurface }
  | {
      readonly status: "online";
      readonly cache: ProjectionCache;
      readonly evolution: EvolutionDecisionSurface;
      readonly control: WorldControlState;
    };

export async function readWorldSetup(
  seed: string | undefined,
  signal: AbortSignal,
): Promise<WorldSetupSurface> {
  const query = seed === undefined ? "" : `?seed=${encodeURIComponent(seed)}`;
  return readProtobuf(
    `/api/v1/worlds/setup${query}`,
    WorldSetupSurfaceSchema,
    signal,
  );
}

export async function createWorld(
  request: CreateWorldRequest,
  signal: AbortSignal,
): Promise<CreateWorldResponse> {
  return postProtobuf(
    "/api/v1/worlds",
    CreateWorldRequestSchema,
    request,
    CreateWorldResponseSchema,
    signal,
  );
}

export async function readWorldCatalogue(signal: AbortSignal): Promise<WorldCatalogue> {
  return readProtobuf("/api/v1/worlds", WorldCatalogueSchema, signal);
}

export async function saveActiveWorld(
  request: SaveActiveWorldRequest,
  signal: AbortSignal,
): Promise<WorldCatalogue> {
  return postProtobuf(
    "/api/v1/worlds/active/save",
    SaveActiveWorldRequestSchema,
    request,
    WorldCatalogueSchema,
    signal,
  );
}

export async function loadWorld(
  request: LoadWorldRequest,
  signal: AbortSignal,
): Promise<WorldCatalogue> {
  return postProtobuf(
    "/api/v1/worlds/load",
    LoadWorldRequestSchema,
    request,
    WorldCatalogueSchema,
    signal,
  );
}

export async function unloadWorld(
  request: UnloadWorldRequest,
  signal: AbortSignal,
): Promise<WorldCatalogue> {
  return postProtobuf(
    "/api/v1/worlds/active/unload",
    UnloadWorldRequestSchema,
    request,
    WorldCatalogueSchema,
    signal,
  );
}

export async function readActiveWorldState(signal: AbortSignal) {
  const state = await readProtobuf(
    "/api/v1/worlds/active/state",
    ActiveWorldStateSchema,
    signal,
  );
  if (state.projection === undefined || state.projection.projection === undefined ||
      state.evolution === undefined || state.control === undefined) {
    throw new Error("Active-world state is missing a required projection.");
  }
  const cache = createProjectionCache(state.projection);
  const evolution = state.evolution;
  const control = state.control;
  if (
    cache.world.worldId !== evolution.worldId ||
    cache.world.worldRevision !== evolution.worldRevision ||
    cache.world.controlledSpeciesId !== evolution.controlledSpeciesId ||
    cache.world.completedTick !== evolution.completedTick ||
    cache.world.worldRevision !== control.worldRevision ||
    cache.world.completedTick !== control.completedTick ||
    cache.world.simulatedHours !== control.simulatedHours ||
    cache.world.lifecycle !== control.lifecycle ||
    cache.world.gameplay?.runStatus !== control.gameRunStatus
  ) {
    throw new Error("Active-world projections were captured at different boundaries.");
  }
  return { cache, evolution, control };
}

export async function sendWorldControl(
  request: WorldControlCommand,
  signal: AbortSignal,
): Promise<WorldControlState> {
  return postProtobuf(
    "/api/v1/worlds/active/control",
    WorldControlCommandSchema,
    request,
    WorldControlStateSchema,
    signal,
  );
}

export async function readActiveWorldProjection(
  signal: AbortSignal,
): Promise<ProjectionCache> {
  const response = await fetchFromServer("/api/v1/worlds/active/projection", {
    headers: { Accept: "application/x-protobuf" },
    signal,
  });

  if (!response.ok) {
    throw new ServerRequestError(
      response.status >= 500 ? "unavailable" : "rejected",
      `Server returned HTTP ${response.status}.`,
      response.status,
    );
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.startsWith("application/x-protobuf")) {
    throw new ServerRequestError(
      "protocol",
      `Server returned unexpected content type ${contentType}.`,
    );
  }

  return createProjectionCache(
    fromBinary(
      ProjectionSnapshotSchema,
      new Uint8Array(await response.arrayBuffer()),
    ),
  );
}

export async function readActiveEvolutionDecisionSurface(
  signal: AbortSignal,
): Promise<EvolutionDecisionSurface> {
  return readProtobuf(
    "/api/v1/worlds/active/evolution",
    EvolutionDecisionSurfaceSchema,
    signal,
  );
}

export async function previewSpeciation(
  request: SpeciationProposalRequest,
  signal: AbortSignal,
): Promise<SpeciationProposal> {
  const proposal = await postProtobuf(
    "/api/v1/worlds/active/evolution/preview",
    SpeciationProposalRequestSchema,
    request,
    SpeciationProposalSchema,
    signal,
  );
  validateSpeciationProposal(proposal, request);
  return proposal;
}

export async function applySpeciation(
  request: ApplySpeciationRequest,
  signal: AbortSignal,
): Promise<ApplySpeciationResponse> {
  const response = await postProtobuf(
    "/api/v1/worlds/active/evolution/apply",
    ApplySpeciationRequestSchema,
    request,
    ApplySpeciationResponseSchema,
    signal,
  );
  if (request.proposal === undefined || response.proposal === undefined) {
    throw new Error("Evolution apply response is missing its proposal contract.");
  }
  validateSpeciationProposal(response.proposal, request.proposal);
  return response;
}

async function readProtobuf<T extends Message>(
  url: string,
  schema: GenMessage<T>,
  signal: AbortSignal,
): Promise<T> {
  const response = await fetchFromServer(url, {
    headers: { Accept: "application/x-protobuf" },
    signal,
  });
  return decodeResponse(response, schema);
}

async function postProtobuf<TRequest extends Message, TResponse extends Message>(
  url: string,
  requestSchema: GenMessage<TRequest>,
  request: TRequest,
  responseSchema: GenMessage<TResponse>,
  signal: AbortSignal,
): Promise<TResponse> {
  const response = await fetchFromServer(url, {
    method: "POST",
    headers: {
      Accept: "application/x-protobuf",
      "Content-Type": "application/x-protobuf",
    },
    body: toBinary(requestSchema, request),
    signal,
  });
  return decodeResponse(response, responseSchema);
}

async function decodeResponse<T extends Message>(
  response: Response,
  schema: GenMessage<T>,
): Promise<T> {
  if (!response.ok) {
    const body = await response.text();
    let detail = "";
    try {
      const problem = JSON.parse(body) as { detail?: unknown };
      if (typeof problem.detail === "string") detail = ` ${problem.detail}`;
    } catch {
      // Non-problem responses intentionally expose only their status.
    }
    throw new ServerRequestError(
      response.status >= 500 ? "unavailable" : "rejected",
      `Server returned HTTP ${response.status}.${detail}`,
      response.status,
    );
  }
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.startsWith("application/x-protobuf")) {
    throw new ServerRequestError(
      "protocol",
      `Server returned unexpected content type ${contentType}.`,
    );
  }
  try {
    return fromBinary(schema, new Uint8Array(await response.arrayBuffer()));
  } catch (error: unknown) {
    throw new ServerRequestError(
      "protocol",
      error instanceof Error
        ? `Server returned invalid protobuf data. ${error.message}`
        : "Server returned invalid protobuf data.",
    );
  }
}

async function fetchFromServer(input: RequestInfo | URL, init: RequestInit): Promise<Response> {
  const upstreamSignal = init.signal;
  const timeoutController = new AbortController();
  let timedOut = false;
  const abortFromUpstream = () => timeoutController.abort(upstreamSignal?.reason);
  if (upstreamSignal?.aborted) abortFromUpstream();
  else upstreamSignal?.addEventListener("abort", abortFromUpstream, { once: true });
  const timeout = setTimeout(() => {
    timedOut = true;
    timeoutController.abort();
  }, REQUEST_TIMEOUT_MS);
  try {
    return await fetch(input, { ...init, signal: timeoutController.signal });
  } catch (error: unknown) {
    if (upstreamSignal?.aborted) throw error;
    if (timedOut) {
      throw new ServerRequestError(
        "unavailable",
        `The world host did not respond within ${REQUEST_TIMEOUT_MS / 1_000} seconds.`,
      );
    }
    throw new ServerRequestError("unavailable", "The world host could not be reached.");
  } finally {
    clearTimeout(timeout);
    upstreamSignal?.removeEventListener("abort", abortFromUpstream);
  }
}

export function formatSimulationHour(simulatedHours: bigint): string {
  return `Hour ${simulatedHours.toLocaleString("en-US")}`;
}
