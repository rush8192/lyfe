import { create } from "@bufbuild/protobuf";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { WorldCatalogueSchema } from "../generated/lyfe/v1/persistence_pb";
import { WorldPersistencePanel } from "./WorldPersistencePanel";

describe("world persistence panel", () => {
  it("labels exact boundaries and unsaved active state without implying continuous autosave", () => {
    const html = renderToStaticMarkup(
      <WorldPersistencePanel
        catalogue={create(WorldCatalogueSchema, {
          hasActiveWorld: true,
          activeWorldId: 7n,
          activeWorldRevision: 12n,
          activeHasUnsavedChanges: true,
          savedWorlds: [{
            worldId: 7n,
            completedTick: 9n,
            worldRevision: 10n,
            simulatedHours: 9n,
            organismCount: 123,
            rootSeedHex: "00112233445566778899aabbccddeeff",
            savedAtUnixMilliseconds: 1_700_000_000_000n,
            isActive: true,
          }],
        })}
        activeWorldRunning={false}
        busy={false}
        error={null}
        onSave={() => undefined}
        onLoad={() => undefined}
        onUnload={() => undefined}
      />,
    );

    expect(html).toContain("Unsaved changes");
    expect(html).toContain("Save current boundary");
    expect(html).toContain("Restore save");
    expect(html).toContain("Hour 9");
    expect(html).toContain("123 organisms");
    expect(html).toContain("random continuation");
    expect(html).not.toContain("autosave");
  });

  it("offers saved worlds while no world is active", () => {
    const html = renderToStaticMarkup(
      <WorldPersistencePanel
        catalogue={create(WorldCatalogueSchema, {
          savedWorlds: [{
            worldId: 2n,
            simulatedHours: 48n,
            organismCount: 80,
            rootSeedHex: "ffeeddccbbaa99887766554433221100",
            savedAtUnixMilliseconds: 1_700_000_000_000n,
          }],
        })}
        activeWorldRunning={false}
        busy={false}
        error={null}
        onSave={() => undefined}
        onLoad={() => undefined}
        onUnload={() => undefined}
      />,
    );

    expect(html).toContain("Load world");
    expect(html).not.toContain("Save current boundary");
  });
});
