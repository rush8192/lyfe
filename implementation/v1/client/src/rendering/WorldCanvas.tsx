import { Application, Graphics } from "pixi.js";
import { useEffect, useRef } from "react";
import type { ActorWorldProjection } from "../generated/lyfe/v1/projection_pb";

const UINT32_SCALE = 0xffff_ffff;

export function WorldCanvas({
  world,
}: {
  readonly world: ActorWorldProjection | null;
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
      target.appendChild(application.canvas);
    }

    void initialize(host);

    return () => {
      disposed = true;
      if (initialized) {
        application.destroy(true, { children: true });
      }
    };
  }, [world]);

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
