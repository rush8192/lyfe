#!/usr/bin/env python3
"""Exploratory passive-spread calibration for LYFE's v1 spatial plan.

This is an authoring model, not runtime implementation code. It mirrors the
planned 256-direction/256-magnitude Brownian-like random walk closely enough to
compare environmental-spread profiles under common random numbers.
"""

from __future__ import annotations

import argparse
import math
import random
import statistics
from collections import defaultdict
from dataclasses import dataclass


R0 = 1.0 / 1024.0
FOUNDER_RMS = 2.0 * R0
CONTACT_RANGE = 2.5 * R0
REMNANT_RANGE = 3.0 * R0
CLUSTER_RADIUS = 16.0 * R0
HOURS_PER_DAY = 24

PROFILES = {
    "anchored": 0.25,
    "baseline": 1.00,
    "drifting": 1.50,
}


def build_direction_table() -> list[tuple[float, float]]:
    return [
        (math.cos(2.0 * math.pi * i / 256), math.sin(2.0 * math.pi * i / 256))
        for i in range(256)
    ]


def build_magnitude_table() -> list[float]:
    # Midpoint Rayleigh quantiles approximate the radial magnitude of a 2-D
    # Gaussian. Normalize the finite table back to the exact desired RMS.
    sigma = FOUNDER_RMS / math.sqrt(2.0)
    values = [
        sigma * math.sqrt(-2.0 * math.log(1.0 - (i + 0.5) / 256.0))
        for i in range(256)
    ]
    discrete_rms = math.sqrt(sum(value * value for value in values) / len(values))
    scale = FOUNDER_RMS / discrete_rms
    return [value * scale for value in values]


DIRECTIONS = build_direction_table()
MAGNITUDES = build_magnitude_table()


@dataclass
class TrialResult:
    boundary_intersections_per_day: float
    contact_pair_snapshots_per_day: float
    contact_episodes: int
    unique_contact_pairs: int
    repeat_snapshots_per_contact_pair: float
    mean_remnant_contacts_per_hour: float
    final_rms_radius_r0: float
    final_within_initial_cluster_fraction: float


def reflect(value: float) -> tuple[float, int]:
    intersections = 0
    while value < 0.0 or value >= 1.0:
        intersections += 1
        value = -value if value < 0.0 else 2.0 - value
    return value, intersections


def initial_positions(rng: random.Random, population: int, scenario: str) -> list[list[float]]:
    if scenario == "uniform":
        return [[rng.random(), rng.random()] for _ in range(population)]

    positions: list[list[float]] = []
    for _ in range(population):
        radius = CLUSTER_RADIUS * math.sqrt(rng.random())
        angle = 2.0 * math.pi * rng.random()
        positions.append([0.5 + radius * math.cos(angle), 0.5 + radius * math.sin(angle)])
    return positions


def contact_pairs(positions: list[list[float]]) -> set[int]:
    bins: dict[tuple[int, int], list[int]] = defaultdict(list)
    pairs: set[int] = set()
    range_squared = CONTACT_RANGE * CONTACT_RANGE

    for i, (x, y) in enumerate(positions):
        bx = int(x / CONTACT_RANGE)
        by = int(y / CONTACT_RANGE)
        for nx in range(bx - 1, bx + 2):
            for ny in range(by - 1, by + 2):
                for j in bins.get((nx, ny), ()):
                    dx = x - positions[j][0]
                    dy = y - positions[j][1]
                    if dx * dx + dy * dy <= range_squared:
                        pairs.add(j * len(positions) + i)
        bins[(bx, by)].append(i)
    return pairs


def run_trial(
    seed: int,
    profile_multiplier: float,
    population: int,
    days: int,
    scenario: str,
) -> TrialResult:
    # Reusing seed/scenario across profiles gives common direction and magnitude
    # draws, reducing noise in comparisons.
    rng = random.Random((seed << 1) ^ (0 if scenario == "uniform" else 1))
    positions = initial_positions(rng, population, scenario)
    hours = days * HOURS_PER_DAY

    boundary_intersections = 0
    contact_pair_snapshots = 0
    contact_episodes = 0
    unique_pairs: set[int] = set()
    active_pairs: set[int] = set()
    remnant_contact_organism_hours = 0

    for _ in range(hours):
        for position in positions:
            ux, uy = DIRECTIONS[rng.randrange(256)]
            magnitude = MAGNITUDES[rng.randrange(256)] * profile_multiplier
            position[0], x_crossings = reflect(position[0] + ux * magnitude)
            position[1], y_crossings = reflect(position[1] + uy * magnitude)
            boundary_intersections += x_crossings + y_crossings

        current_pairs = contact_pairs(positions)
        contact_pair_snapshots += len(current_pairs)
        contact_episodes += len(current_pairs - active_pairs)
        unique_pairs.update(current_pairs)
        active_pairs = current_pairs

        remnant_range_squared = REMNANT_RANGE * REMNANT_RANGE
        remnant_contact_organism_hours += sum(
            1
            for x, y in positions
            if (x - 0.5) ** 2 + (y - 0.5) ** 2 <= remnant_range_squared
        )

    radii_squared = [(x - 0.5) ** 2 + (y - 0.5) ** 2 for x, y in positions]
    final_rms_radius_r0 = math.sqrt(sum(radii_squared) / population) / R0
    final_within = sum(radius_sq <= CLUSTER_RADIUS**2 for radius_sq in radii_squared) / population

    return TrialResult(
        boundary_intersections_per_day=boundary_intersections / days,
        contact_pair_snapshots_per_day=contact_pair_snapshots / days,
        contact_episodes=contact_episodes,
        unique_contact_pairs=len(unique_pairs),
        repeat_snapshots_per_contact_pair=(
            contact_pair_snapshots / len(unique_pairs) if unique_pairs else 0.0
        ),
        mean_remnant_contacts_per_hour=remnant_contact_organism_hours / hours,
        final_rms_radius_r0=final_rms_radius_r0,
        final_within_initial_cluster_fraction=final_within,
    )


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    rank = fraction * (len(ordered) - 1)
    low = int(math.floor(rank))
    high = int(math.ceil(rank))
    if low == high:
        return ordered[low]
    return ordered[low] + (ordered[high] - ordered[low]) * (rank - low)


def summarize(results: list[TrialResult], field: str) -> tuple[float, float, float]:
    values = [float(getattr(result, field)) for result in results]
    return percentile(values, 0.1), statistics.median(values), percentile(values, 0.9)


def fmt(summary: tuple[float, float, float], digits: int = 2) -> str:
    low, median, high = summary
    return f"{median:.{digits}f} ({low:.{digits}f}..{high:.{digits}f})"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=64)
    parser.add_argument("--days", type=int, default=30)
    parser.add_argument("--population", type=int, default=100)
    parser.add_argument("--include-anchor-sensitivity", action="store_true")
    args = parser.parse_args()

    fields = [
        ("boundary_intersections_per_day", "boundary/day"),
        ("contact_pair_snapshots_per_day", "contact snapshots/day"),
        ("contact_episodes", "contact episodes"),
        ("unique_contact_pairs", "unique contact pairs"),
        ("repeat_snapshots_per_contact_pair", "snapshots/contacted pair"),
        ("mean_remnant_contacts_per_hour", "center-remnant contacts/hour"),
        ("final_rms_radius_r0", "final RMS radius R0"),
        ("final_within_initial_cluster_fraction", "final fraction within 16 R0"),
    ]

    print(
        f"seeds={args.seeds} days={args.days} population={args.population} "
        f"magnitude_table_rms={math.sqrt(sum(x*x for x in MAGNITUDES)/256)/R0:.6f} R0 "
        f"magnitude_table_max={max(MAGNITUDES)/R0:.6f} R0"
    )
    for scenario in ("uniform", "clustered"):
        print(f"\n[{scenario}]")
        print("profile | " + " | ".join(label for _, label in fields))
        print("--- | " + " | ".join("---" for _ in fields))
        profiles = list(PROFILES.items())
        if args.include_anchor_sensitivity:
            profiles.insert(1, ("anchored_0.50_sensitivity", 0.50))
        for profile, multiplier in profiles:
            results = [
                run_trial(seed, multiplier, args.population, args.days, scenario)
                for seed in range(args.seeds)
            ]
            summaries = [fmt(summarize(results, field), 3) for field, _ in fields]
            print(profile + " | " + " | ".join(summaries))


if __name__ == "__main__":
    main()
