import { fromBinary } from "@bufbuild/protobuf";
import {
  ProjectionSnapshotSchema,
} from "../generated/lyfe/v1/projection_pb";
import { createProjectionCache, type ProjectionCache } from "../state/projectionCache";

export type ServerConnection =
  | { readonly status: "loading" }
  | { readonly status: "offline"; readonly message: string }
  | { readonly status: "online"; readonly cache: ProjectionCache };

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

export function formatSimulationHour(simulatedHours: bigint): string {
  return `Hour ${simulatedHours.toLocaleString("en-US")}`;
}
