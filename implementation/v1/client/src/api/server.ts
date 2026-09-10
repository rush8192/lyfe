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
import { createProjectionCache, type ProjectionCache } from "../state/projectionCache";
import { validateSpeciationProposal } from "../state/evolutionPlanner";

export type ServerConnection =
  | { readonly status: "loading" }
  | { readonly status: "offline"; readonly message: string }
  | {
      readonly status: "online";
      readonly cache: ProjectionCache;
      readonly evolution: EvolutionDecisionSurface;
    };

export async function readActiveWorldState(signal: AbortSignal) {
  const [cache, evolution] = await Promise.all([
    readActiveWorldProjection(signal),
    readActiveEvolutionDecisionSurface(signal),
  ]);
  if (
    cache.world.worldId !== evolution.worldId ||
    cache.world.worldRevision !== evolution.worldRevision ||
    cache.world.controlledSpeciesId !== evolution.controlledSpeciesId
  ) {
    throw new Error("World and evolution projections were captured at different boundaries.");
  }
  return { cache, evolution };
}

export async function readActiveWorldProjection(
  signal: AbortSignal,
): Promise<ProjectionCache> {
  const response = await fetch("/api/v1/worlds/active/projection", {
    headers: { Accept: "application/x-protobuf" },
    signal,
  });

  if (!response.ok) {
    throw new Error(`Server returned HTTP ${response.status}.`);
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.startsWith("application/x-protobuf")) {
    throw new Error(`Server returned unexpected content type ${contentType}.`);
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
  const response = await fetch(url, {
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
  const response = await fetch(url, {
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
    throw new Error(`Server returned HTTP ${response.status}.${detail}`);
  }
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.startsWith("application/x-protobuf")) {
    throw new Error(`Server returned unexpected content type ${contentType}.`);
  }
  return fromBinary(schema, new Uint8Array(await response.arrayBuffer()));
}

export function formatSimulationHour(simulatedHours: bigint): string {
  return `Hour ${simulatedHours.toLocaleString("en-US")}`;
}
