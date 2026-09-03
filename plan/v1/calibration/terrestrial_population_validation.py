#!/usr/bin/env python3
"""Individual-based authoring fixture for terrestrial dormancy and storage.

This is executable design validation, not authoritative engine code. It uses
the selected hourly phase order, moisture and senescence death curves, reserve
capacity, structure assembly, deterministic reproduction with keyed cooldown
jitter, a finite CO2 account, and a deliberately information-bounded dormancy
policy. It omits micronutrient acquisition, spatial migration, predation,
remnant decay, and the server's exact fixed-point/RNG implementation.
"""

from __future__ import annotations

from dataclasses import dataclass, replace
import hashlib
import math
import statistics


HOURS_PER_DAY = 24
DAYS_PER_YEAR = 360
HOURS_PER_YEAR = HOURS_PER_DAY * DAYS_PER_YEAR
BASE_MAINTENANCE = 50
ASSEMBLY_ENERGY = 100
REPRODUCTION_WORK = 500
REPRODUCTION_HEALTH = 0.60
RESULT_RESERVE_MINIMUM = 4_000
GROWTH_FLOOR_FRACTION = 0.40
CO2_SOURCE_PER_HOUR = 20_004
INITIAL_CO2_HOURS = 24
DORMANCY_ENTRY_LEAD = 0.05
DORMANCY_EXIT_HYSTERESIS = 0.10
DORMANCY_TREND_WINDOW_HOURS = 24
DORMANCY_MINIMUM_DWELL_HOURS = 24
DORMANCY_ENTRY_RESERVE_HORIZON_HOURS = 24


@dataclass(frozen=True)
class Phenotype:
    name: str
    capacity: int
    mature_structure: int
    active_upkeep: int
    dormant_upkeep: int
    active_preferred: float
    active_hard: float
    assembly_ceiling: int
    can_dorm: bool = False
    dormant_age_multiplier: float = 0.10


WET_SURFACE = Phenotype(
    "WetSurface", 10_000, 1_100, 100, 30, 0.70, 0.30, 4
)
INTERMITTENT = Phenotype(
    "IntermittentTolerance", 10_000, 1_265, 115, 33, 0.40, 0.10, 4
)
WET_DORMANT = replace(WET_SURFACE, name="WetSurfaceDormant", can_dorm=True)
COMPARTMENTALIZED_DORMANT = Phenotype(
    "CompartmentalizedReserveDormant",
    150_000,
    1_935,
    118,
    34,
    0.70,
    0.30,
    6,
    True,
)


@dataclass
class Organism:
    organism_id: int
    biological_age: float
    reserve: int
    structure: int
    dormant: bool
    phase_entered_tick: int
    reproduction_not_before_tick: int
    successful_reproduction_count: int = 0


@dataclass(frozen=True)
class Scenario:
    name: str
    phenotype: Phenotype
    climate: str
    dormancy_policy: str
    years: int = 3
    founders: int = 12
    start_hour: int = 0
    duration_hours: int | None = None
    initial_co2_hours: int = INITIAL_CO2_HOURS
    reproduction_enabled: bool = True
    growth_enabled: bool = True


@dataclass
class Result:
    final_population: int
    peak_population: int
    births: int
    moisture_deaths: int
    senescence_deaths: int
    maintenance_deaths: int
    multi_trigger_deaths: int
    dormancy_entries: int
    dormancy_exits: int
    co2_consumed: int
    co2_remaining: int
    carbon_error: int


def keyed_u64(seed: int, *parts: object) -> int:
    payload = ":".join(str(part) for part in (seed, *parts)).encode("utf-8")
    return int.from_bytes(hashlib.blake2b(payload, digest_size=8).digest(), "big")


def keyed_uniform(seed: int, *parts: object) -> float:
    return keyed_u64(seed, *parts) / 2**64


def keyed_integer(seed: int, low: int, high: int, *parts: object) -> int:
    return low + keyed_u64(seed, *parts) % (high - low + 1)


def moisture_at_hour(hour: int, climate: str) -> float:
    if climate == "permanent_wet":
        return 0.85
    if climate != "seasonal":
        raise ValueError(climate)
    year_fraction = (hour + 0.5) / HOURS_PER_YEAR
    return 0.50 + 0.35 * math.cos(2.0 * math.pi * year_fraction)


def surface_light(hour: int) -> float:
    local_fraction = ((hour % HOURS_PER_DAY) + 0.5) / HOURS_PER_DAY
    hour_angle = 2.0 * math.pi * (local_fraction - 0.5)
    return max(0.0, math.cos(hour_angle)) * 0.94


def terrestrial_activity(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= preferred:
        return 1.0
    if moisture >= hard:
        progress = (moisture - hard) / (preferred - hard)
        return 0.25 + 0.75 * progress
    return 0.25 * moisture / hard if hard > 0 else 0.0


def stress_cost(moisture: float, preferred: float, hard: float) -> int:
    if moisture >= preferred:
        return 0
    severity = min(1.0, (preferred - moisture) / (preferred - hard))
    return math.ceil(BASE_MAINTENANCE * severity * severity)


def environmental_health(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= preferred:
        return 1.0
    if moisture >= hard:
        severity = (preferred - moisture) / (preferred - hard)
        return 1.0 - 0.25 * severity * severity
    overage = (hard - moisture) / (preferred - hard)
    return max(0.10, 0.75 / (1.0 + overage) ** 2)


def moisture_death_probability(moisture: float, preferred: float, hard: float) -> float:
    if moisture >= hard:
        return 0.0
    overage = (hard - moisture) / (preferred - hard)
    return min(0.25, 0.001 * (1.0 + overage) ** 2)


def age_factor(age: float) -> float:
    if age < 720:
        return 1.0
    decline = min(1.0, (age - 720.0) / 720.0)
    return 1.0 - 0.5 * decline * decline


def senescence_probability(age: float) -> float:
    if age < 720:
        return 0.0
    excess = age - 720.0
    return min(0.50, 0.0001 * (1.0 + excess / 168.0) ** 2)


def age_throughput(age: float) -> float:
    return 0.5 + 0.5 * age_factor(age)


def cooldown_tick(seed: int, organism: Organism, completed_tick: int) -> int:
    jitter = keyed_integer(
        seed,
        0,
        3,
        "reproduction-cooldown",
        organism.organism_id,
        organism.successful_reproduction_count,
    )
    return completed_tick + 24 + jitter


def recent_trend(history: list[float]) -> int:
    """Return -1/0/+1 from two trailing 24-hour observed means."""
    window = DORMANCY_TREND_WINDOW_HOURS
    if len(history) < 2 * window:
        return 0
    prior = sum(history[-2 * window : -window]) / window
    recent = sum(history[-window:]) / window
    epsilon = 1e-9
    return -1 if recent < prior - epsilon else 1 if recent > prior + epsilon else 0


def desired_dormancy(
    organism: Organism,
    phenotype: Phenotype,
    policy: str,
    tick: int,
    moisture: float,
    history: list[float],
    climate: str,
) -> bool:
    if not phenotype.can_dorm or policy == "never":
        return False
    if tick - organism.phase_entered_tick < DORMANCY_MINIMUM_DWELL_HOURS:
        return organism.dormant

    trend = recent_trend(history)
    if policy == "observable":
        emergency_entry = phenotype.active_hard
        warning_entry = min(
            phenotype.active_preferred,
            emergency_entry + DORMANCY_ENTRY_LEAD,
        )
        recovery_exit = min(
            phenotype.active_preferred,
            warning_entry + DORMANCY_EXIT_HYSTERESIS,
        )
        if not organism.dormant:
            wants_entry = moisture <= emergency_entry or (
                moisture <= warning_entry and trend < 0
            )
            current_dormant_cost = phenotype.dormant_upkeep + stress_cost(
                moisture, 0.20, 0.05
            )
            can_pay = organism.reserve >= 500 + (
                DORMANCY_ENTRY_RESERVE_HORIZON_HOURS * current_dormant_cost
            )
            return wants_entry and can_pay
        wants_exit = moisture >= recovery_exit and trend > 0
        next_active_cost = phenotype.active_upkeep + stress_cost(
            moisture, phenotype.active_preferred, phenotype.active_hard
        )
        can_pay = organism.reserve >= 250 + next_active_cost
        return not (wants_exit and can_pay)

    if policy == "current_hard":
        return moisture < phenotype.active_hard

    if policy == "hindsight":
        # Deliberately invalid runtime policy: it sees the following tick's
        # moisture and establishes the comparison ceiling used by the old
        # isolated oracle calculation.
        return moisture_at_hour(tick + 1, climate) < phenotype.active_hard

    raise ValueError(policy)


def proportional_grants(
    requests: dict[int, int], available: int, seed: int, tick: int
) -> dict[int, int]:
    total = sum(requests.values())
    if total <= available:
        return dict(requests)
    grants: dict[int, int] = {}
    residual_rows: list[tuple[int, int, int]] = []
    granted = 0
    for organism_id, request in requests.items():
        numerator = available * request
        base, remainder = divmod(numerator, total)
        grants[organism_id] = base
        granted += base
        residual_rows.append(
            (
                -remainder,
                keyed_u64(seed, "co2-residual", tick, organism_id),
                organism_id,
            )
        )
    residual_rows.sort()
    for _, _, organism_id in residual_rows[: available - granted]:
        grants[organism_id] += 1
    return grants


def run_scenario(scenario: Scenario, seed: int) -> Result:
    phenotype = scenario.phenotype
    organisms: dict[int, Organism] = {}
    next_id = 1
    for _ in range(scenario.founders):
        organism = Organism(
            next_id,
            biological_age=0.0,
            reserve=phenotype.capacity,
            structure=phenotype.mature_structure,
            dormant=False,
            phase_entered_tick=scenario.start_hour - 24,
            reproduction_not_before_tick=0,
        )
        organism.reproduction_not_before_tick = cooldown_tick(
            seed, organism, scenario.start_hour
        )
        organisms[next_id] = organism
        next_id += 1

    co2 = CO2_SOURCE_PER_HOUR * scenario.initial_co2_hours
    initial_carbon = co2 + sum(
        organism.reserve + organism.structure * ASSEMBLY_ENERGY
        for organism in organisms.values()
    )
    boundary_carbon = 0
    waste_carbon = 0
    remnant_carbon = 0
    co2_consumed = 0
    peak_population = len(organisms)
    births = 0
    moisture_deaths = 0
    senescence_deaths = 0
    maintenance_deaths = 0
    multi_trigger_deaths = 0
    dormancy_entries = 0
    dormancy_exits = 0
    history = [
        moisture_at_hour(hour, scenario.climate)
        for hour in range(scenario.start_hour - 48, scenario.start_hour)
    ]

    duration = scenario.duration_hours or scenario.years * HOURS_PER_YEAR
    for offset in range(duration):
        tick = scenario.start_hour + offset
        moisture = moisture_at_hour(tick, scenario.climate)
        history.append(moisture)
        co2 += CO2_SOURCE_PER_HOUR
        boundary_carbon += CO2_SOURCE_PER_HOUR

        # Phase 3: advance biological age and evaluate every positive intrinsic
        # probability, even when more than one succeeds.
        dead: list[int] = []
        for organism_id in sorted(organisms):
            organism = organisms[organism_id]
            organism.biological_age += (
                phenotype.dormant_age_multiplier if organism.dormant else 1.0
            )
            if organism.dormant:
                preferred, hard = 0.20, 0.05
            else:
                preferred, hard = phenotype.active_preferred, phenotype.active_hard
            moisture_probability = moisture_death_probability(moisture, preferred, hard)
            age_probability = senescence_probability(organism.biological_age)
            moisture_trigger = moisture_probability > 0 and keyed_uniform(
                seed, "moisture-death", tick, organism_id
            ) < moisture_probability
            age_trigger = age_probability > 0 and keyed_uniform(
                seed, "senescence-death", tick, organism_id
            ) < age_probability
            if moisture_trigger or age_trigger or organism.reserve <= 0:
                dead.append(organism_id)
                moisture_deaths += int(moisture_trigger)
                senescence_deaths += int(age_trigger)
                multi_trigger_deaths += int(moisture_trigger and age_trigger)

        for organism_id in dead:
            organism = organisms.pop(organism_id)
            remnant_carbon += organism.reserve + organism.structure * ASSEMBLY_ENERGY

        if not organisms:
            break

        # Phases 5-6: build age/activity-limited oxygenic requests, then resolve
        # the finite tile CO2 account by deterministic proportional allocation.
        requests: dict[int, int] = {}
        for organism_id in sorted(organisms):
            organism = organisms[organism_id]
            if organism.dormant:
                continue
            activity = terrestrial_activity(
                moisture, phenotype.active_preferred, phenotype.active_hard
            )
            base_opportunities = math.floor(1_500 * min(surface_light(tick), 0.75))
            expected_successes = math.floor(
                base_opportunities * activity * age_throughput(organism.biological_age) * 0.90
            )
            upkeep = phenotype.active_upkeep + stress_cost(
                moisture, phenotype.active_preferred, phenotype.active_hard
            )
            remaining_growth = (
                max(0, 2 * phenotype.mature_structure - organism.structure)
                if scenario.growth_enabled
                else 0
            )
            same_tick_growth = min(phenotype.assembly_ceiling, remaining_growth)
            atomic_capacity = (
                phenotype.capacity - organism.reserve
                + upkeep
                + same_tick_growth * ASSEMBLY_ENERGY
            )
            requests[organism_id] = min(expected_successes, max(0, atomic_capacity))

        grants = proportional_grants(requests, co2, seed, tick)
        for organism_id, grant in grants.items():
            organisms[organism_id].reserve += grant
            co2 -= grant
            co2_consumed += grant

        # Phase 7: maintenance, then optional structure assembly above the
        # current 40% reserve-protection floor.
        failed: list[int] = []
        for organism_id in sorted(organisms):
            organism = organisms[organism_id]
            if organism.dormant:
                preferred, hard = 0.20, 0.05
                upkeep = phenotype.dormant_upkeep + stress_cost(moisture, preferred, hard)
            else:
                preferred, hard = phenotype.active_preferred, phenotype.active_hard
                upkeep = phenotype.active_upkeep + stress_cost(moisture, preferred, hard)
            if organism.reserve < upkeep:
                failed.append(organism_id)
                maintenance_deaths += 1
                continue
            organism.reserve -= upkeep
            waste_carbon += upkeep
            if not organism.dormant and scenario.growth_enabled:
                growth_floor = math.ceil(phenotype.capacity * GROWTH_FLOOR_FRACTION)
                affordable = max(0, (organism.reserve - growth_floor) // ASSEMBLY_ENERGY)
                growth = min(
                    phenotype.assembly_ceiling,
                    max(0, 2 * phenotype.mature_structure - organism.structure),
                    affordable,
                )
                organism.reserve -= growth * ASSEMBLY_ENERGY
                organism.structure += growth

        for organism_id in failed:
            organism = organisms.pop(organism_id)
            remnant_carbon += organism.reserve + organism.structure * ASSEMBLY_ENERGY

        if not organisms:
            break

        # Phase 8: transition first, then deterministic reproduction. The
        # observable policy uses only the current/past moisture history and
        # current reserve; the hindsight comparator is explicitly non-runtime.
        for organism_id in sorted(list(organisms)):
            organism = organisms.get(organism_id)
            if organism is None:
                continue
            desired = desired_dormancy(
                organism,
                phenotype,
                scenario.dormancy_policy,
                tick,
                moisture,
                history,
                scenario.climate,
            )
            if desired != organism.dormant:
                transition_cost = 500 if desired else 250
                if organism.reserve >= transition_cost:
                    organism.reserve -= transition_cost
                    waste_carbon += transition_cost
                    organism.dormant = desired
                    organism.phase_entered_tick = tick
                    dormancy_entries += int(desired)
                    dormancy_exits += int(not desired)
                    continue

            if (
                not scenario.reproduction_enabled
                or organism.dormant
                or tick < organism.reproduction_not_before_tick
            ):
                continue
            health = (
                min(1.0, organism.reserve / phenotype.capacity)
                * environmental_health(
                    moisture, phenotype.active_preferred, phenotype.active_hard
                )
                * age_factor(organism.biological_age)
            )
            if health < REPRODUCTION_HEALTH:
                continue
            if organism.structure < 2 * phenotype.mature_structure:
                continue
            if organism.reserve < 2 * RESULT_RESERVE_MINIMUM + REPRODUCTION_WORK:
                continue

            organism.reserve -= REPRODUCTION_WORK
            waste_carbon += REPRODUCTION_WORK
            child_reserve = organism.reserve // 2
            organism.reserve -= child_reserve
            child_structure = phenotype.mature_structure
            organism.structure -= child_structure
            if min(organism.reserve, child_reserve) < RESULT_RESERVE_MINIMUM:
                raise AssertionError("accepted reproduction violated result reserve floor")
            organism.successful_reproduction_count += 1
            organism.reproduction_not_before_tick = cooldown_tick(seed, organism, tick)
            child = Organism(
                next_id,
                biological_age=0.0,
                reserve=child_reserve,
                structure=child_structure,
                dormant=False,
                phase_entered_tick=tick,
                reproduction_not_before_tick=0,
            )
            child.reproduction_not_before_tick = cooldown_tick(seed, child, tick)
            organisms[next_id] = child
            next_id += 1
            births += 1

        peak_population = max(peak_population, len(organisms))

    living_carbon = sum(
        organism.reserve + organism.structure * ASSEMBLY_ENERGY
        for organism in organisms.values()
    )
    carbon_error = (
        initial_carbon + boundary_carbon
        - (co2 + living_carbon + remnant_carbon + waste_carbon)
    )
    return Result(
        len(organisms),
        peak_population,
        births,
        moisture_deaths,
        senescence_deaths,
        maintenance_deaths,
        multi_trigger_deaths,
        dormancy_entries,
        dormancy_exits,
        co2_consumed,
        co2,
        carbon_error,
    )


def summarize(scenario: Scenario, seeds: range) -> list[Result]:
    results = [run_scenario(scenario, seed) for seed in seeds]
    assert all(result.carbon_error == 0 for result in results)
    final = [result.final_population for result in results]
    peak = [result.peak_population for result in results]
    births = [result.births for result in results]
    extinct = sum(value == 0 for value in final)
    moisture = sum(result.moisture_deaths for result in results)
    senescence = sum(result.senescence_deaths for result in results)
    maintenance = sum(result.maintenance_deaths for result in results)
    entries = sum(result.dormancy_entries for result in results)
    exits = sum(result.dormancy_exits for result in results)
    print(
        f"{scenario.name} | {statistics.median(final):g} "
        f"[{min(final)}..{max(final)}] | {statistics.median(peak):g} | "
        f"{statistics.median(births):g} | {extinct}/{len(results)} | "
        f"{moisture}/{senescence}/{maintenance} | {entries}/{exits}"
    )
    return results


def main() -> None:
    seeds = range(8)
    scenarios = (
        Scenario("seasonal wet active", WET_SURFACE, "seasonal", "never", years=2),
        Scenario(
            "seasonal intermittent active", INTERMITTENT, "seasonal", "never", years=2
        ),
        Scenario(
            "seasonal no-storage observable",
            WET_DORMANT,
            "seasonal",
            "observable",
            years=2,
        ),
        Scenario(
            "seasonal storage current-hard",
            COMPARTMENTALIZED_DORMANT,
            "seasonal",
            "current_hard",
            years=2,
        ),
        Scenario(
            "seasonal storage observable",
            COMPARTMENTALIZED_DORMANT,
            "seasonal",
            "observable",
            years=2,
        ),
        Scenario(
            "seasonal storage hindsight",
            COMPARTMENTALIZED_DORMANT,
            "seasonal",
            "hindsight",
            years=2,
        ),
        Scenario("wet lean", WET_SURFACE, "permanent_wet", "never", years=2),
        Scenario(
            "wet storage",
            COMPARTMENTALIZED_DORMANT,
            "permanent_wet",
            "observable",
            years=2,
        ),
    )

    print(
        "8 seeds; 2 years; 12 full mature founders; initial CO2 = one source-day; "
        "CO2 source = 20,004/h"
    )
    print(
        "scenario | final pop median [range] | peak median | births median | "
        "extinct | moisture/senescence/maintenance deaths (sum) | entries/exits (sum)"
    )
    print("--- | ---: | ---: | ---: | ---: | ---: | ---:")
    population_results = {
        scenario.name: summarize(scenario, seeds) for scenario in scenarios
    }

    print(
        "\n[one dry-season cohort bridge: starts day 100, ends day 270, "
        "128 age-zero founders, reproduction disabled, abundant initial CO2]"
    )
    print(
        "scenario | final pop median [range] | peak median | births median | "
        "extinct | moisture/senescence/maintenance deaths (sum) | entries/exits (sum)"
    )
    print("--- | ---: | ---: | ---: | ---: | ---: | ---:")
    bridge_common = dict(
        climate="seasonal",
        years=1,
        founders=128,
        start_hour=100 * HOURS_PER_DAY,
        duration_hours=170 * HOURS_PER_DAY,
        initial_co2_hours=2_000,
        reproduction_enabled=False,
        growth_enabled=False,
    )
    bridges = (
        Scenario(
            "bridge wet active", WET_SURFACE, dormancy_policy="never", **bridge_common
        ),
        Scenario(
            "bridge no-storage observable",
            WET_DORMANT,
            dormancy_policy="observable",
            **bridge_common,
        ),
        Scenario(
            "bridge storage current-hard",
            COMPARTMENTALIZED_DORMANT,
            dormancy_policy="current_hard",
            **bridge_common,
        ),
        Scenario(
            "bridge storage observable",
            COMPARTMENTALIZED_DORMANT,
            dormancy_policy="observable",
            **bridge_common,
        ),
        Scenario(
            "bridge storage hindsight",
            COMPARTMENTALIZED_DORMANT,
            dormancy_policy="hindsight",
            **bridge_common,
        ),
    )
    bridge_results = {
        scenario.name: summarize(scenario, seeds) for scenario in bridges
    }

    selected = [
        result.final_population
        for result in bridge_results["bridge storage observable"]
    ]
    current_hard = [
        result.final_population
        for result in bridge_results["bridge storage current-hard"]
    ]
    hindsight = [
        result.final_population
        for result in bridge_results["bridge storage hindsight"]
    ]
    assert min(selected) >= 119 and max(selected) <= 126
    assert statistics.median(selected) == 121
    assert current_hard == hindsight
    assert statistics.median(current_hard) == 16
    assert all(
        result.final_population == 0
        for result in bridge_results["bridge no-storage observable"]
    )
    assert sum(
        result.moisture_deaths
        for result in bridge_results["bridge storage observable"]
    ) == 0
    assert sum(
        result.dormancy_entries
        for result in bridge_results["bridge storage observable"]
    ) == 8 * 128
    assert all(
        result.final_population == 0
        for name, results in population_results.items()
        if name.startswith("seasonal")
        for result in results
    )
    assert all(
        result.final_population > 0
        for name, results in population_results.items()
        if name.startswith("wet ")
        for result in results
    )
    assert statistics.median(
        result.final_population for result in population_results["wet lean"]
    ) > statistics.median(
        result.final_population for result in population_results["wet storage"]
    )
    print("\nacceptance assertions: pass")


if __name__ == "__main__":
    main()
