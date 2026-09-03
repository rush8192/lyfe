#!/usr/bin/env python3
"""Executable authoring fixture for LYFE's paired Survival opening.

This is design validation, not authoritative engine code. It combines the
selected hourly light curve, a bounded gas field, individual reserve/structure
state, micronutrient-quota acquisition, age throughput, deterministic fission,
senescence, mutation income, and the first player speciation. It intentionally
omits movement, predation, detailed remnant chemistry, generated-map repair,
and the production fixed-point/RNG implementation.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import argparse
import hashlib
import json
import math
import statistics


HOURS_PER_DAY = 24
GRID_WIDTH = 15
GRID_HEIGHT = 15
REFERENCE_SEED = 0x4C594645  # ASCII-ish "LYFE"
FOUNDER_COUNT = 100
INITIAL_RESERVE = 5_000
RESERVE_CAPACITY = 10_000
GROWTH_FLOOR = 4_000
REPRODUCTION_RESERVE_GATE = 8_500
REPRODUCTION_WORK = 500
REPRODUCTION_HEALTH_GATE = 0.60
MATURE_STRUCTURE = 1_000
FISSION_STRUCTURE = 2_000
BASE_MAINTENANCE = 50
SENESCENCE_ONSET = 720
SENESCENCE_DECLINE = 720
SENESCENCE_ESCALATION = 168
MUTATION_DENOMINATOR = 750.0
SPECIATION_COOLDOWN = 168
HORIZON_HOURS = 45 * HOURS_PER_DAY

SULFUR_LIGHT_EXTENTS = (
    194, 569, 905, 1180, 1374, 1475,
    1475, 1374, 1180, 905, 569, 194,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
)

GAS_RATES = {
    "H2": (0.0025, 0.0001),
    "H2S": (0.0050, 0.00025),
    "SO2": (0.0100, 0.00050),
    "CO2": (0.0002, 0.10000),
    "NH3": (0.0005, 0.00100),
}

HYDROGEN_SOURCES = {
    "H2": 250_000.0,
    "H2S": 100_000.0,
    "SO2": 50_000.0,
    "CO2": 150_000.0,
    "NH3": 5_000.0,
}

SULFUR_SOURCES = {
    "H2": 50_000.0,
    "H2S": 200_000.0,
    "SO2": 50_000.0,
    "CO2": 150_000.0,
    "NH3": 5_000.0,
}


def keyed_u64(seed: int, *parts: object) -> int:
    payload = ":".join(str(part) for part in (seed, *parts)).encode("utf-8")
    return int.from_bytes(hashlib.blake2b(payload, digest_size=8).digest(), "big")


def keyed_uniform(seed: int, *parts: object) -> float:
    return keyed_u64(seed, *parts) / 2**64


def keyed_integer(seed: int, low: int, high: int, *parts: object) -> int:
    return low + keyed_u64(seed, *parts) % (high - low + 1)


@dataclass
class GasField:
    stock: dict[str, list[float]]
    sources: dict[str, list[float]]

    @staticmethod
    def index(x: int, y: int) -> int:
        return y * GRID_WIDTH + x

    def clone(self) -> "GasField":
        return GasField(
            {name: values.copy() for name, values in self.stock.items()},
            self.sources,
        )

    def step(self) -> None:
        tile_count = GRID_WIDTH * GRID_HEIGHT
        for gas, values in self.stock.items():
            sink, exchange = GAS_RATES[gas]
            post = [
                (values[i] + self.sources[gas][i]) * (1.0 - sink)
                for i in range(tile_count)
            ]
            delta = [0.0] * tile_count
            for y in range(GRID_HEIGHT):
                for x in range(GRID_WIDTH):
                    here = self.index(x, y)
                    right = self.index((x + 1) % GRID_WIDTH, y)
                    flow = exchange * (post[here] - post[right])
                    delta[here] -= flow
                    delta[right] += flow
                    if y + 1 < GRID_HEIGHT:
                        down = self.index(x, y + 1)
                        flow = exchange * (post[here] - post[down])
                        delta[here] -= flow
                        delta[down] += flow
            self.stock[gas] = [post[i] + delta[i] for i in range(tile_count)]


@dataclass
class Organism:
    organism_id: int
    species_id: int
    tile: str
    metabolism: str
    reserve: int = INITIAL_RESERVE
    structure: int = MATURE_STRUCTURE
    age: int = 0
    extra_quota: int = 0
    reproduction_not_before_tick: int = 0
    reproduction_count: int = 0
    regulation: bool = False


@dataclass
class Species:
    species_id: int
    name: str
    metabolism: str
    controlled: bool
    mutation_balance: float = 0.0
    speciation_not_before_tick: int = 0
    regulation: bool = False
    parent_id: int | None = None


@dataclass
class Remnants:
    entities: int = 0
    reserve: float = 0.0
    structure: float = 0.0
    labile_organic: float = 0.0
    structural_decay_extents: float = 0.0

    def decay_one_hour(self) -> None:
        reserve_loss = self.reserve * (1.0 - 2.0 ** (-1.0 / 336.0))
        structure_loss = self.structure * (1.0 - 2.0 ** (-1.0 / 720.0))
        labile_loss = self.labile_organic * (1.0 - 2.0 ** (-1.0 / 336.0))
        self.reserve -= reserve_loss
        self.structure -= structure_loss
        self.labile_organic += reserve_loss + 4.0 * structure_loss - labile_loss
        self.structural_decay_extents += structure_loss


@dataclass
class Result:
    seed: int
    milestones: dict[str, int | None]
    populations: dict[str, dict[int, int]]
    minimum_root_populations: dict[str, int]
    deaths: dict[str, int]
    death_records_with_multiple_risks: int
    final_species: dict[str, dict[str, float | int | bool | None]]
    initial_gases: dict[str, dict[str, int]]
    final_gases: dict[str, dict[str, int]]
    remnants: dict[str, float | int]
    autonomous_evaluations: int
    autonomous_material_rejections: int


def build_equilibrium_field() -> tuple[GasField, dict[str, int]]:
    tile_count = GRID_WIDTH * GRID_HEIGHT
    hydrogen_xy = (GRID_WIDTH // 2 - 1, GRID_HEIGHT // 2)
    sulfur_xy = (GRID_WIDTH // 2, GRID_HEIGHT // 2)
    sources = {gas: [0.0] * tile_count for gas in GAS_RATES}
    for gas in GAS_RATES:
        sources[gas][GasField.index(*hydrogen_xy)] = HYDROGEN_SOURCES[gas]
        sources[gas][GasField.index(*sulfur_xy)] = SULFUR_SOURCES[gas]
    # The diffuse boundary source holds the selected global CO2 background.
    background_source = 100_000_000 * 0.0002 / (1.0 - 0.0002)
    sources["CO2"] = [value + background_source for value in sources["CO2"]]

    stock = {gas: [0.0] * tile_count for gas in GAS_RATES}
    stock["CO2"] = [100_000_000.0] * tile_count
    # NH3 is a local volcanic reservoir rather than a stable global background.
    stock["NH3"][GasField.index(*hydrogen_xy)] = 10_000_000.0
    stock["NH3"][GasField.index(*sulfur_xy)] = 10_000_000.0
    field = GasField(stock, sources)
    iterations = 0
    for iterations in range(1, 20_001):
        before_h = field.stock["H2"][GasField.index(*hydrogen_xy)]
        field.step()
        after_h = field.stock["H2"][GasField.index(*hydrogen_xy)]
        if iterations >= 8_000 and abs(after_h - before_h) < 0.001:
            break
    return field, {"hydrogen": GasField.index(*hydrogen_xy), "sulfur": GasField.index(*sulfur_xy), "iterations": iterations}


def age_factor(age: int) -> float:
    if age <= SENESCENCE_ONSET:
        return 1.0
    decline = min(1.0, (age - SENESCENCE_ONSET) / SENESCENCE_DECLINE)
    return 1.0 - 0.5 * decline * decline


def age_throughput(age: int) -> float:
    return 0.5 + 0.5 * age_factor(age)


def senescence_chance(age: int) -> float:
    if age <= SENESCENCE_ONSET:
        return 0.0
    excess = age - SENESCENCE_ONSET
    return min(0.50, 0.0001 * (1.0 + excess / SENESCENCE_ESCALATION) ** 2)


def sulfur_stress(stock: float, chemical: str) -> tuple[int, float, float]:
    if chemical == "SO2":
        soft, hard = 2_500_000.0, 10_000_000.0
    elif chemical == "H2S":
        soft, hard = 20_000_000.0, 80_000_000.0
    else:
        raise ValueError(chemical)
    if stock <= soft:
        return 0, 1.0, 0.0
    if stock <= hard:
        severity = (stock - soft) / (hard - soft)
        return math.ceil(BASE_MAINTENANCE * severity * severity), 1.0 - 0.30 * severity * severity, 0.0
    overage = (stock - hard) / hard
    health = max(0.10, 0.70 / (1.0 + overage) ** 2)
    death = min(0.50, 0.01 * (1.0 + overage) ** 2)
    return BASE_MAINTENANCE, health, death


def condition(organism: Organism, so2: float, h2s: float) -> float:
    _, so2_factor, _ = sulfur_stress(so2, "SO2")
    _, h2s_factor, _ = sulfur_stress(h2s, "H2S")
    reserve = max(0.0, min(1.0, organism.reserve / RESERVE_CAPACITY))
    structure = max(0.0, min(1.0, organism.structure / MATURE_STRUCTURE))
    return reserve * structure * age_factor(organism.age) * so2_factor * h2s_factor


def allocate_extents(
    requests: list[tuple[Organism, int]], total_available: int, seed: int, tick: int, channel: str
) -> dict[int, int]:
    total_requested = sum(request for _, request in requests)
    if total_requested <= total_available:
        return {organism.organism_id: request for organism, request in requests}
    if total_available <= 0 or total_requested <= 0:
        return {organism.organism_id: 0 for organism, _ in requests}
    scale = total_available / total_requested
    grants = {
        organism.organism_id: math.floor(request * scale)
        for organism, request in requests
    }
    left = total_available - sum(grants.values())
    eligible = [
        organism
        for organism, request in requests
        if grants[organism.organism_id] < request
    ]
    eligible.sort(key=lambda organism: (keyed_u64(seed, channel, tick, organism.organism_id), organism.organism_id))
    for organism in eligible[:left]:
        grants[organism.organism_id] += 1
    return grants


def population_by_species(organisms: list[Organism]) -> dict[int, int]:
    result: dict[int, int] = {}
    for organism in organisms:
        result[organism.species_id] = result.get(organism.species_id, 0) + 1
    return result


def run(seed: int, equilibrium: GasField, tile_indices: dict[str, int]) -> Result:
    field = equilibrium.clone()
    hydrogen_index = tile_indices["hydrogen"]
    sulfur_index = tile_indices["sulfur"]
    phosphorus = {"hydrogen": 5_000_000, "sulfur": 5_000_000}
    species = {
        1: Species(1, "Hydrogen root", "hydrogen", False),
        2: Species(2, "Sulfur root", "sulfur", True),
    }
    organisms: list[Organism] = []
    next_organism_id = 1
    for species_id, tile, metabolism, quota in (
        (1, "hydrogen", "hydrogen", 57),
        (2, "sulfur", "sulfur", 55),
    ):
        for _ in range(FOUNDER_COUNT):
            organisms.append(
                Organism(
                    next_organism_id,
                    species_id,
                    tile,
                    metabolism,
                    reproduction_not_before_tick=24 + keyed_integer(
                        seed, 0, 3, "reproduction-cooldown", next_organism_id, 0
                    ),
                )
            )
            next_organism_id += 1

    initial_gases = {
        tile: {
            gas: round(field.stock[gas][index])
            for gas in ("H2", "H2S", "SO2", "CO2", "NH3")
        }
        for tile, index in (("hydrogen", hydrogen_index), ("sulfur", sulfur_index))
    }
    milestones: dict[str, int | None] = {
        "hydrogen_first_reproduction": None,
        "sulfur_first_reproduction": None,
        "hydrogen_40_mp": None,
        "sulfur_60_mp": None,
        "player_speciation": None,
        "post_speciation_7d": None,
    }
    snapshots: dict[str, dict[int, int]] = {}
    minimum_root = {"hydrogen": FOUNDER_COUNT, "sulfur": FOUNDER_COUNT}
    deaths = {"Senescence": 0, "ChemicalExposure": 0, "MaintenanceFailure": 0, "EnergyExhaustion": 0}
    multi_risk_deaths = 0
    remnants = Remnants()
    autonomous_evaluations = 0
    autonomous_rejections = 0
    pending_player_speciation = False
    controlled_species_id = 2
    sulfur_descendant_id: int | None = None

    for tick in range(HORIZON_HOURS):
        # Phase 0: apply the first player mutation decision at the boundary after
        # the controlled sulfur lineage can afford MetabolicRegulation.
        if pending_player_speciation:
            ancestor = species[controlled_species_id]
            candidates = [o for o in organisms if o.species_id == ancestor.species_id and o.tile == "sulfur"]
            count = max(1, math.floor(len(candidates) * 0.50))
            candidates.sort(key=lambda o: (keyed_u64(seed, "speciation-founder", tick, o.organism_id), o.organism_id))
            post_price = ancestor.mutation_balance - 60.0
            ancestor.mutation_balance = post_price
            ancestor.controlled = False
            ancestor.speciation_not_before_tick = tick + SPECIATION_COOLDOWN
            sulfur_descendant_id = max(species) + 1
            species[sulfur_descendant_id] = Species(
                sulfur_descendant_id,
                "Regulated sulfur descendant",
                "sulfur",
                True,
                post_price,
                tick + SPECIATION_COOLDOWN,
                True,
                ancestor.species_id,
            )
            for organism in candidates[:count]:
                organism.species_id = sulfur_descendant_id
                organism.regulation = True
            controlled_species_id = sulfur_descendant_id
            milestones["player_speciation"] = tick
            milestones["post_speciation_7d"] = tick + SPECIATION_COOLDOWN
            pending_player_speciation = False

        field.step()
        phosphorus["hydrogen"] += 250
        phosphorus["sulfur"] += 250
        remnants.decay_one_hour()

        # Phases 3-4: intrinsic death first. Movement is intentionally a no-op in
        # this local interaction fixture.
        survivors: list[Organism] = []
        for organism in organisms:
            organism.age += 1
            index = hydrogen_index if organism.tile == "hydrogen" else sulfur_index
            risks: list[tuple[str, float]] = []
            if organism.reserve <= 0:
                risks.append(("EnergyExhaustion", 1.0))
            senescence = senescence_chance(organism.age)
            if senescence > 0:
                risks.append(("Senescence", senescence))
            for chemical in ("SO2", "H2S"):
                _, _, chance = sulfur_stress(field.stock[chemical][index], chemical)
                if chance > 0:
                    risks.append(("ChemicalExposure", chance))
            triggered = [
                (name, keyed_uniform(seed, "death", name, tick, organism.organism_id) < chance)
                for name, chance in risks
            ]
            selected = next((name for name, did_trigger in triggered if did_trigger), None)
            if selected is not None:
                deaths[selected] += 1
                if len(risks) > 1:
                    multi_risk_deaths += 1
                remnants.entities += 1
                remnants.reserve += organism.reserve
                remnants.structure += organism.structure
            else:
                survivors.append(organism)
        organisms = survivors

        # Phase 5: founders remain in BaselineActivity. MetabolicRegulation adds
        # its constitutive cost; the active founding pathway cannot be suppressed.
        for tile, index in (("hydrogen", hydrogen_index), ("sulfur", sulfur_index)):
            tile_organisms = [o for o in organisms if o.tile == tile]
            if not tile_organisms:
                continue
            capture_requests: list[tuple[Organism, int]] = []
            for organism in tile_organisms:
                so2_cost, _, _ = sulfur_stress(field.stock["SO2"][index], "SO2")
                h2s_cost, _, _ = sulfur_stress(field.stock["H2S"][index], "H2S")
                upkeep = BASE_MAINTENANCE + so2_cost + h2s_cost + (5 if organism.regulation else 0)
                possible_growth = (4 if tile == "sulfur" else 3)
                possible_growth = math.floor(possible_growth * age_throughput(organism.age))
                output_need = max(0, RESERVE_CAPACITY - organism.reserve + upkeep + 100 * possible_growth)
                if tile == "hydrogen":
                    request = min(math.floor(200 * age_throughput(organism.age)), math.ceil(output_need / 2))
                else:
                    request = min(math.floor(SULFUR_LIGHT_EXTENTS[tick % 24] * age_throughput(organism.age)), output_need)
                capture_requests.append((organism, max(0, request)))

            if tile == "hydrogen":
                available_extents = min(
                    math.floor(field.stock["H2"][index] / 4),
                    math.floor(field.stock["CO2"][index] / 2),
                )
                capture_grants = allocate_extents(capture_requests, available_extents, seed, tick, "hydrogen-capture")
                h2_per_extent, co2_per_extent, reserve_per_extent = 4, 2, 2
            else:
                available_extents = min(
                    math.floor(field.stock["H2S"][index] / 2),
                    math.floor(field.stock["CO2"][index]),
                )
                capture_grants = allocate_extents(capture_requests, available_extents, seed, tick, "sulfur-capture")
                h2_per_extent, co2_per_extent, reserve_per_extent = 0, 1, 1

            capture_total = sum(capture_grants.values())
            if tile == "hydrogen":
                field.stock["H2"][index] -= h2_per_extent * capture_total
            else:
                field.stock["H2S"][index] -= 2 * capture_total
            field.stock["CO2"][index] -= co2_per_extent * capture_total
            for organism in tile_organisms:
                organism.reserve = min(
                    RESERVE_CAPACITY,
                    organism.reserve + reserve_per_extent * capture_grants[organism.organism_id],
                )

            # Phase 7: maintenance, then optional whole-unit structural assembly.
            alive_after_maintenance: list[Organism] = []
            growth_requests: list[tuple[Organism, int]] = []
            for organism in tile_organisms:
                so2_cost, _, _ = sulfur_stress(field.stock["SO2"][index], "SO2")
                h2s_cost, _, _ = sulfur_stress(field.stock["H2S"][index], "H2S")
                upkeep = BASE_MAINTENANCE + so2_cost + h2s_cost + (5 if organism.regulation else 0)
                if organism.reserve < upkeep:
                    deaths["MaintenanceFailure"] += 1
                    risks = int(senescence_chance(organism.age) > 0)
                    multi_risk_deaths += int(risks > 0)
                    remnants.entities += 1
                    remnants.reserve += organism.reserve
                    remnants.structure += organism.structure
                    continue
                organism.reserve -= upkeep
                ceiling = 4 if tile == "sulfur" else 3
                ceiling = math.floor(ceiling * age_throughput(organism.age))
                affordable = max(0, (organism.reserve - GROWTH_FLOOR) // 100)
                growth_requests.append((organism, min(ceiling, affordable)))
                alive_after_maintenance.append(organism)

            dead_ids = {o.organism_id for o in tile_organisms} - {o.organism_id for o in alive_after_maintenance}
            if dead_ids:
                organisms = [o for o in organisms if o.organism_id not in dead_ids]
            available_growth = min(
                sum(request for _, request in growth_requests),
                math.floor(field.stock["NH3"][index] / 20),
                math.floor(field.stock["H2S"][index]),
                phosphorus[tile] // 2,
            )
            growth_grants = allocate_extents(growth_requests, available_growth, seed, tick, f"{tile}-growth")
            growth_total = sum(growth_grants.values())
            field.stock["NH3"][index] -= 20 * growth_total
            field.stock["H2S"][index] -= growth_total
            phosphorus[tile] -= 2 * growth_total
            for organism in alive_after_maintenance:
                growth = growth_grants.get(organism.organism_id, 0)
                organism.reserve -= 100 * growth
                organism.structure += growth

        # Primitive passive micronutrient acquisition. The total count is a
        # sufficient abstraction here because all required opening stocks are ample.
        for organism in organisms:
            quota = 57 if organism.metabolism == "hydrogen" else 55
            if organism.extra_quota < quota and keyed_uniform(seed, "micronutrient", tick, organism.organism_id) < 0.5:
                organism.extra_quota += 1

        # Phase 8: deterministic reproduction once every gate is true.
        births: list[Organism] = []
        for organism in organisms:
            index = hydrogen_index if organism.tile == "hydrogen" else sulfur_index
            quota = 57 if organism.metabolism == "hydrogen" else 55
            health = condition(organism, field.stock["SO2"][index], field.stock["H2S"][index])
            if (
                tick >= organism.reproduction_not_before_tick
                and health >= REPRODUCTION_HEALTH_GATE
                and organism.structure >= FISSION_STRUCTURE
                and organism.extra_quota >= quota
                and organism.reserve >= REPRODUCTION_RESERVE_GATE
            ):
                root_key = f"{organism.metabolism}_first_reproduction"
                if milestones[root_key] is None and organism.species_id in (1, 2):
                    milestones[root_key] = tick + 1
                organism.reserve -= REPRODUCTION_WORK
                child_reserve = organism.reserve // 2
                organism.reserve -= child_reserve
                child_structure = organism.structure // 2
                organism.structure -= child_structure
                organism.extra_quota -= quota
                organism.reproduction_count += 1
                organism.reproduction_not_before_tick = tick + 1 + 24 + keyed_integer(
                    seed, 0, 3, "reproduction-cooldown", organism.organism_id, organism.reproduction_count
                )
                child = Organism(
                    next_organism_id,
                    organism.species_id,
                    organism.tile,
                    organism.metabolism,
                    child_reserve,
                    child_structure,
                    0,
                    0,
                    tick + 1 + 24 + keyed_integer(seed, 0, 3, "reproduction-cooldown", next_organism_id, 0),
                    0,
                    organism.regulation,
                )
                births.append(child)
                next_organism_id += 1
        organisms.extend(births)

        # Phase 10: aggregate health and accrue mutation income equally for all
        # authority types. Autonomous candidates needing organic substrate are
        # evaluated daily and rejected while their material opportunity is absent.
        populations = population_by_species(organisms)
        for species_id, spec in species.items():
            members = [o for o in organisms if o.species_id == species_id]
            if not members:
                continue
            index = hydrogen_index if spec.metabolism == "hydrogen" else sulfur_index
            average_health = statistics.fmean(
                condition(o, field.stock["SO2"][index], field.stock["H2S"][index]) for o in members
            )
            effective_population = 100.0 * math.log2(1.0 + len(members) / 100.0)
            spec.mutation_balance += effective_population * average_health / MUTATION_DENOMINATOR
            if not spec.controlled and tick % 24 == 23:
                autonomous_evaluations += 1
                if spec.mutation_balance >= 40.0 and remnants.labile_organic < 120.0:
                    autonomous_rejections += 1

        if milestones["hydrogen_40_mp"] is None and species[1].mutation_balance >= 40.0:
            milestones["hydrogen_40_mp"] = tick + 1
        if milestones["sulfur_60_mp"] is None and species[2].mutation_balance >= 60.0:
            milestones["sulfur_60_mp"] = tick + 1
            pending_player_speciation = True

        root_pops = population_by_species(organisms)
        minimum_root["hydrogen"] = min(minimum_root["hydrogen"], root_pops.get(1, 0))
        # After player speciation, root 2 is expected to give up half its members.
        if milestones["player_speciation"] is None:
            minimum_root["sulfur"] = min(minimum_root["sulfur"], root_pops.get(2, 0))
        for name, hour in (
            ("day_14", 14 * 24),
            ("day_30", 30 * 24),
            ("day_45", 45 * 24),
        ):
            if tick + 1 == hour:
                snapshots[name] = root_pops.copy()
        if milestones["post_speciation_7d"] == tick + 1:
            snapshots["post_speciation_7d"] = root_pops.copy()

    final_gases = {
        tile: {
            gas: round(field.stock[gas][index])
            for gas in ("H2", "H2S", "SO2", "CO2", "NH3")
        }
        for tile, index in (("hydrogen", hydrogen_index), ("sulfur", sulfur_index))
    }
    final_species: dict[str, dict[str, float | int | bool | None]] = {}
    for species_id, spec in species.items():
        members = [o for o in organisms if o.species_id == species_id]
        index = hydrogen_index if spec.metabolism == "hydrogen" else sulfur_index
        average_health = (
            statistics.fmean(condition(o, field.stock["SO2"][index], field.stock["H2S"][index]) for o in members)
            if members else 0.0
        )
        final_species[spec.name] = {
            "species_id": species_id,
            "parent_id": spec.parent_id,
            "controlled": spec.controlled,
            "population": len(members),
            "average_health": average_health,
            "mutation_balance": spec.mutation_balance,
        }
    return Result(
        seed,
        milestones,
        snapshots,
        minimum_root,
        deaths,
        multi_risk_deaths,
        final_species,
        initial_gases,
        final_gases,
        {
            "entities_created": remnants.entities,
            "reserve_remaining": remnants.reserve,
            "structure_remaining": remnants.structure,
            "labile_organic": remnants.labile_organic,
            "structural_decay_extents": remnants.structural_decay_extents,
        },
        autonomous_evaluations,
        autonomous_rejections,
    )


def validate(results: list[Result]) -> list[str]:
    failures: list[str] = []
    milestone_bands = {
        "hydrogen_first_reproduction": (330, 340),
        "sulfur_first_reproduction": (250, 275),
        "hydrogen_40_mp": (335, 350),
        "sulfur_60_mp": (480, 520),
        "player_speciation": (480, 520),
        "post_speciation_7d": (648, 688),
    }
    for result in results:
        for name, (low, high) in milestone_bands.items():
            value = result.milestones[name]
            if value is None or not low <= value <= high:
                failures.append(f"seed {result.seed}: {name}={value}, expected {low}..{high}")
        if result.minimum_root_populations["hydrogen"] < 95:
            failures.append(f"seed {result.seed}: hydrogen opening survival below 95%")
        if result.minimum_root_populations["sulfur"] < 95:
            failures.append(f"seed {result.seed}: sulfur opening survival below 95%")
        post = result.populations.get("post_speciation_7d", {})
        if post.get(2, 0) < 45 or post.get(3, 0) < 45:
            failures.append(f"seed {result.seed}: a sulfur branch failed the seven-day viability floor")
        if result.deaths["ChemicalExposure"] != 0 or result.deaths["MaintenanceFailure"] != 0:
            failures.append(f"seed {result.seed}: adapted opening suffered acute chemical or maintenance death")
        day_45 = sum(result.populations["day_45"].values())
        if not 700 <= day_45 <= 850:
            failures.append(f"seed {result.seed}: day-45 population {day_45} outside 700..850")
        if result.final_gases["hydrogen"]["NH3"] >= 1_000 or result.final_gases["sulfur"]["NH3"] >= 1_000:
            failures.append(f"seed {result.seed}: fixed nitrogen did not become limiting")
        if not 20 <= result.deaths["Senescence"] <= 50:
            failures.append(f"seed {result.seed}: senescence deaths outside 20..50")
    return failures


def summarize(results: list[Result], equilibrium_iterations: int) -> dict[str, object]:
    milestone_names = results[0].milestones.keys()
    milestone_summary = {}
    for name in milestone_names:
        values = [result.milestones[name] for result in results if result.milestones[name] is not None]
        milestone_summary[name] = {
            "min": min(values) if values else None,
            "median": statistics.median(values) if values else None,
            "max": max(values) if values else None,
        }
    failures = validate(results)
    return {
        "acceptance": "PASS" if not failures else "FAIL",
        "acceptance_failures": failures,
        "seeds": len(results),
        "equilibrium_iterations": equilibrium_iterations,
        "milestones": milestone_summary,
        "minimum_pre_speciation_population": {
            name: min(result.minimum_root_populations[name] for result in results)
            for name in ("hydrogen", "sulfur")
        },
        "day_45_total_population": {
            "min": min(sum(result.populations["day_45"].values()) for result in results),
            "median": statistics.median(sum(result.populations["day_45"].values()) for result in results),
            "max": max(sum(result.populations["day_45"].values()) for result in results),
        },
        "deaths": {
            name: {
                "min": min(result.deaths[name] for result in results),
                "median": statistics.median(result.deaths[name] for result in results),
                "max": max(result.deaths[name] for result in results),
            }
            for name in results[0].deaths
        },
        "autonomous_material_rejections": {
            "min": min(result.autonomous_material_rejections for result in results),
            "median": statistics.median(result.autonomous_material_rejections for result in results),
            "max": max(result.autonomous_material_rejections for result in results),
        },
        "reference": results[0].__dict__,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=8)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    equilibrium, indices = build_equilibrium_field()
    results = [run(REFERENCE_SEED + offset, equilibrium, indices) for offset in range(args.seeds)]
    report = summarize(results, indices["iterations"])
    if args.json:
        print(json.dumps(report, indent=2, sort_keys=True))
        return
    print(f"paired-field equilibrium iterations: {indices['iterations']}")
    print(f"seeds: {args.seeds}")
    print(f"acceptance: {report['acceptance']}")
    for name, values in report["milestones"].items():
        print(f"{name:32s} {values['min']} / {values['median']} / {values['max']}")
    print("minimum pre-speciation population:", report["minimum_pre_speciation_population"])
    print("day-45 total population:", report["day_45_total_population"])
    print("deaths:", report["deaths"])
    reference = results[0]
    print("reference initial gases:", reference.initial_gases)
    print("reference final gases:", reference.final_gases)
    print("reference final species:", reference.final_species)
    print("reference remnants:", reference.remnants)
    print(
        "reference autonomous evaluations/rejections:",
        reference.autonomous_evaluations,
        reference.autonomous_material_rejections,
    )


if __name__ == "__main__":
    main()
