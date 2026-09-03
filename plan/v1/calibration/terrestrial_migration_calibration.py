#!/usr/bin/env python3
"""Authoring calibration for LYFE's v1 migration composition.

This is not authoritative engine code. It combines the Brownian boundary model
with the proposed fixed migration probabilities and prints reproducible
landfall/expansion landmarks for the planning documents.
"""

from __future__ import annotations

import argparse
import random
import statistics

from environmental_spread_sim import DIRECTIONS, MAGNITUDES


PROFILES = {
    "anchored": 0.25,
    "baseline": 1.00,
    "drifting": 1.50,
}

PASSIVE_BASE = 0.10
ACTIVE_BASE = 0.80
SHORE_TRANSITION = 0.75
RISKY_FLOOR = 0.02
CENTER_TO_EDGE_R0 = 512.0


def activity(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= preferred:
        return 1.0
    if moisture >= hard:
        progress = (moisture - hard) / (preferred - hard)
        return 0.25 + 0.75 * progress
    if hard > 0.0:
        return 0.25 * moisture / hard
    return 0.0


def probability(base: float, transition: float, compatibility: float) -> float:
    return base * transition * max(RISKY_FLOOR, min(1.0, compatibility))


def expected_active_hours(
    maximum_speed: float,
    acceleration: float,
    activity_factor: float,
    success_probability: float,
) -> float:
    travel = 0
    speed = 0.0
    distance = 0.0
    while distance < CENTER_TO_EDGE_R0:
        speed = min(maximum_speed, speed + acceleration)
        distance += speed * activity_factor
        travel += 1
    post_arrival_failures = 1.0 / success_probability - 1.0
    return travel + post_arrival_failures


def median_boundary_rate(
    seeds: int, days: int, population: int, multiplier: float
) -> float:
    rates: list[float] = []
    for seed in range(seeds):
        rng = random.Random(seed << 1)
        positions = [[rng.random(), rng.random()] for _ in range(population)]
        intersections = 0
        for _ in range(days * 24):
            for position in positions:
                ux, uy = DIRECTIONS[rng.randrange(256)]
                magnitude = MAGNITUDES[rng.randrange(256)] * multiplier
                for axis, component in ((0, ux), (1, uy)):
                    value = position[axis] + component * magnitude
                    while value < 0.0 or value >= 1.0:
                        intersections += 1
                        value = -value if value < 0.0 else 2.0 - value
                    position[axis] = value
        rates.append(intersections / days)
    return statistics.median(rates)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=64)
    parser.add_argument("--days", type=int, default=30)
    parser.add_argument("--population", type=int, default=100)
    args = parser.parse_args()

    print(
        f"seeds={args.seeds} days={args.days} population={args.population} "
        f"passive={PASSIVE_BASE:.2f} active={ACTIVE_BASE:.2f} "
        f"shore={SHORE_TRANSITION:.2f} floor={RISKY_FLOOR:.2f}"
    )

    print("\n[probability anchors]")
    print("destination | activity/compatibility | passive shore | active shore")
    print("--- | ---: | ---: | ---:")
    cases = [
        ("wet-surface at 0.85", activity(0.85, 0.70, 0.30)),
        ("wet-surface at 0.40", activity(0.40, 0.70, 0.30)),
        ("wet-surface at 0.30", activity(0.30, 0.70, 0.30)),
        ("wet-surface at 0.00", activity(0.00, 0.70, 0.30)),
        ("intermittent tolerance at 0.40", activity(0.40, 0.40, 0.10)),
    ]
    for name, compatibility in cases:
        print(
            f"{name} | {compatibility:.4f} | "
            f"{probability(PASSIVE_BASE, SHORE_TRANSITION, compatibility):.4%} | "
            f"{probability(ACTIVE_BASE, SHORE_TRANSITION, compatibility):.4%}"
        )

    print("\n[uniform passive spread]")
    print(
        "profile | aquatic raw edges/100/day | aquatic success/100/day | "
        "terrestrial raw edges/100/day | terrestrial success/100/day | "
        "one wet shore success/100/day"
    )
    print("--- | ---: | ---: | ---: | ---: | ---:")
    for name, profile in PROFILES.items():
        aquatic_raw = median_boundary_rate(
            args.seeds, args.days, args.population, profile
        )
        terrestrial_raw = median_boundary_rate(
            args.seeds, args.days, args.population, profile * 0.50
        )
        # Uniform geometry assigns one quarter of intersections to one selected edge.
        one_shore = aquatic_raw / 4.0 * PASSIVE_BASE * SHORE_TRANSITION
        print(
            f"{name} | {aquatic_raw:.3f} | {aquatic_raw * PASSIVE_BASE:.3f} | "
            f"{terrestrial_raw:.3f} | {terrestrial_raw * PASSIVE_BASE:.3f} | "
            f"{one_shore:.3f}"
        )

    print("\n[directed active movement from tile center]")
    print("scenario | effective speed R0/hour | probability/attempt | expected hours")
    print("--- | ---: | ---: | ---:")
    active_rows = [
        ("water-to-wet-land, basal motility", 4.0, 2.0, 1.0, ACTIVE_BASE * SHORE_TRANSITION),
        ("wet land-to-land, basal motility", 4.0, 2.0, 1.0, ACTIVE_BASE),
        ("wet land-to-land, surface gliding", 6.0, 3.0, 1.0, ACTIVE_BASE),
    ]
    wet_surface_at_040 = activity(0.40, 0.70, 0.30)
    active_rows.extend(
        [
            (
                "0.40-moisture land, basal wet-surface",
                4.0,
                2.0,
                wet_surface_at_040,
                ACTIVE_BASE * wet_surface_at_040,
            ),
            (
                "0.40-moisture land, gliding wet-surface",
                6.0,
                3.0,
                wet_surface_at_040,
                ACTIVE_BASE * wet_surface_at_040,
            ),
            (
                "0.40-moisture land, intermittent gliding",
                6.0,
                3.0,
                1.0,
                ACTIVE_BASE,
            ),
        ]
    )
    for name, speed, acceleration, activity_factor, chance in active_rows:
        print(
            f"{name} | {speed * activity_factor:.3f} | {chance:.2%} | "
            f"{expected_active_hours(speed, acceleration, activity_factor, chance):.2f}"
        )


if __name__ == "__main__":
    main()
