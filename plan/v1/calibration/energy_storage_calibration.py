#!/usr/bin/env python3
"""Expected-value authoring fixture for LYFE's v1 energy-storage ladder.

This is not authoritative engine code. It compares storage construction,
maintenance, reproduction, ordinary starvation endurance, and the established
terrestrial seasonal-isolation fixture without simulating a population.
"""

from __future__ import annotations

from dataclasses import dataclass
import math


HOURS_PER_DAY = 24
DAYS_PER_YEAR = 360
BASE_MAINTENANCE = 50
COMPARTMENTALIZED_BASE_MAINTENANCE = 55
STRUCTURE_ASSEMBLY_ENERGY = 100
FOUNDING_RESERVE = 10_000
CAPACITY_PER_STORAGE_STRUCTURE = 250
DORMANCY_ENTRY_WARNING = 0.35
DORMANCY_EMERGENCY_ENTRY = 0.30
DORMANCY_EXIT_RECOVERY = 0.45
DORMANCY_TREND_WINDOW_HOURS = 24
DORMANCY_MINIMUM_DWELL_HOURS = 24


@dataclass(frozen=True)
class StorageTier:
    name: str
    capacity: int
    storage_structure: int
    storage_upkeep: int
    mutation_cost: int
    complexity: int
    requires_compartmentalized_cell: bool = False


TIERS = (
    StorageTier("PrimitiveOrganicReserve", 10_000, 0, 0, 0, 0),
    StorageTier("ReserveCapacityI", 25_000, 60, 2, 40, 1),
    StorageTier("ReserveCapacityII", 50_000, 160, 5, 80, 2),
    StorageTier(
        "CompartmentalizedReserve", 150_000, 560, 13, 160, 3, True
    ),
)


def surface_light(hour: int) -> float:
    local_fraction = ((hour % HOURS_PER_DAY) + 0.5) / HOURS_PER_DAY
    hour_angle = 2.0 * math.pi * (local_fraction - 0.5)
    return max(0.0, math.cos(hour_angle)) * 0.94


def gross_oxygenic_energy(hour: int, activity: float) -> int:
    return math.floor(
        1_500 * min(surface_light(hour), 0.75) * activity * 0.90
    )


def moisture_at_hour(hour: int) -> float:
    year_fraction = (hour + 0.5) / (HOURS_PER_DAY * DAYS_PER_YEAR)
    return 0.50 + 0.35 * math.cos(2.0 * math.pi * year_fraction)


def activity(moisture: float, preferred: float = 0.70, hard: float = 0.30) -> float:
    if moisture >= preferred:
        return 1.0
    if moisture >= hard:
        progress = (moisture - hard) / (preferred - hard)
        return 0.25 + 0.75 * progress
    return 0.25 * moisture / hard


def stress_cost(moisture: float, preferred: float, hard: float) -> int:
    if moisture >= preferred:
        return 0
    severity = min(1.0, (preferred - moisture) / (preferred - hard))
    return math.ceil(BASE_MAINTENANCE * severity * severity)


def maximum_drawdown(hourly_net: list[int]) -> int:
    cumulative = 0
    prior_peak = 0
    drawdown = 0
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


def phenotype_values(tier: StorageTier) -> tuple[int, int, int, int]:
    if tier.requires_compartmentalized_cell:
        # CompartmentalizedCell's 1,250 structure is multiplied by the 1.10
        # wet-surface liability; storage structure is a separate additive role.
        non_storage_structure = 1_375
        base_maintenance = COMPARTMENTALIZED_BASE_MAINTENANCE
        reproduction_work = 550
    else:
        non_storage_structure = 1_100
        base_maintenance = BASE_MAINTENANCE
        reproduction_work = 500

    total_structure = non_storage_structure + tier.storage_structure
    active_upkeep = base_maintenance + 30 + 20 + tier.storage_upkeep
    dormant_upkeep = (
        math.ceil(base_maintenance * 0.20)
        + math.ceil(tier.storage_upkeep * 0.20)
        + 20
    )
    return total_structure, active_upkeep, dormant_upkeep, reproduction_work


def seasonal_dormancy(tier: StorageTier) -> tuple[int, int, int, float]:
    _, active_upkeep, dormant_upkeep, _ = phenotype_values(tier)
    hourly_net: list[int] = []
    active_hours = 0
    is_dormant = False
    phase_entered_hour = -DORMANCY_MINIMUM_DWELL_HOURS
    history = [moisture_at_hour(hour) for hour in range(-48, 0)]

    for hour in range(HOURS_PER_DAY * DAYS_PER_YEAR):
        moisture = moisture_at_hour(hour)
        history.append(moisture)
        transition = 0

        if is_dormant:
            gross = 0
            upkeep = dormant_upkeep
            stress = stress_cost(moisture, 0.20, 0.05)
        else:
            active_hours += 1
            gross = gross_oxygenic_energy(hour, activity(moisture))
            upkeep = active_upkeep
            stress = stress_cost(moisture, 0.70, 0.30)

        if hour - phase_entered_hour >= DORMANCY_MINIMUM_DWELL_HOURS:
            trend = recent_moisture_trend(history)
            if not is_dormant and (
                moisture <= DORMANCY_EMERGENCY_ENTRY
                or (moisture <= DORMANCY_ENTRY_WARNING and trend < 0)
            ):
                is_dormant = True
                phase_entered_hour = hour
                transition = 500
            elif (
                is_dormant
                and moisture >= DORMANCY_EXIT_RECOVERY
                and trend > 0
            ):
                is_dormant = False
                phase_entered_hour = hour
                transition = 250

        hourly_net.append(gross - upkeep - stress - transition)

    drawdown = maximum_drawdown(hourly_net)
    return sum(hourly_net), drawdown, active_hours, tier.capacity / drawdown


def commissioning_transition(
    prior: StorageTier, target: StorageTier
) -> tuple[int, float, int]:
    storage_structure = prior.storage_structure
    reserve = prior.capacity
    minimum_health = 1.0
    overflow = 0
    assembly_ceiling = 6 if target.requires_compartmentalized_cell else 4
    base_active_upkeep = (
        COMPARTMENTALIZED_BASE_MAINTENANCE
        if target.requires_compartmentalized_cell
        else BASE_MAINTENANCE
    ) + 50

    for hour in range(10_000):
        capacity_before_growth = min(
            target.capacity,
            10_000 + CAPACITY_PER_STORAGE_STRUCTURE * storage_structure,
        )
        captured = gross_oxygenic_energy(hour, 1.0)
        admitted_capture = min(captured, capacity_before_growth - reserve)
        reserve += admitted_capture
        overflow += captured - admitted_capture
        commissioned_storage_upkeep = math.ceil(
            target.storage_upkeep
            * storage_structure
            / target.storage_structure
        )
        active_upkeep = base_active_upkeep + commissioned_storage_upkeep
        reserve -= active_upkeep
        if reserve < 0:
            raise RuntimeError(f"{target.name} starved during commissioning")

        floor = math.ceil(capacity_before_growth * 0.40)
        affordable = max(0, (reserve - floor) // STRUCTURE_ASSEMBLY_ENERGY)
        built = min(
            assembly_ceiling,
            target.storage_structure - storage_structure,
            affordable,
        )
        reserve -= built * STRUCTURE_ASSEMBLY_ENERGY
        storage_structure += built

        capacity_after_growth = min(
            target.capacity,
            10_000 + CAPACITY_PER_STORAGE_STRUCTURE * storage_structure,
        )
        minimum_health = min(minimum_health, reserve / capacity_after_growth)

        if storage_structure == target.storage_structure:
            return hour + 1, minimum_health, overflow

    raise RuntimeError(f"{target.name} did not commission")


def main() -> None:
    for tier in TIERS:
        expected_capacity = (
            10_000 + CAPACITY_PER_STORAGE_STRUCTURE * tier.storage_structure
        )
        assert tier.capacity == expected_capacity

    print(
        "ladder: capacity = 10,000 + 250 * commissioned StorageStructure; "
        "one ReserveOrganic chemistry"
    )
    print("\n[ladder economics]")
    print(
        "tier | capacity | storage structure | build energy | total storage upkeep | "
        "MP/complexity | health at retained 10k"
    )
    print("--- | ---: | ---: | ---: | ---: | ---: | ---:")
    for tier in TIERS:
        print(
            f"{tier.name} | {tier.capacity} | {tier.storage_structure} | "
            f"{tier.storage_structure * STRUCTURE_ASSEMBLY_ENERGY} | "
            f"{tier.storage_upkeep} | {tier.mutation_cost}/{tier.complexity} | "
            f"{FOUNDING_RESERVE / tier.capacity:.3f}"
        )

    print("\n[zero-income endurance from full reserve]")
    print(
        "tier | aquatic basal active days | wet oxygenic active days | "
        "wet oxygenic dormant days"
    )
    print("--- | ---: | ---: | ---:")
    for tier in TIERS:
        _, wet_active, wet_dormant, _ = phenotype_values(tier)
        aquatic_base = (
            COMPARTMENTALIZED_BASE_MAINTENANCE
            if tier.requires_compartmentalized_cell
            else BASE_MAINTENANCE
        ) + tier.storage_upkeep
        print(
            f"{tier.name} | {tier.capacity / aquatic_base / 24:.2f} | "
            f"{tier.capacity / wet_active / 24:.2f} | "
            f"{tier.capacity / wet_dormant / 24:.2f}"
        )

    print("\n[favorable wet-oxygenic commissioning]")
    print("transition | hours | minimum reserve health | overflow-routed energy")
    print("--- | ---: | ---: | ---:")
    for prior, target in zip(TIERS[:-1], TIERS[1:], strict=True):
        hours, minimum_health, overflow = commissioning_transition(prior, target)
        print(
            f"{prior.name} -> {target.name} | {hours} | "
            f"{minimum_health:.3f} | {overflow}"
        )

    print("\n[seasonal wet-surface plus observable ordinary dormancy]")
    print(
        "tier | active/dormant upkeep | annual net | max draw | capacity/draw | "
        "bridges from full"
    )
    print("--- | ---: | ---: | ---: | ---: | ---:")
    for tier in TIERS:
        _, active_upkeep, dormant_upkeep, _ = phenotype_values(tier)
        annual_net, drawdown, _, coverage = seasonal_dormancy(tier)
        print(
            f"{tier.name} | {active_upkeep}/{dormant_upkeep} | {annual_net} | "
            f"{drawdown} | {coverage:.3f} | {'yes' if coverage >= 1 else 'no'}"
        )

    final_draw = seasonal_dormancy(TIERS[-1])[1]
    print("\n[final-tier capacity sensitivity at fixed full liabilities]")
    print("capacity | storage structure | capacity/draw | reserve margin")
    print("--- | ---: | ---: | ---:")
    for capacity in (125_000, 150_000, 175_000):
        storage_structure = (capacity - 10_000) // CAPACITY_PER_STORAGE_STRUCTURE
        print(
            f"{capacity} | {storage_structure} | {capacity / final_draw:.3f} | "
            f"{capacity - final_draw}"
        )

    print("\n[permanently wet oxygenic reproduction landmark]")
    gross_day = sum(gross_oxygenic_energy(hour, 1.0) for hour in range(24))
    print("tier | total structure | daily net | replacement capital | nominal days")
    print("--- | ---: | ---: | ---: | ---:")
    for tier in TIERS:
        structure, active_upkeep, _, reproduction_work = phenotype_values(tier)
        daily_net = gross_day - active_upkeep * 24
        replacement_capital = structure * 100 + 4_000 + reproduction_work
        print(
            f"{tier.name} | {structure} | {daily_net} | "
            f"{replacement_capital} | {replacement_capital / daily_net:.2f}"
        )

    print("\n[mutation-price cadence at 100 effective organisms]")
    print("node | MP | days at health 1.0 | days at health 0.75")
    print("--- | ---: | ---: | ---:")
    for tier in TIERS[1:]:
        full_health_days = tier.mutation_cost / 3.2
        print(
            f"{tier.name} | {tier.mutation_cost} | {full_health_days:.2f} | "
            f"{full_health_days / 0.75:.2f}"
        )


if __name__ == "__main__":
    main()
