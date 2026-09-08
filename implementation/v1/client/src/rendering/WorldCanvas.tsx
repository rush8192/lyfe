import { Application, Container, Graphics, Text } from "pixi.js";
import { useEffect, useRef } from "react";
import {
  OrganismJourneyEventFamily,
  type ActorWorldProjection,
  type OrganismJourneyEvent,
} from "../generated/lyfe/v1/projection_pb";

const UINT32_SCALE = 0xffff_ffff;

export function WorldCanvas({
  world,
  enabledEventFamilies,
}: {
  readonly world: ActorWorldProjection | null;
  readonly enabledEventFamilies: ReadonlySet<OrganismJourneyEventFamily>;
}) {
  const hostRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const host = hostRef.current;
    if (host === null) {
      return;
    }

    const application = new Application();
    let disposed = false;
    let initialized = false;

    async function initialize(target: HTMLDivElement) {
      await application.init({
        antialias: true,
        backgroundAlpha: 0,
        resizeTo: target,
      });
      initialized = true;

      if (disposed) {
        application.destroy(true);
        return;
      }

      const scene = new Graphics();
      if (world === null) {
        scene
          .roundRect(28, 28, Math.max(80, target.clientWidth - 56), 308, 24)
          .fill({ color: 0x102d35, alpha: 0.78 })
          .stroke({ color: 0x47766f, width: 2, alpha: 0.4 });
      } else {
        drawWorld(scene, world, Math.max(320, target.clientWidth), 430);
      }

      application.stage.addChild(scene);
      if (world !== null) {
        addActivityPulses(
          application,
          world,
          enabledEventFamilies,
          Math.max(320, target.clientWidth),
          430,
        );
      }
      target.appendChild(application.canvas);
    }

    void initialize(host);

    return () => {
      disposed = true;
      if (initialized) {
        application.destroy(true, { children: true });
      }
    };
  }, [world, enabledEventFamilies]);

  const visibleCount = world?.tiles.reduce(
    (total, tile) =>
      total + (tile.detail.case === "live" ? tile.detail.value.organisms.length : 0),
    0,
  );
  return (
    <div
      className="world-canvas"
      ref={hostRef}
      role="img"
      aria-label={
        world === null
          ? "Waiting for the actor-authorized world projection"
          : `World grid containing ${visibleCount ?? 0} visible simulated organisms`
      }
    />
  );
}

function addActivityPulses(
  application: Application,
  world: ActorWorldProjection,
  enabled: ReadonlySet<OrganismJourneyEventFamily>,
  width: number,
  height: number,
) {
  if (world.journeyEvents.length === 0) return;

  const latestTick = world.journeyEvents.at(-1)?.tick ?? 0n;
  const events = world.journeyEvents
    .filter((event) => event.tick === latestTick && enabled.has(event.family))
    .slice(-160);
  const startedAt = performance.now();
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const pulses = events.map((event) => createPulse(event, world, width, height));
  for (const pulse of pulses) application.stage.addChild(pulse.node);

  application.ticker.add(() => {
    const elapsed = performance.now() - startedAt;
    const fadeProgress = Math.max(0, Math.min(1, (elapsed - 400) / 2_600));
    for (const pulse of pulses) {
      pulse.node.alpha = 1 - fadeProgress;
      pulse.node.y = pulse.baseY - (reducedMotion ? 0 : 16 * fadeProgress);
    }
  });
}

function createPulse(
  event: OrganismJourneyEvent,
  world: ActorWorldProjection,
  width: number,
  height: number,
) {
  const padding = 24;
  const tileWidth = (width - padding * 2) / world.width;
  const tileHeight = (height - padding * 2) / world.height;
  const tile = world.tiles.find((candidate) => candidate.tileId === event.tileId);
  const left = padding + (tile?.x ?? 0) * tileWidth;
  const top = padding + (tile?.y ?? 0) * tileHeight;
  const x = left + (event.positionXQ / UINT32_SCALE) * tileWidth;
  const y = top + (event.positionYQ / UINT32_SCALE) * tileHeight;
  const visual = eventVisual(event.family);
  const node = new Container({ x, y });
  const backing = new Graphics()
    .circle(0, 0, 10)
    .fill({ color: 0x071113, alpha: 0.76 })
    .stroke({ color: visual.color, width: 1.5, alpha: 0.92 });
  const glyph = new Text({
    text: visual.glyph,
    style: {
      fill: visual.color,
      fontFamily: "system-ui, sans-serif",
      fontSize: 14,
      fontWeight: "700",
    },
  });
  glyph.anchor.set(0.5);
  node.addChild(backing, glyph);
  return { node, baseY: y };
}

function eventVisual(family: OrganismJourneyEventFamily) {
  switch (family) {
    case OrganismJourneyEventFamily.BIRTH:
      return { glyph: "✦", color: 0x78e6ac };
    case OrganismJourneyEventFamily.REPRODUCTION:
      return { glyph: "+", color: 0x58d99b };
    case OrganismJourneyEventFamily.RESOURCE_ABSORPTION:
      return { glyph: "↟", color: 0x61c9b5 };
    case OrganismJourneyEventFamily.FEEDING:
      return { glyph: "◆", color: 0xa8d887 };
    case OrganismJourneyEventFamily.MIGRATION:
      return { glyph: "→", color: 0xd8b870 };
    case OrganismJourneyEventFamily.DEATH:
      return { glyph: "×", color: 0xed756d };
    default:
      return { glyph: "•", color: 0xc4b987 };
  }
}

function drawWorld(
  scene: Graphics,
  world: ActorWorldProjection,
  width: number,
  height: number,
) {
  const padding = 24;
  const availableWidth = width - padding * 2;
  const availableHeight = height - padding * 2;
  const tileWidth = availableWidth / world.width;
  const tileHeight = availableHeight / world.height;

  for (const tile of world.tiles) {
    const left = padding + tile.x * tileWidth;
    const top = padding + tile.y * tileHeight;
    const color =
      tile.detail.case === "live"
        ? 0x123f52
        : tile.detail.case === "reduced"
          ? 0x243b3d
          : 0x111c1f;
    scene
      .rect(left, top, tileWidth, tileHeight)
      .fill({ color, alpha: 0.94 })
      .stroke({ color: 0x58c4a3, width: 1, alpha: 0.38 });

    if (tile.detail.case !== "live") {
      continue;
    }

    for (const organism of tile.detail.value.organisms) {
      const organismX = left + (organism.positionXQ / UINT32_SCALE) * tileWidth;
      const organismY = top + (organism.positionYQ / UINT32_SCALE) * tileHeight;
      scene
        .circle(organismX, organismY, 2.4)
        .fill({ color: 0x8ff0c8, alpha: 0.88 });
    }
  }
}
