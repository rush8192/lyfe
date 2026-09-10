import { describe, expect, it } from "vitest";
import {
  MAX_CHRONICLE_NOTE_LENGTH,
  readWorldChronicleNotes,
  writeChronicleNote,
  type ChronicleNoteStorage,
} from "./chronicleNotes";

describe("chronicle notes", () => {
  it("keeps notes local to their world and authoritative event", () => {
    const storage = memoryStorage();

    writeChronicleNote(storage, 1n, 7n, "Pressure eased after the split.");
    writeChronicleNote(storage, 1n, 8n, "Watch the next resource interval.");
    writeChronicleNote(storage, 2n, 7n, "A different world.");

    expect([...readWorldChronicleNotes(storage, 1n)]).toEqual([
      [7n, "Pressure eased after the split."],
      [8n, "Watch the next resource interval."],
    ]);
    expect([...readWorldChronicleNotes(storage, 2n)]).toEqual([
      [7n, "A different world."],
    ]);
  });

  it("normalizes updates and removes a note when only whitespace remains", () => {
    const storage = memoryStorage();
    writeChronicleNote(storage, 1n, 7n, "Cafe\u0301");
    expect(readWorldChronicleNotes(storage, 1n).get(7n)).toBe("Café");

    writeChronicleNote(storage, 1n, 7n, "   ");
    expect(readWorldChronicleNotes(storage, 1n).has(7n)).toBe(false);
  });

  it("fails closed for malformed state and rejects unbounded text", () => {
    const storage = memoryStorage();
    storage.setItem("lyfe.v1.chronicle-notes", JSON.stringify({
      version: 1,
      notes: [{ worldId: "01", eventId: "7", text: "Not canonical" }],
    }));
    expect(readWorldChronicleNotes(storage, 1n).size).toBe(0);
    expect(() => writeChronicleNote(
      memoryStorage(),
      1n,
      7n,
      "x".repeat(MAX_CHRONICLE_NOTE_LENGTH + 1),
    )).toThrow(/length limit/);
  });
});

function memoryStorage(): ChronicleNoteStorage {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
}
