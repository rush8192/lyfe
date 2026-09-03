#!/usr/bin/env python3
"""Deterministic authoring fixture for LYFE opening-strategy comparisons.

This extends survival_opening_validation.py with scenario-defined player
authority, trait choices, full-grid organism positions, passive migration,
storage commissioning, and the first dissolved-organic/fermentation path.
It remains a floating-point balance model rather than production engine code.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import argparse
import hashlib
import json
import math
import statistics
from typing import Any, Iterable

from survival_opening_validation import (
    BASE_MAINTENANCE,
    FOUNDER_COUNT,
    GAS_RATES,
    GRID_HEIGHT,
    GRID_WIDTH,
    GROWTH_FLOOR,
    HORIZON_HOURS,
    INITIAL_RESERVE,
    MUTATION_DENOMINATOR,
    REFERENCE_SEED,
    REPRODUCTION_HEALTH_GATE,
    REPRODUCTION_RESERVE_GATE,
    REPRODUCTION_WORK,
    SPECIATION_COOLDOWN,
    SULFUR_LIGHT_EXTENTS,
    GasField,
    age_factor,
    age_throughput,
    build_equilibrium_field,
    keyed_integer,
    keyed_u64,
    keyed_uniform,
    senescence_chance,
    sulfur_stress,
)


FOUNDER_RADIUS = 1.0 / 1024.0
FOUNDER_BROWNIAN_RMS = 2.0 * FOUNDER_RADIUS
PASSIVE_MIGRATION_SUCCESS = 0.10
PHOSPHORUS_BACKGROUND = 250_000
PHOSPHORUS_VOLCANIC = 5_000_000
PHOSPHORUS_SOURCE_PER_HOUR = 250
LDO_HALF_LIFE_HOURS = 336.0
RFP_HALF_LIFE_HOURS = 2_160.0
DISSOLVED_EXCHANGE = 0.0015
STORAGE_STRUCTURE_TARGET = 60
STORAGE_REPRODUCTION_TARGET = 120
CAPACITY_PER_STORAGE_STRUCTURE = 250


@dataclass(frozen=True)
class TraitChoice:
    choice_id: str
    display_name: str
    mutation_cost: float
    change_complexity: int
    benefit_timing: str
    strategic_intent: str
    traits: tuple[str, ...]


CHOICES: dict[str, TraitChoice] = {
    "anchoring": TraitChoice(
        "anchoring", "Environmental Anchoring", 40.0, 1,
        "Immediate", "Exploit current niche", ("EnvironmentalAnchoring",),
    ),
    "drifting": TraitChoice(
        "drifting", "Environmental Drifting", 60.0, 1,
        "Immediate", "Expand or alter dispersal", ("EnvironmentalDrifting",),
    ),
    "organic_uptake": TraitChoice(
        "organic_uptake", "Organic Resource Uptake", 40.0, 1,
        "Preparatory", "Diversify resource and energy access", ("OrganicResourceUptake",),
    ),
    "fermentation_bundle": TraitChoice(
        "fermentation_bundle", "Organic Uptake + Fermentation", 120.0, 3,
        "Conditional", "Diversify resource and energy access",
        ("OrganicResourceUptake", "Fermentation"),
    ),
    "regulation": TraitChoice(
        "regulation", "Metabolic Regulation", 60.0, 1,
        "Preparatory", "Invest in regulation and future flexibility", ("MetabolicRegulation",),
    ),
    "reserve_i": TraitChoice(
        "reserve_i", "Reserve Capacity I", 40.0, 1,
        "Maturing", "Invest in buffering and storage", ("ReserveCapacityI",),
    ),
}

PLAYER_MENUS = {
    "hydrogen": ("anchoring", "organic_uptake", "reserve_i", "fermentation_bundle"),
    "sulfur": ("anchoring", "drifting", "regulation", "reserve_i"),
}


@dataclass(frozen=True)
class Scenario:
    scenario_id: str
    player_metabolism: str
    choice: TraitChoice
    horizon_hours: int = HORIZON_HOURS
    review_hours: int = SPECIATION_COOLDOWN


@dataclass
class Organism:
    organism_id: int
    species_id: int
    metabolism: str
    tile_index: int
    local_x: float
    local_y: float
    reserve: int = INITIAL_RESERVE
    geometric_structure: int = 1_000
    storage_structure: int = 0
    age: int = 0
    extra_quota: int = 0
    reproduction_not_before_tick: int = 0
    reproduction_count: int = 0


@dataclass
class Species:
    species_id: int
    name: str
    metabolism: str
    controlled: bool
    traits: frozenset[str] = frozenset()
    mutation_balance: float = 0.0
    parent_id: int | None = None
    speciation_not_before_tick: int = 0


@dataclass
class SpeciesCounters:
    births: int = 0
    deaths: int = 0
    migrations: int = 0
    failed_migrations: int = 0
    capture_extents: int = 0
    fermentation_extents: int = 0
    storage_constructed: int = 0
    trait_active_organism_hours: int = 0
    useful_trait_events: int = 0


@dataclass
class RemnantField:
    reserve: list[float]
    structure: list[float]
    ldo: list[float]
    reduced_products: list[float]

    @classmethod
    def empty(cls) -> "RemnantField":
        count = GRID_WIDTH * GRID_HEIGHT
        return cls(*([0.0] * count for _ in range(4)))

    def step(self) -> None:
        reserve_loss_factor = 1.0 - 2.0 ** (-1.0 / LDO_HALF_LIFE_HOURS)
        structure_loss_factor = 1.0 - 2.0 ** (-1.0 / 720.0)
        ldo_loss_factor = 1.0 - 2.0 ** (-1.0 / LDO_HALF_LIFE_HOURS)
        rfp_loss_factor = 1.0 - 2.0 ** (-1.0 / RFP_HALF_LIFE_HOURS)
        count = GRID_WIDTH * GRID_HEIGHT
        for index in range(count):
            reserve_loss = self.reserve[index] * reserve_loss_factor
            structure_loss = self.structure[index] * structure_loss_factor
            self.reserve[index] -= reserve_loss
            self.structure[index] -= structure_loss
            self.ldo[index] = self.ldo[index] * (1.0 - ldo_loss_factor) + 4.0 * structure_loss
            self.reduced_products[index] *= 1.0 - rfp_loss_factor
        self.ldo = diffuse(self.ldo, DISSOLVED_EXCHANGE)
        self.reduced_products = diffuse(self.reduced_products, DISSOLVED_EXCHANGE)


@dataclass
class ConsequenceSnapshot:
    population: int
    average_health: float
    average_reserve_fraction: float
    occupied_tiles: int
    births: int
    deaths: int
    migrations: int
    failed_migrations: int
    capture_extents: int
    fermentation_extents: int
    storage_constructed: int
    average_commissioned_capacity: float
    trait_active_organism_hours: int
    useful_trait_events: int


@dataclass
class ScenarioResult:
    seed: int
    scenario_id: str
    player_metabolism: str
    choice_id: str
    mutation_cost: float
    choice_timing: str
    choice_intent: str
    decision_tick: int | None
    review_tick: int | None
    founder_count: int
    founder_digest: str | None
    ancestor_start_population: int
    ancestor: ConsequenceSnapshot | None
    descendant: ConsequenceSnapshot | None
    competitor: ConsequenceSnapshot | None
    descendant_population_ratio: float | None
    descendant_growth_factor: float | None
    ancestor_growth_factor: float | None
    relative_growth_factor: float | None
    descendant_health_delta: float | None
    descendant_reserve_fraction_delta: float | None
    descendant_occupied_tile_delta: int | None
    ldo_at_decision: float | None
    ldo_at_review: float | None
    deaths_by_cause: dict[str, int]
    result_digest: str


def diffuse(values: list[float], coefficient: float) -> list[float]:
    """One conservative wrapped-x, bounded-y explicit exchange pass."""
    delta = [0.0] * len(values)
    for y in range(GRID_HEIGHT):
        for x in range(GRID_WIDTH):
            here = GasField.index(x, y)
            right = GasField.index((x + 1) % GRID_WIDTH, y)
            flow = coefficient * (values[here] - values[right])
            delta[here] -= flow
            delta[right] += flow
            if y + 1 < GRID_HEIGHT:
                down = GasField.index(x, y + 1)
                flow = coefficient * (values[here] - values[down])
                delta[here] -= flow
                delta[down] += flow
    return [values[index] + delta[index] for index in range(len(values))]


def allocate_extents(
    requests: Iterable[tuple[Organism, int]],
    available: int,
    seed: int,
    tick: int,
    channel: str,
) -> dict[int, int]:
    request_list = [(organism, max(0, request)) for organism, request in requests]
    total = sum(request for _, request in request_list)
    if total <= available:
        return {organism.organism_id: request for organism, request in request_list}
    if available <= 0 or total == 0:
        return {organism.organism_id: 0 for organism, _ in request_list}
    scale = available / total
    grants = {organism.organism_id: math.floor(request * scale) for organism, request in request_list}
    remainder = available - sum(grants.values())
    eligible = [organism for organism, request in request_list if grants[organism.organism_id] < request]
    eligible.sort(key=lambda item: (keyed_u64(seed, channel, tick, item.organism_id), item.organism_id))
    for organism in eligible[:remainder]:
        grants[organism.organism_id] += 1
    return grants


def current_capacity(organism: Organism, spec: Species) -> int:
    if "ReserveCapacityI" not in spec.traits:
        return 10_000
    return min(25_000, 10_000 + CAPACITY_PER_STORAGE_STRUCTURE * min(
        organism.storage_structure, STORAGE_STRUCTURE_TARGET
    ))


def storage_upkeep(organism: Organism, spec: Species) -> int:
    if "ReserveCapacityI" not in spec.traits or organism.storage_structure <= 0:
        return 0
    return math.ceil(2.0 * min(organism.storage_structure, STORAGE_STRUCTURE_TARGET) / STORAGE_STRUCTURE_TARGET)


def trait_upkeep(organism: Organism, spec: Species) -> int:
    upkeep = storage_upkeep(organism, spec)
    upkeep += 2 if "EnvironmentalAnchoring" in spec.traits else 0
    upkeep += 3 if "EnvironmentalDrifting" in spec.traits else 0
    upkeep += 5 if "OrganicResourceUptake" in spec.traits else 0
    upkeep += 10 if "Fermentation" in spec.traits else 0
    upkeep += 5 if "MetabolicRegulation" in spec.traits else 0
    return upkeep


def passive_multiplier(spec: Species) -> float:
    if "EnvironmentalAnchoring" in spec.traits:
        return 0.25
    if "EnvironmentalDrifting" in spec.traits:
        return 1.50
    return 1.0


def condition(organism: Organism, spec: Species, field: GasField) -> float:
    _, so2_factor, _ = sulfur_stress(field.stock["SO2"][organism.tile_index], "SO2")
    _, h2s_factor, _ = sulfur_stress(field.stock["H2S"][organism.tile_index], "H2S")
    capacity = current_capacity(organism, spec)
    reserve = max(0.0, min(1.0, organism.reserve / capacity))
    mature_target = 1_000 + (STORAGE_STRUCTURE_TARGET if "ReserveCapacityI" in spec.traits else 0)
    structure = max(0.0, min(
        1.0,
        (organism.geometric_structure + organism.storage_structure) / mature_target,
    ))
    return reserve * structure * age_factor(organism.age) * so2_factor * h2s_factor


def metabolism_quota(metabolism: str) -> int:
    return 57 if metabolism == "hydrogen" else 55


def metabolism_growth_ceiling(metabolism: str) -> int:
    return 3 if metabolism == "hydrogen" else 4


def tile_groups(organisms: list[Organism]) -> dict[int, list[Organism]]:
    grouped: dict[int, list[Organism]] = {}
    for organism in organisms:
        grouped.setdefault(organism.tile_index, []).append(organism)
    return grouped


def species_members(organisms: list[Organism]) -> dict[int, list[Organism]]:
    grouped: dict[int, list[Organism]] = {}
    for organism in organisms:
        grouped.setdefault(organism.species_id, []).append(organism)
    return grouped


def brownian_vector(seed: int, tick: int, organism: Organism, multiplier: float) -> tuple[float, float]:
    bits = keyed_u64(seed, "brownian", tick, organism.organism_id)
    radial_u = max(1.0 / 2**32, ((bits >> 32) + 0.5) / 2**32)
    angle_u = ((bits & 0xFFFFFFFF) + 0.5) / 2**32
    radius = FOUNDER_BROWNIAN_RMS * multiplier * math.sqrt(-math.log(radial_u))
    angle = 2.0 * math.pi * angle_u
    return radius * math.cos(angle), radius * math.sin(angle)


def move_organism(
    organism: Organism,
    spec: Species,
    seed: int,
    tick: int,
    counters: SpeciesCounters,
) -> None:
    multiplier = passive_multiplier(spec)
    dx, dy = brownian_vector(seed, tick, organism, multiplier)
    counters.trait_active_organism_hours += int(multiplier != 1.0)
    next_x = organism.local_x + dx
    next_y = organism.local_y + dy
    tile_x = organism.tile_index % GRID_WIDTH
    tile_y = organism.tile_index // GRID_WIDTH
    crossings: list[tuple[float, str, int]] = []
    if dx < 0.0 and next_x < 0.0:
        crossings.append((organism.local_x / -dx, "x", -1))
    elif dx > 0.0 and next_x >= 1.0:
        crossings.append(((1.0 - organism.local_x) / dx, "x", 1))
    if dy < 0.0 and next_y < 0.0 and tile_y > 0:
        crossings.append((organism.local_y / -dy, "y", -1))
    elif dy > 0.0 and next_y >= 1.0 and tile_y < GRID_HEIGHT - 1:
        crossings.append(((1.0 - organism.local_y) / dy, "y", 1))
    if not crossings:
        if next_y < 0.0:
            next_y = -next_y
        elif next_y >= 1.0:
            next_y = 2.0 - next_y
        organism.local_x = min(1.0 - 1e-12, max(0.0, next_x))
        organism.local_y = min(1.0 - 1e-12, max(0.0, next_y))
        return
    _, axis, direction = min(crossings, key=lambda item: (item[0], item[1]))
    target_x, target_y = tile_x, tile_y
    if axis == "x":
        target_x = (tile_x + direction) % GRID_WIDTH
    else:
        target_y = tile_y + direction
    admitted = keyed_uniform(seed, "passive-migration", tick, organism.organism_id) < PASSIVE_MIGRATION_SUCCESS
    if admitted:
        organism.tile_index = GasField.index(target_x, target_y)
        organism.local_x = next_x % 1.0 if axis == "x" else min(1.0 - 1e-12, max(0.0, next_x))
        organism.local_y = next_y % 1.0 if axis == "y" else min(1.0 - 1e-12, max(0.0, next_y))
        counters.migrations += 1
        counters.useful_trait_events += int(multiplier != 1.0)
    else:
        organism.local_x = min(1.0 - 1e-12, max(0.0, abs(next_x) if next_x < 0 else 2.0 - next_x if next_x >= 1 else next_x))
        organism.local_y = min(1.0 - 1e-12, max(0.0, abs(next_y) if next_y < 0 else 2.0 - next_y if next_y >= 1 else next_y))
        counters.failed_migrations += 1


def choose_growth_role(organism: Organism, spec: Species) -> str:
    if "ReserveCapacityI" not in spec.traits:
        return "geometric"
    geometric_deficit = max(0.0, (2_000 - organism.geometric_structure) / 2_000)
    storage_deficit = max(0.0, (STORAGE_REPRODUCTION_TARGET - organism.storage_structure) / STORAGE_REPRODUCTION_TARGET)
    return "storage" if storage_deficit > geometric_deficit else "geometric"


def snapshot(
    members: list[Organism],
    spec: Species,
    field: GasField,
    counters: SpeciesCounters,
) -> ConsequenceSnapshot:
    if not members:
        return ConsequenceSnapshot(0, 0.0, 0.0, 0, counters.births, counters.deaths,
                                   counters.migrations, counters.failed_migrations,
                                   counters.capture_extents, counters.fermentation_extents,
                                   counters.storage_constructed, 0.0,
                                   counters.trait_active_organism_hours, counters.useful_trait_events)
    return ConsequenceSnapshot(
        population=len(members),
        average_health=statistics.fmean(condition(item, spec, field) for item in members),
        average_reserve_fraction=statistics.fmean(item.reserve / current_capacity(item, spec) for item in members),
        occupied_tiles=len({item.tile_index for item in members}),
        births=counters.births,
        deaths=counters.deaths,
        migrations=counters.migrations,
        failed_migrations=counters.failed_migrations,
        capture_extents=counters.capture_extents,
        fermentation_extents=counters.fermentation_extents,
        storage_constructed=counters.storage_constructed,
        average_commissioned_capacity=statistics.fmean(current_capacity(item, spec) for item in members),
        trait_active_organism_hours=counters.trait_active_organism_hours,
        useful_trait_events=counters.useful_trait_events,
    )


def run_scenario(seed: int, scenario: Scenario, equilibrium: GasField, tile_indices: dict[str, int]) -> ScenarioResult:
    gas = equilibrium.clone()
    remnants = RemnantField.empty()
    tile_count = GRID_WIDTH * GRID_HEIGHT
    phosphorus = [PHOSPHORUS_BACKGROUND] * tile_count
    phosphorus[tile_indices["hydrogen"]] = PHOSPHORUS_VOLCANIC
    phosphorus[tile_indices["sulfur"]] = PHOSPHORUS_VOLCANIC
    player_metabolism = scenario.player_metabolism
    competitor_metabolism = "sulfur" if player_metabolism == "hydrogen" else "hydrogen"
    species: dict[int, Species] = {
        1: Species(1, f"{player_metabolism.title()} player root", player_metabolism, True),
        2: Species(2, f"{competitor_metabolism.title()} competitor root", competitor_metabolism, False),
    }
    counters: dict[int, SpeciesCounters] = {1: SpeciesCounters(), 2: SpeciesCounters()}
    organisms: list[Organism] = []
    next_organism_id = 1
    for species_id, metabolism in ((1, player_metabolism), (2, competitor_metabolism)):
        start_tile = tile_indices[metabolism]
        for _ in range(FOUNDER_COUNT):
            organisms.append(Organism(
                organism_id=next_organism_id,
                species_id=species_id,
                metabolism=metabolism,
                tile_index=start_tile,
                local_x=keyed_uniform(seed, "initial-x", next_organism_id),
                local_y=keyed_uniform(seed, "initial-y", next_organism_id),
                reproduction_not_before_tick=24 + keyed_integer(
                    seed, 0, 3, "reproduction-cooldown", next_organism_id, 0
                ),
            ))
            next_organism_id += 1

    deaths_by_cause = {"Senescence": 0, "ChemicalExposure": 0, "MaintenanceFailure": 0, "EnergyExhaustion": 0}
    event_hash = hashlib.blake2b(digest_size=16)
    decision_tick: int | None = None
    review_tick: int | None = None
    descendant_id: int | None = None
    founder_count = 0
    founder_digest: str | None = None
    ancestor_start_population = 0
    ldo_at_decision: float | None = None
    ldo_at_review: float | None = None
    ancestor_review: ConsequenceSnapshot | None = None
    descendant_review: ConsequenceSnapshot | None = None
    competitor_review: ConsequenceSnapshot | None = None
    pending_speciation = False

    for tick in range(scenario.horizon_hours):
        if pending_speciation:
            start_tile = tile_indices[player_metabolism]
            candidates = [item for item in organisms if item.species_id == 1 and item.tile_index == start_tile]
            candidates.sort(key=lambda item: (
                keyed_u64(seed, "strategy-founder", tick, item.organism_id),
                item.organism_id,
            ))
            founder_count = max(1, math.floor(len(candidates) * 0.50))
            descendant_id = 3
            ancestor = species[1]
            post_price = ancestor.mutation_balance - scenario.choice.mutation_cost
            ancestor.mutation_balance = post_price
            ancestor.controlled = False
            ancestor.speciation_not_before_tick = tick + SPECIATION_COOLDOWN
            species[descendant_id] = Species(
                descendant_id,
                f"{scenario.choice.display_name} descendant",
                player_metabolism,
                True,
                frozenset(scenario.choice.traits),
                post_price,
                1,
                tick + SPECIATION_COOLDOWN,
            )
            counters[1] = SpeciesCounters()
            counters[descendant_id] = SpeciesCounters()
            for organism in candidates[:founder_count]:
                organism.species_id = descendant_id
            founder_hasher = hashlib.blake2b(digest_size=16)
            for organism in sorted(candidates[:founder_count], key=lambda item: item.organism_id):
                founder_hasher.update(organism.organism_id.to_bytes(8, "big"))
            founder_digest = founder_hasher.hexdigest()
            ancestor_start_population = sum(1 for item in organisms if item.species_id == 1)
            decision_tick = tick
            review_tick = tick + scenario.review_hours
            ldo_at_decision = sum(remnants.ldo)
            event_hash.update(f"speciation:{tick}:{scenario.choice.choice_id}:{founder_count}".encode())
            pending_speciation = False

        gas.step()
        phosphorus[tile_indices["hydrogen"]] += PHOSPHORUS_SOURCE_PER_HOUR
        phosphorus[tile_indices["sulfur"]] += PHOSPHORUS_SOURCE_PER_HOUR
        remnants.step()

        # Intrinsic death evaluation.
        survivors: list[Organism] = []
        for organism in organisms:
            organism.age += 1
            risks: list[tuple[str, float]] = []
            if organism.reserve <= 0:
                risks.append(("EnergyExhaustion", 1.0))
            age_risk = senescence_chance(organism.age)
            if age_risk > 0:
                risks.append(("Senescence", age_risk))
            for chemical in ("SO2", "H2S"):
                _, _, chance = sulfur_stress(gas.stock[chemical][organism.tile_index], chemical)
                if chance > 0:
                    risks.append(("ChemicalExposure", chance))
            triggered = next((name for name, chance in risks
                              if keyed_uniform(seed, "death", name, tick, organism.organism_id) < chance), None)
            if triggered is None:
                survivors.append(organism)
                continue
            deaths_by_cause[triggered] += 1
            counters[organism.species_id].deaths += 1
            remnants.reserve[organism.tile_index] += organism.reserve
            remnants.structure[organism.tile_index] += organism.geometric_structure + organism.storage_structure
            event_hash.update(f"death:{tick}:{organism.organism_id}:{triggered}".encode())
        organisms = survivors

        # Passive movement and migration.
        for organism in organisms:
            move_organism(organism, species[organism.species_id], seed, tick, counters[organism.species_id])

        # External founding-path capture, grouped by real current tile.
        for tile_index, tile_organisms in tile_groups(organisms).items():
            by_metabolism: dict[str, list[Organism]] = {"hydrogen": [], "sulfur": []}
            for organism in tile_organisms:
                by_metabolism[organism.metabolism].append(organism)
            for metabolism, members in by_metabolism.items():
                if not members:
                    continue
                capture_requests: list[tuple[Organism, int]] = []
                for organism in members:
                    spec = species[organism.species_id]
                    so2_cost, _, _ = sulfur_stress(gas.stock["SO2"][tile_index], "SO2")
                    h2s_cost, _, _ = sulfur_stress(gas.stock["H2S"][tile_index], "H2S")
                    upkeep = BASE_MAINTENANCE + so2_cost + h2s_cost + trait_upkeep(organism, spec)
                    capacity = current_capacity(organism, spec)
                    possible_growth = math.floor(metabolism_growth_ceiling(metabolism) * age_throughput(organism.age))
                    output_need = max(0, capacity - organism.reserve + upkeep + 100 * possible_growth)
                    if metabolism == "hydrogen":
                        request = min(math.floor(200 * age_throughput(organism.age)), math.ceil(output_need / 2))
                    else:
                        request = min(math.floor(SULFUR_LIGHT_EXTENTS[tick % 24] * age_throughput(organism.age)), output_need)
                    capture_requests.append((organism, request))
                if metabolism == "hydrogen":
                    available = min(math.floor(gas.stock["H2"][tile_index] / 4), math.floor(gas.stock["CO2"][tile_index] / 2))
                    reserve_per_extent, h2_per_extent, co2_per_extent = 2, 4, 2
                else:
                    available = min(math.floor(gas.stock["H2S"][tile_index] / 2), math.floor(gas.stock["CO2"][tile_index]))
                    reserve_per_extent, h2_per_extent, co2_per_extent = 1, 0, 1
                grants = allocate_extents(
                    capture_requests, available, seed, tick, f"{metabolism}-capture-{tile_index}"
                )
                total = sum(grants.values())
                gas.stock["H2"][tile_index] -= h2_per_extent * total
                gas.stock["H2S"][tile_index] -= (2 if metabolism == "sulfur" else 0) * total
                gas.stock["CO2"][tile_index] -= co2_per_extent * total
                for organism in members:
                    granted = grants[organism.organism_id]
                    organism.reserve = min(
                        current_capacity(organism, species[organism.species_id]),
                        organism.reserve + reserve_per_extent * granted,
                    )
                    counters[organism.species_id].capture_extents += granted

        # Conditional dissolved-organic fermentation. Uptake alone correctly emits no claim.
        for tile_index, members in tile_groups(organisms).items():
            fermentation_requests: list[tuple[Organism, int]] = []
            for organism in members:
                spec = species[organism.species_id]
                if "Fermentation" not in spec.traits:
                    continue
                capacity = current_capacity(organism, spec)
                request = min(120, max(0, math.ceil((capacity - organism.reserve) / 2)))
                expected_success = math.floor(request * 0.90 + keyed_uniform(
                    seed, "fermentation-success-round", tick, organism.organism_id
                ))
                fermentation_requests.append((organism, expected_success))
            grants = allocate_extents(
                fermentation_requests,
                math.floor(remnants.ldo[tile_index]),
                seed,
                tick,
                f"fermentation-{tile_index}",
            )
            total = sum(grants.values())
            remnants.ldo[tile_index] -= total
            remnants.reduced_products[tile_index] += total
            for organism, _ in fermentation_requests:
                granted = grants[organism.organism_id]
                organism.reserve = min(current_capacity(organism, species[organism.species_id]), organism.reserve + 2 * granted)
                counters[organism.species_id].fermentation_extents += granted
                counters[organism.species_id].trait_active_organism_hours += 1
                counters[organism.species_id].useful_trait_events += int(granted > 0)

        # Internal maintenance and structural assignment.
        alive_after_maintenance: list[Organism] = []
        growth_by_tile: dict[int, list[tuple[Organism, int, str]]] = {}
        for organism in organisms:
            spec = species[organism.species_id]
            if "MetabolicRegulation" in spec.traits:
                counters[organism.species_id].trait_active_organism_hours += 1
            so2_cost, _, _ = sulfur_stress(gas.stock["SO2"][organism.tile_index], "SO2")
            h2s_cost, _, _ = sulfur_stress(gas.stock["H2S"][organism.tile_index], "H2S")
            upkeep = BASE_MAINTENANCE + so2_cost + h2s_cost + trait_upkeep(organism, spec)
            if organism.reserve < upkeep:
                deaths_by_cause["MaintenanceFailure"] += 1
                counters[organism.species_id].deaths += 1
                remnants.reserve[organism.tile_index] += organism.reserve
                remnants.structure[organism.tile_index] += organism.geometric_structure + organism.storage_structure
                event_hash.update(f"death:{tick}:{organism.organism_id}:MaintenanceFailure".encode())
                continue
            organism.reserve -= upkeep
            capacity = current_capacity(organism, spec)
            floor = max(GROWTH_FLOOR, math.ceil(0.40 * capacity))
            ceiling = math.floor(metabolism_growth_ceiling(organism.metabolism) * age_throughput(organism.age))
            request = min(ceiling, max(0, (organism.reserve - floor) // 100))
            growth_by_tile.setdefault(organism.tile_index, []).append((organism, request, choose_growth_role(organism, spec)))
            alive_after_maintenance.append(organism)
        organisms = alive_after_maintenance

        for tile_index, plans in growth_by_tile.items():
            requests = [(organism, request) for organism, request, _ in plans]
            available = min(
                sum(request for _, request in requests),
                math.floor(gas.stock["NH3"][tile_index] / 20),
                math.floor(gas.stock["H2S"][tile_index]),
                phosphorus[tile_index] // 2,
            )
            grants = allocate_extents(requests, available, seed, tick, f"growth-{tile_index}")
            total = sum(grants.values())
            gas.stock["NH3"][tile_index] -= 20 * total
            gas.stock["H2S"][tile_index] -= total
            phosphorus[tile_index] -= 2 * total
            role_by_id = {organism.organism_id: role for organism, _, role in plans}
            for organism, _ in requests:
                granted = grants[organism.organism_id]
                organism.reserve -= 100 * granted
                if role_by_id[organism.organism_id] == "storage":
                    organism.storage_structure += granted
                    counters[organism.species_id].storage_constructed += granted
                    counters[organism.species_id].trait_active_organism_hours += 1
                    counters[organism.species_id].useful_trait_events += int(granted > 0)
                else:
                    organism.geometric_structure += granted

        # Primitive quota acquisition.
        for organism in organisms:
            quota = metabolism_quota(organism.metabolism)
            if organism.extra_quota < quota and keyed_uniform(seed, "micronutrient", tick, organism.organism_id) < 0.5:
                organism.extra_quota += 1

        # Deterministic fission.
        births: list[Organism] = []
        for organism in organisms:
            spec = species[organism.species_id]
            quota = metabolism_quota(organism.metabolism)
            required_storage = STORAGE_REPRODUCTION_TARGET if "ReserveCapacityI" in spec.traits else 0
            if not (
                tick >= organism.reproduction_not_before_tick
                and condition(organism, spec, gas) >= REPRODUCTION_HEALTH_GATE
                and organism.geometric_structure >= 2_000
                and organism.storage_structure >= required_storage
                and organism.extra_quota >= quota
                and organism.reserve >= REPRODUCTION_RESERVE_GATE
            ):
                continue
            organism.reserve -= REPRODUCTION_WORK
            child_reserve = organism.reserve // 2
            organism.reserve -= child_reserve
            child_geometric = organism.geometric_structure // 2
            organism.geometric_structure -= child_geometric
            child_storage = organism.storage_structure // 2
            organism.storage_structure -= child_storage
            organism.extra_quota -= quota
            organism.reproduction_count += 1
            organism.reproduction_not_before_tick = tick + 1 + 24 + keyed_integer(
                seed, 0, 3, "reproduction-cooldown", organism.organism_id, organism.reproduction_count
            )
            angle = 2.0 * math.pi * keyed_uniform(seed, "offspring-angle", tick, next_organism_id)
            distance = 2.5 * FOUNDER_RADIUS
            child_x = min(1.0 - 1e-12, max(0.0, organism.local_x + distance * math.cos(angle)))
            child_y = min(1.0 - 1e-12, max(0.0, organism.local_y + distance * math.sin(angle)))
            births.append(Organism(
                next_organism_id, organism.species_id, organism.metabolism,
                organism.tile_index, child_x, child_y, child_reserve,
                child_geometric, child_storage, 0, 0,
                tick + 1 + 24 + keyed_integer(seed, 0, 3, "reproduction-cooldown", next_organism_id, 0),
            ))
            counters[organism.species_id].births += 1
            event_hash.update(f"birth:{tick + 1}:{next_organism_id}:{organism.species_id}".encode())
            next_organism_id += 1
        organisms.extend(births)

        for resource_name, values in gas.stock.items():
            if min(values) < -1e-6:
                raise AssertionError(f"negative gas {resource_name} at tick {tick}")
        if min(phosphorus) < 0 or min(remnants.ldo) < -1e-6 or min(remnants.reduced_products) < -1e-6:
            raise AssertionError(f"negative non-gas resource at tick {tick}")

        # Completed-tick species aggregation and mutation income.
        grouped_species = species_members(organisms)
        for species_id, spec in species.items():
            members = grouped_species.get(species_id, [])
            if not members:
                continue
            average_health = statistics.fmean(condition(item, spec, gas) for item in members)
            effective_population = 100.0 * math.log2(1.0 + len(members) / 100.0)
            spec.mutation_balance += effective_population * average_health / MUTATION_DENOMINATOR

        if decision_tick is None and species[1].mutation_balance >= scenario.choice.mutation_cost:
            pending_speciation = True

        if review_tick is not None and tick + 1 == review_tick:
            grouped_species = species_members(organisms)
            ancestor_review = snapshot(grouped_species.get(1, []), species[1], gas, counters[1])
            descendant_review = snapshot(grouped_species.get(descendant_id or -1, []), species[descendant_id or -1], gas, counters[descendant_id or -1])
            competitor_review = snapshot(grouped_species.get(2, []), species[2], gas, counters[2])
            ldo_at_review = sum(remnants.ldo)
            break

    if ancestor_review is None and decision_tick is not None and descendant_id is not None:
        grouped_species = species_members(organisms)
        ancestor_review = snapshot(grouped_species.get(1, []), species[1], gas, counters[1])
        descendant_review = snapshot(grouped_species.get(descendant_id, []), species[descendant_id], gas, counters[descendant_id])
        competitor_review = snapshot(grouped_species.get(2, []), species[2], gas, counters[2])
        ldo_at_review = sum(remnants.ldo)

    population_ratio: float | None
    descendant_growth: float | None
    ancestor_growth: float | None
    relative_growth: float | None
    health_delta: float | None
    reserve_delta: float | None
    tile_delta: int | None
    if ancestor_review is not None and descendant_review is not None:
        population_ratio = descendant_review.population / max(1, ancestor_review.population)
        descendant_growth = descendant_review.population / max(1, founder_count)
        ancestor_growth = ancestor_review.population / max(1, ancestor_start_population)
        relative_growth = descendant_growth / max(1e-12, ancestor_growth)
        health_delta = descendant_review.average_health - ancestor_review.average_health
        reserve_delta = descendant_review.average_reserve_fraction - ancestor_review.average_reserve_fraction
        tile_delta = descendant_review.occupied_tiles - ancestor_review.occupied_tiles
    else:
        population_ratio = None
        descendant_growth = None
        ancestor_growth = None
        relative_growth = None
        health_delta = None
        reserve_delta = None
        tile_delta = None

    for organism in sorted(organisms, key=lambda item: item.organism_id):
        event_hash.update(
            f"state:{organism.organism_id}:{organism.species_id}:{organism.tile_index}:"
            f"{organism.local_x:.12f}:{organism.local_y:.12f}:{organism.reserve}:"
            f"{organism.geometric_structure}:{organism.storage_structure}:{organism.age}".encode()
        )
    event_hash.update(f"ldo:{sum(remnants.ldo):.9f}:rfp:{sum(remnants.reduced_products):.9f}".encode())

    return ScenarioResult(
        seed=seed,
        scenario_id=scenario.scenario_id,
        player_metabolism=player_metabolism,
        choice_id=scenario.choice.choice_id,
        mutation_cost=scenario.choice.mutation_cost,
        choice_timing=scenario.choice.benefit_timing,
        choice_intent=scenario.choice.strategic_intent,
        decision_tick=decision_tick,
        review_tick=review_tick,
        founder_count=founder_count,
        founder_digest=founder_digest,
        ancestor_start_population=ancestor_start_population,
        ancestor=ancestor_review,
        descendant=descendant_review,
        competitor=competitor_review,
        descendant_population_ratio=population_ratio,
        descendant_growth_factor=descendant_growth,
        ancestor_growth_factor=ancestor_growth,
        relative_growth_factor=relative_growth,
        descendant_health_delta=health_delta,
        descendant_reserve_fraction_delta=reserve_delta,
        descendant_occupied_tile_delta=tile_delta,
        ldo_at_decision=ldo_at_decision,
        ldo_at_review=ldo_at_review,
        deaths_by_cause=deaths_by_cause,
        result_digest=event_hash.hexdigest(),
    )


def scenarios(horizon_hours: int, review_hours: int = SPECIATION_COOLDOWN) -> list[Scenario]:
    result = []
    for metabolism, choice_ids in PLAYER_MENUS.items():
        for choice_id in choice_ids:
            result.append(Scenario(
                f"{metabolism}-{choice_id}", metabolism, CHOICES[choice_id], horizon_hours, review_hours
            ))
    return result


def distribution(values: list[float | int | None]) -> dict[str, float | int | None]:
    clean = [value for value in values if value is not None]
    if not clean:
        return {"min": None, "median": None, "max": None}
    return {"min": min(clean), "median": statistics.median(clean), "max": max(clean)}


def summarize(results: list[ScenarioResult], equilibrium_iterations: int) -> dict[str, Any]:
    by_scenario: dict[str, list[ScenarioResult]] = {}
    for result in results:
        by_scenario.setdefault(result.scenario_id, []).append(result)
    summaries: dict[str, Any] = {}
    for scenario_id, group in by_scenario.items():
        sample = group[0]
        summaries[scenario_id] = {
            "player_metabolism": sample.player_metabolism,
            "choice_id": sample.choice_id,
            "mutation_cost": sample.mutation_cost,
            "choice_timing": sample.choice_timing,
            "choice_intent": sample.choice_intent,
            "decision_tick": distribution([item.decision_tick for item in group]),
            "review_tick": distribution([item.review_tick for item in group]),
            "founder_count": distribution([item.founder_count for item in group]),
            "founder_digests": [item.founder_digest for item in group],
            "descendant_population": distribution([
                item.descendant.population if item.descendant else None for item in group
            ]),
            "descendant_population_ratio": distribution([item.descendant_population_ratio for item in group]),
            "relative_growth_factor": distribution([item.relative_growth_factor for item in group]),
            "descendant_health_delta": distribution([item.descendant_health_delta for item in group]),
            "descendant_reserve_fraction_delta": distribution([
                item.descendant_reserve_fraction_delta for item in group
            ]),
            "descendant_occupied_tile_delta": distribution([
                item.descendant_occupied_tile_delta for item in group
            ]),
            "descendant_migrations": distribution([
                item.descendant.migrations if item.descendant else None for item in group
            ]),
            "ancestor_migrations": distribution([
                item.ancestor.migrations if item.ancestor else None for item in group
            ]),
            "descendant_births": distribution([
                item.descendant.births if item.descendant else None for item in group
            ]),
            "ancestor_births": distribution([
                item.ancestor.births if item.ancestor else None for item in group
            ]),
            "descendant_fermentation_extents": distribution([
                item.descendant.fermentation_extents if item.descendant else None for item in group
            ]),
            "descendant_storage_constructed": distribution([
                item.descendant.storage_constructed if item.descendant else None for item in group
            ]),
            "descendant_average_capacity": distribution([
                item.descendant.average_commissioned_capacity if item.descendant else None for item in group
            ]),
            "useful_trait_events": distribution([
                item.descendant.useful_trait_events if item.descendant else None for item in group
            ]),
            "ldo_at_decision": distribution([item.ldo_at_decision for item in group]),
            "ldo_at_review": distribution([item.ldo_at_review for item in group]),
            "result_digests": [item.result_digest for item in group],
        }
    failures = []
    for result in results:
        if result.decision_tick is None or result.review_tick is None or result.descendant is None:
            failures.append(f"{result.scenario_id} seed {result.seed}: no complete decision/review")
        elif result.descendant.population == 0:
            failures.append(f"{result.scenario_id} seed {result.seed}: descendant extinct by review")
    same_boundary_cohorts: dict[tuple[int, str, float, int | None], str | None] = {}
    for result in results:
        key = (result.seed, result.player_metabolism, result.mutation_cost, result.decision_tick)
        prior = same_boundary_cohorts.setdefault(key, result.founder_digest)
        if prior != result.founder_digest:
            failures.append(
                f"{result.scenario_id} seed {result.seed}: same-boundary founder cohort differs"
            )
    return {
        "acceptance": "PASS" if not failures else "FAIL",
        "acceptance_failures": failures,
        "equilibrium_iterations": equilibrium_iterations,
        "seeds_per_scenario": len(results) // max(1, len(by_scenario)),
        "scenario_count": len(by_scenario),
        "scenarios": summaries,
        "reference_results": [item.__dict__ for item in results if item.seed == REFERENCE_SEED],
    }


def print_report(report: dict[str, Any]) -> None:
    print(f"equilibrium iterations: {report['equilibrium_iterations']}")
    print(f"scenarios: {report['scenario_count']}; seeds/scenario: {report['seeds_per_scenario']}")
    print(f"acceptance: {report['acceptance']}")
    if "repeat_determinism" in report:
        print(f"repeat determinism: {report['repeat_determinism']}")
    if report["acceptance_failures"]:
        for failure in report["acceptance_failures"]:
            print("FAIL", failure)
    print()
    print("scenario                         decision  rel.growth health Δ   reserve Δ  tiles Δ  migrations  ferm.  storage")
    for scenario_id, values in report["scenarios"].items():
        median = lambda field: values[field]["median"]
        print(
            f"{scenario_id:32s}"
            f" {median('decision_tick'):8.1f}"
            f" {median('relative_growth_factor'):10.3f}"
            f" {median('descendant_health_delta'):+9.3f}"
            f" {median('descendant_reserve_fraction_delta'):+10.3f}"
            f" {median('descendant_occupied_tile_delta'):8.1f}"
            f" {median('descendant_migrations'):11.1f}"
            f" {median('descendant_fermentation_extents'):6.1f}"
            f" {median('descendant_storage_constructed'):8.1f}"
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=4)
    parser.add_argument("--horizon-days", type=int, default=45)
    parser.add_argument("--review-hours", type=int, default=SPECIATION_COOLDOWN)
    parser.add_argument("--scenario", choices=[scenario.scenario_id for scenario in scenarios(HORIZON_HOURS)])
    parser.add_argument("--verify-repeat", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    equilibrium, indices = build_equilibrium_field()
    selected = scenarios(args.horizon_days * 24, args.review_hours)
    if args.scenario:
        selected = [scenario for scenario in selected if scenario.scenario_id == args.scenario]
    results = [
        run_scenario(REFERENCE_SEED + offset, scenario, equilibrium, indices)
        for scenario in selected
        for offset in range(args.seeds)
    ]
    report = summarize(results, indices["iterations"])
    if args.verify_repeat:
        repeated = [
            run_scenario(REFERENCE_SEED + offset, scenario, equilibrium, indices)
            for scenario in selected
            for offset in range(args.seeds)
        ]
        report["repeat_determinism"] = all(
            left == right for left, right in zip(results, repeated, strict=True)
        )
        if not report["repeat_determinism"]:
            report["acceptance"] = "FAIL"
            report["acceptance_failures"].append("repeat run differed from first run")
    if args.json:
        print(json.dumps(report, indent=2, sort_keys=True, default=lambda item: item.__dict__))
    else:
        print_report(report)


if __name__ == "__main__":
    main()
