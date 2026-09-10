const CHRONICLE_NOTES_STORAGE_KEY = "lyfe.v1.chronicle-notes";
const CHRONICLE_NOTES_VERSION = 1;
const MAX_CHRONICLE_NOTES = 256;
export const MAX_CHRONICLE_NOTE_LENGTH = 1_200;

export interface ChronicleNoteStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

interface StoredChronicleNote {
  readonly worldId: string;
  readonly eventId: string;
  readonly text: string;
}

interface ChronicleNoteEnvelope {
  readonly version: number;
  readonly notes: readonly StoredChronicleNote[];
}

export function readWorldChronicleNotes(
  storage: ChronicleNoteStorage,
  worldId: bigint,
): ReadonlyMap<bigint, string> {
  if (worldId <= 0n) return new Map();
  const envelope = readEnvelope(storage);
  if (envelope === null) return new Map();

  const notes = new Map<bigint, string>();
  for (const note of envelope.notes) {
    if (BigInt(note.worldId) === worldId) {
      notes.set(BigInt(note.eventId), note.text);
    }
  }
  return notes;
}

export function writeChronicleNote(
  storage: ChronicleNoteStorage,
  worldId: bigint,
  eventId: bigint,
  text: string,
): void {
  if (worldId <= 0n || eventId <= 0n) {
    throw new Error("Chronicle note identity must be positive.");
  }
  const normalized = text.normalize("NFC");
  if (normalized.length > MAX_CHRONICLE_NOTE_LENGTH) {
    throw new Error("Chronicle note exceeds the local length limit.");
  }

  const envelope = readEnvelope(storage) ?? {
    version: CHRONICLE_NOTES_VERSION,
    notes: [],
  };
  const retained = envelope.notes.filter((note) =>
    note.worldId !== worldId.toString() || note.eventId !== eventId.toString());
  const notes = normalized.trim().length === 0
    ? retained
    : [...retained, {
        worldId: worldId.toString(),
        eventId: eventId.toString(),
        text: normalized,
      }];
  storage.setItem(CHRONICLE_NOTES_STORAGE_KEY, JSON.stringify({
    version: CHRONICLE_NOTES_VERSION,
    notes: notes.slice(-MAX_CHRONICLE_NOTES),
  } satisfies ChronicleNoteEnvelope));
}

function readEnvelope(storage: ChronicleNoteStorage): ChronicleNoteEnvelope | null {
  let value: unknown;
  try {
    const raw = storage.getItem(CHRONICLE_NOTES_STORAGE_KEY);
    if (raw === null) return {
      version: CHRONICLE_NOTES_VERSION,
      notes: [],
    };
    value = JSON.parse(raw);
  } catch {
    return null;
  }

  if (!isRecord(value) || value.version !== CHRONICLE_NOTES_VERSION ||
      !Array.isArray(value.notes) || value.notes.length > MAX_CHRONICLE_NOTES) {
    return null;
  }
  const notes: StoredChronicleNote[] = [];
  const identities = new Set<string>();
  for (const candidate of value.notes) {
    if (!isRecord(candidate) || typeof candidate.worldId !== "string" ||
        typeof candidate.eventId !== "string" || typeof candidate.text !== "string" ||
        candidate.text.length === 0 || candidate.text.length > MAX_CHRONICLE_NOTE_LENGTH ||
        candidate.text !== candidate.text.normalize("NFC") ||
        !isPositiveCanonicalInteger(candidate.worldId) ||
        !isPositiveCanonicalInteger(candidate.eventId)) {
      return null;
    }
    const identity = `${candidate.worldId}:${candidate.eventId}`;
    if (identities.has(identity)) return null;
    identities.add(identity);
    notes.push({
      worldId: candidate.worldId,
      eventId: candidate.eventId,
      text: candidate.text,
    });
  }
  return { version: CHRONICLE_NOTES_VERSION, notes };
}

function isPositiveCanonicalInteger(value: string): boolean {
  if (!/^[1-9][0-9]*$/.test(value)) return false;
  try {
    return BigInt(value) <= ((1n << 64n) - 1n);
  } catch {
    return false;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
