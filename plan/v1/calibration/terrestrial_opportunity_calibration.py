#!/usr/bin/env python3
"""Expected-value authoring fixture for LYFE's first terrestrial niches.

This is not authoritative engine code. It intentionally holds light, temperature,
CO2, nutrients, contention, aging, and predation constant so the land adaptation,
moisture, dormancy, and maintenance rules can be compared in isolation.
"""

from __future__ import annotations

import math


HOURS_PER_DAY = 24
DAYS_PER_YEAR = 360
BASE_MAINTENANCE = 50
IDEAL_OXYGENIC_SUCCESS = 0.90
OXYGENIC_REQUESTS_PER_LIGHT = 1_500
LIGHT_SATURATION = 0.75
CLOUD_FACTOR = 0.94
MOISTURE_DEATH_AT_HARD = 0.001
MOISTURE_DEATH_CAP = 0.25
DORMANCY_ENTRY_WARNING = 0.35
DORMANCY_EMERGENCY_ENTRY = 0.30
DORMANCY_EXIT_RECOVERY = 0.45
DORMANCY_TREND_WINDOW_HOURS = 24
DORMANCY_MINIMUM_DWELL_HOURS = 24


def surface_light(hour: int) -> float:
    local_fraction = ((hour % HOURS_PER_DAY) + 0.5) / HOURS_PER_DAY
    hour_angle = 2.0 * math.pi * (local_fraction - 0.5)
    return max(0.0, math.cos(hour_angle)) * CLOUD_FACTOR


def gross_oxygenic_energy(hour: int, activity: float) -> float:
    # Mirrors the existing deterministic authoring estimate: the executable
    # engine replaces this with fixed-point request construction and a keyed
    # binomial draw across named seeds.
    return math.floor(
        OXYGENIC_REQUESTS_PER_LIGHT
        * min(surface_light(hour), LIGHT_SATURATION)
        * activity
        * IDEAL_OXYGENIC_SUCCESS
    )


def moisture_at_hour(hour: int) -> float:
    # Smooth deterministic seasonal-interior isolation curve. Generated-world
    # validation will replace this with the actual hourly moisture recurrence.
    year_fraction = (hour + 0.5) / (HOURS_PER_DAY * DAYS_PER_YEAR)
    return 0.50 + 0.35 * math.cos(2.0 * math.pi * year_fraction)


def activity(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= preferred:
        return 1.0
    if moisture >= hard:
        progress = (moisture - hard) / (preferred - hard)
        return 0.25 + 0.75 * progress
    return 0.25 * moisture / hard if hard > 0.0 else 0.0


def severity(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= preferred:
        return 0.0
    return min(1.0, (preferred - moisture) / (preferred - hard))


def stress_cost(moisture: float, preferred: float, hard: float) -> int:
    return math.ceil(BASE_MAINTENANCE * severity(moisture, preferred, hard) ** 2)


def death_chance(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= hard:
        return 0.0
    overage = (hard - moisture) / (preferred - hard)
    return min(MOISTURE_DEATH_CAP, MOISTURE_DEATH_AT_HARD * (1.0 + overage) ** 2)


def maximum_drawdown(hourly_net: list[float]) -> float:
    cumulative = 0.0
    prior_peak = 0.0
    drawdown = 0.0
    for value in hourly_net:
        cumulative += value
        prior_peak = max(prior_peak, cumulative)
        drawdown = max(drawdown, prior_peak - cumulative)
    return drawdown


def recent_moisture_trend(history: list[float]) -> int:
    window = DORMANCY_TREND_WINDOW_HOURS
    prior = sum(history[-2 * window : -window]) / window
    recent = sum(history[-window:]) / window
    return -1 if recent < prior else 1 if recent > prior else 0


def evaluate(
    name: str,
    active_thresholds: tuple[float, float],
    active_upkeep: int,
    structure_target: int,
    dormancy: bool = False,
) -> dict[str, float]:
    active_preferred, active_hard = active_thresholds
    dormant_preferred, dormant_hard = 0.20, 0.05
    dormant_upkeep = 30
    transition_cost = 0
    is_dormant = False
    phase_entered_hour = -DORMANCY_MINIMUM_DWELL_HOURS
    history = [moisture_at_hour(hour) for hour in range(-48, 0)]
    survival = 1.0
    active_hours = 0
    below_active_hard_hours = 0
    hourly_net: list[float] = []

    for hour in range(HOURS_PER_DAY * DAYS_PER_YEAR):
        moisture = moisture_at_hour(hour)
        history.append(moisture)
        transition_cost = 0

        if moisture < active_hard:
            below_active_hard_hours += 1

        if is_dormant:
            gross = 0.0
            upkeep = dormant_upkeep
            stress = stress_cost(moisture, dormant_preferred, dormant_hard)
            death = death_chance(moisture, dormant_preferred, dormant_hard)
        else:
            active_hours += 1
            gross = gross_oxygenic_energy(
                hour, activity(moisture, active_preferred, active_hard)
            )
            upkeep = active_upkeep
            stress = stress_cost(moisture, active_preferred, active_hard)
            death = death_chance(moisture, active_preferred, active_hard)

        if dormancy and hour - phase_entered_hour >= DORMANCY_MINIMUM_DWELL_HOURS:
            trend = recent_moisture_trend(history)
            if not is_dormant and (
                moisture <= DORMANCY_EMERGENCY_ENTRY
                or (moisture <= DORMANCY_ENTRY_WARNING and trend < 0)
            ):
                is_dormant = True
                phase_entered_hour = hour
                transition_cost = 500
            elif (
                is_dormant
                and moisture >= DORMANCY_EXIT_RECOVERY
                and trend > 0
            ):
                is_dormant = False
                phase_entered_hour = hour
                transition_cost = 250

        hourly_net.append(gross - upkeep - stress - transition_cost)
        survival *= 1.0 - death

    # Base replacement capital is 100 energy per structure plus 4,000 result
    # reserve and 500 reproductive work. This is an opportunity landmark, not
    # a complete reproduction simulation.
    replacement_capital = structure_target * 100 + 4_500
    annual_net = sum(hourly_net)
    return {
        "annual_net": annual_net,
        "drawdown": maximum_drawdown(hourly_net),
        "survival": survival,
        "active_days": active_hours / HOURS_PER_DAY,
        "below_hard_days": below_active_hard_hours / HOURS_PER_DAY,
        "replacement_capital": replacement_capital,
        "replacement_equivalents": annual_net / replacement_capital,
    }


def main() -> None:
    surface_gross = sum(gross_oxygenic_energy(hour, 1.0) for hour in range(24))
    print(
        "fixture: 360 days, moisture=0.50+0.35*cos(year), "
        "surface cloud=0.10, non-limiting resources"
    )
    print(f"ideal surface gross/day={surface_gross:.1f}")
    print(
        "observable dormancy: enter at <=0.35 while trailing moisture falls "
        "(or <=0.30 emergency), exit at >=0.45 while it rises; 24 h windows "
        "and dwell; 30 energy/hour dormant upkeep; 500/250 transition costs"
    )

    profiles = [
        evaluate("wet-surface, always active", (0.70, 0.30), 100, 1_100),
        evaluate("intermittent tolerance, active", (0.40, 0.10), 115, 1_265),
        evaluate(
            "wet-surface plus ordinary dormancy",
            (0.70, 0.30),
            100,
            1_100,
            dormancy=True,
        ),
    ]
    names = [
        "wet-surface, always active",
        "intermittent tolerance, active",
        "wet-surface plus ordinary dormancy",
    ]

    print("\n[seasonal interior expected-value comparison]")
    print(
        "profile | active days | days below active hard | annual net energy | "
        "max reserve draw | moisture survival | capital equivalents"
    )
    print("--- | ---: | ---: | ---: | ---: | ---: | ---:")
    for name, result in zip(names, profiles, strict=True):
        print(
            f"{name} | {result['active_days']:.1f} | "
            f"{result['below_hard_days']:.1f} | {result['annual_net']:.0f} | "
            f"{result['drawdown']:.0f} | {result['survival']:.4%} | "
            f"{result['replacement_equivalents']:.2f}"
        )

    print("\n[permanently wet expected-value comparison]")
    for name, upkeep, structure in (
        ("wet-surface", 100, 1_100),
        ("intermittent tolerance", 115, 1_265),
    ):
        daily_net = surface_gross - upkeep * HOURS_PER_DAY
        capital = structure * 100 + 4_500
        print(
            f"{name}: net/day={daily_net:.0f}, capital={capital}, "
            f"nominal division={capital / daily_net:.2f} days"
        )


if __name__ == "__main__":
    main()
