#!/usr/bin/env python3
"""Exact integer probe for the first LYFE founder setup allocations."""

from __future__ import annotations

from dataclasses import dataclass
import json


SCALE = 1_000_000


@dataclass(frozen=True)
class Founder:
    name: str
    maximum_extents: int
    efficiency_q: int
    h2s_soft: int
    h2s_hard: int
    so2_soft: int
    so2_hard: int


@dataclass(frozen=True)
class Package:
    name: str
    efficiency_multiplier_q: int
    tolerance_multiplier_q: int


FOUNDERS = (
    Founder("hydrogen", 250, 800_000, 20_000_000, 80_000_000, 2_500_000, 10_000_000),
    Founder("sulfur", 2_083, 950_000, 50_000_000, 200_000_000, 2_500_000, 10_000_000),
)

PACKAGES = (
    Package("high-throughput", 1_062_500, 800_000),
    Package("balanced", 1_000_000, 1_000_000),
    Package("stress-tolerant", 937_500, 1_250_000),
)


def multiply_q(left: int, right: int) -> int:
    quotient, remainder = divmod(left * right, SCALE)
    if remainder > SCALE // 2 or remainder == SCALE // 2 and quotient % 2:
        quotient += 1
    return quotient


def ratio_q(numerator: int, denominator: int) -> int:
    quotient, remainder = divmod(numerator * SCALE, denominator)
    if remainder * 2 > denominator or remainder * 2 == denominator and quotient % 2:
        quotient += 1
    return min(SCALE, quotient)


def chemical_factor(exposure: int, soft: int, hard: int) -> int:
    if exposure <= soft:
        return SCALE
    if exposure <= hard:
        severity = ratio_q(exposure - soft, hard - soft)
        return SCALE - multiply_q(300_000, multiply_q(severity, severity))
    overage = ratio_q(exposure - hard, hard)
    one_plus = SCALE + overage
    return max(100_000, (700_000 * SCALE * SCALE + one_plus * one_plus // 2) //
               (one_plus * one_plus))


def evaluate(founder: Founder, package: Package) -> dict[str, int | str]:
    efficiency = min(SCALE, multiply_q(founder.efficiency_q, package.efficiency_multiplier_q))
    h2s_soft = multiply_q(founder.h2s_soft, package.tolerance_multiplier_q)
    h2s_hard = multiply_q(founder.h2s_hard, package.tolerance_multiplier_q)
    so2_soft = multiply_q(founder.so2_soft, package.tolerance_multiplier_q)
    so2_hard = multiply_q(founder.so2_hard, package.tolerance_multiplier_q)
    h2s_factor = chemical_factor(20_000_000, h2s_soft, h2s_hard)
    so2_factor = chemical_factor(5_000_000, so2_soft, so2_hard)
    environmental = multiply_q(h2s_factor, so2_factor)
    elevated_environmental = multiply_q(
        chemical_factor(60_000_000, h2s_soft, h2s_hard),
        chemical_factor(8_000_000, so2_soft, so2_hard),
    )
    return {
        "founder": founder.name,
        "package": package.name,
        "efficiencyQ": efficiency,
        "fullOpportunityExtents": founder.maximum_extents * efficiency // SCALE,
        "h2sSoftQ": h2s_soft,
        "h2sHardQ": h2s_hard,
        "so2SoftQ": so2_soft,
        "so2HardQ": so2_hard,
        "openingEnvironmentalFactorQ": environmental,
        "openingHealthAtHalfReserveQ": multiply_q(500_000, environmental),
        "elevatedEnvironmentalFactorQ": elevated_environmental,
    }


def validate(rows: list[dict[str, int | str]]) -> None:
    for founder in ("hydrogen", "sulfur"):
        group = [row for row in rows if row["founder"] == founder]
        assert [row["package"] for row in group] == [package.name for package in PACKAGES]
        assert group[0]["fullOpportunityExtents"] > group[1]["fullOpportunityExtents"]
        assert group[1]["fullOpportunityExtents"] > group[2]["fullOpportunityExtents"]
        assert group[0]["elevatedEnvironmentalFactorQ"] < group[1]["elevatedEnvironmentalFactorQ"]
        assert group[1]["elevatedEnvironmentalFactorQ"] < group[2]["elevatedEnvironmentalFactorQ"]
        assert all(row["openingEnvironmentalFactorQ"] >= 900_000 for row in group)
        for row in group:
            assert row["h2sSoftQ"] < row["h2sHardQ"]
            assert row["so2SoftQ"] < row["so2HardQ"]


def main() -> None:
    rows = [evaluate(founder, package) for founder in FOUNDERS for package in PACKAGES]
    validate(rows)
    print(json.dumps(rows, indent=2))


if __name__ == "__main__":
    main()

