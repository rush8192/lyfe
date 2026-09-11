import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  AcquisitionGateEvidenceSchema,
  AcquisitionGateReason,
  AcquisitionProcess,
  ActorWorldProjectionSchema,
  ExactResourceStockSchema,
  OrganismActionGateEvidenceSchema,
  OrganismActionGateReason,
  OrganismActionProcess,
  ResourceBiologicalForm,
  ResourceDefinitionSchema,
  ResourceEnvironmentalPhase,
  ResourceAcquisitionEvidenceSchema,
  ResourceFlowKind,
  ResourceFlowHistoryIntervalSchema,
  ResourceFlowContributorSchema,
  ResourceFlowProcess,
  ResourceFlowSchema,
  ReactionDefinitionSchema,
} from "../generated/lyfe/v1/projection_pb";
import {
  buildResourceFlowRows,
  buildResourceHistorySeries,
  buildResourceContributorRows,
  describeAcquisitionGate,
  describeOrganismActionGate,
  selectLiveTile,
  selectLimitingAcquisition,
} from "./ResourcePressurePanel";

describe("resource flow rows", () => {
  it("does not substitute another live tile for an explicitly selected stale tile", () => {
    const world = create(ActorWorldProjectionSchema, {
      controlledSpeciesId: 5n,
      tiles: [
        {
          tileId: 1,
          detail: {
            case: "live",
            value: { organisms: [{ organismId: 9n, speciesId: 5n }] },
          },
        },
        {
          tileId: 2,
          detail: {
            case: "reduced",
            value: { observedAtTick: 3n },
          },
        },
      ],
    });

    expect(selectLiveTile(world, 9n, 2)).toBeNull();
    expect(selectLiveTile(world, 9n, null)?.tileId).toBe(1);
  });

  it("names true reactions separately from environmental and masked biological contributors", () => {
    const rows = buildResourceContributorRows({
      resourceFlowContributors: [
        create(ResourceFlowContributorSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.ORGANISM_UPTAKE,
          process: ResourceFlowProcess.EXTERNAL_ENERGY_CAPTURE,
          reactionId: 2,
          speciesId: 9n,
          amountQ: 80n,
        }),
        create(ResourceFlowContributorSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.ENVIRONMENTAL_SOURCE,
          process: ResourceFlowProcess.ENVIRONMENTAL_GAS_SOURCE,
          amountQ: 100n,
        }),
        create(ResourceFlowContributorSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.ORGANISM_UPTAKE,
          process: ResourceFlowProcess.MICRONUTRIENT_UPTAKE,
          amountQ: 20n,
        }),
      ],
    }, 7, [create(ReactionDefinitionSchema, {
      reactionId: 2,
      stableKey: "reaction.test",
      displayName: "Hydrogen acetogenesis",
    })]);

    expect(rows).toEqual([
      { process: "Volcanic gas source", actor: "Environment", direction: "source", amountQ: 100n },
      { process: "Hydrogen acetogenesis", actor: "Species 9", direction: "organism uptake", amountQ: 80n },
      { process: "Micronutrient uptake", actor: "Other biological activity", direction: "organism uptake", amountQ: 20n },
    ]);
  });

  it("reconstructs exact historical stocks backward from current stock and sparse flows", () => {
    const series = buildResourceHistorySeries({
      resourceStocks: [create(ExactResourceStockSchema, { resourceId: 7, quantityQ: 1_040n })],
      resourceFlowHistory: [
        create(ResourceFlowHistoryIntervalSchema, {
          completedTick: 1n,
          endSimulatedHour: 1n,
          periodHours: 1,
          resourceFlows: [
            create(ResourceFlowSchema, {
              resourceId: 7,
              kind: ResourceFlowKind.ENVIRONMENTAL_SOURCE,
              amountQ: 100n,
            }),
          ],
        }),
        create(ResourceFlowHistoryIntervalSchema, {
          completedTick: 2n,
          endSimulatedHour: 2n,
          periodHours: 1,
          resourceFlows: [
            create(ResourceFlowSchema, {
              resourceId: 7,
              kind: ResourceFlowKind.ORGANISM_UPTAKE,
              amountQ: 60n,
            }),
          ],
        }),
      ],
    }, 7);

    expect(series.map((point) => ({
      hour: point.simulatedHour,
      stock: point.stockQ,
      net: point.netQ,
    }))).toEqual([
      { hour: 0n, stock: 1_000n, net: 0n },
      { hour: 1n, stock: 1_100n, net: 100n },
      { hour: 2n, stock: 1_040n, net: -60n },
    ]);
  });

  it("keeps gross balanced-ledger directions visible instead of inferring flow from net stock", () => {
    const rows = buildResourceFlowRows({
      resourceStocks: [create(ExactResourceStockSchema, { resourceId: 7, quantityQ: 10_000n })],
      resourceFlows: [
        create(ResourceFlowSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.ENVIRONMENTAL_SOURCE,
          amountQ: 500n,
        }),
        create(ResourceFlowSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.NEIGHBOR_EXCHANGE_OUT,
          amountQ: 200n,
        }),
        create(ResourceFlowSchema, {
          resourceId: 7,
          kind: ResourceFlowKind.ORGANISM_UPTAKE,
          amountQ: 300n,
        }),
      ],
    }, [create(ResourceDefinitionSchema, {
      resourceId: 7,
      stableKey: "resource.hydrogen-gas",
      displayName: "Hydrogen gas",
      biologicalForm: ResourceBiologicalForm.INORGANIC,
      environmentalPhase: ResourceEnvironmentalPhase.GAS,
    })]);

    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({
      displayName: "Hydrogen gas",
      descriptor: "inorganic · gas",
      stockQ: 10_000n,
      inflowQ: 500n,
      outflowQ: 500n,
      netQ: 0n,
    });
    expect(rows[0].contributions.map((value) => value.label)).toEqual([
      "source",
      "neighbor out",
      "organism uptake",
    ]);
  });

  it("names only an evidenced constraint and ranks its proportional shortfall", () => {
    const coLimited = create(ResourceAcquisitionEvidenceSchema, {
      resourceId: 2,
      requestedQ: 100n,
      grantedQ: 10n,
    });
    const tileLimited = create(ResourceAcquisitionEvidenceSchema, {
      resourceId: 1,
      requestedQ: 100n,
      grantedQ: 40n,
      tileSupplyConstrained: true,
    });
    const contentionLimited = create(ResourceAcquisitionEvidenceSchema, {
      resourceId: 3,
      requestedQ: 100n,
      grantedQ: 60n,
      claimContentionConstrained: true,
    });

    expect(selectLimitingAcquisition([
      coLimited,
      contentionLimited,
      tileLimited,
    ])).toBe(tileLimited);
    expect(selectLimitingAcquisition([coLimited])).toBeUndefined();
  });

  it("explains engine-authored capacity and environmental gates", () => {
    expect(describeAcquisitionGate(create(AcquisitionGateEvidenceSchema, {
      process: AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE,
      reason: AcquisitionGateReason.INTERNAL_CAPACITY,
      availableQ: 0n,
      requiredQ: 2n,
    }))).toBe(
      "Energy capture did not run: 0 q reserve room was available, " +
      "but one reaction extent requires 2 q.",
    );
    expect(describeAcquisitionGate(create(AcquisitionGateEvidenceSchema, {
      process: AcquisitionProcess.EXTERNAL_ENERGY_CAPTURE,
      reason: AcquisitionGateReason.INACCESSIBLE_LIGHT,
    }))).toContain("accessible light");
    expect(describeAcquisitionGate(create(AcquisitionGateEvidenceSchema, {
      process: AcquisitionProcess.SCAVENGING,
      reason: AcquisitionGateReason.COOLDOWN_ACTIVE,
      clearsAtTick: 9n,
    }))).toBe(
      "Scavenging did not run: cooldown remains active until tick 9.",
    );
    expect(describeAcquisitionGate(create(AcquisitionGateEvidenceSchema, {
      process: AcquisitionProcess.SCAVENGING,
      reason: AcquisitionGateReason.INSUFFICIENT_ACTION_ENERGY,
      availableQ: 12n,
      requiredQ: 25n,
    }))).toContain("12 q reserve energy");

    const cobalt = create(ResourceDefinitionSchema, {
      resourceId: 31,
      stableKey: "resource.cobalt",
      displayName: "Cobalt",
      biologicalForm: ResourceBiologicalForm.INORGANIC,
      environmentalPhase: ResourceEnvironmentalPhase.DISSOLVED,
    });
    expect(describeOrganismActionGate(create(OrganismActionGateEvidenceSchema, {
      process: OrganismActionProcess.REPRODUCTION,
      reason: OrganismActionGateReason.COOLDOWN_ACTIVE,
      clearsAtTick: 27n,
    }), [cobalt])).toContain("until tick 27");
    expect(describeOrganismActionGate(create(OrganismActionGateEvidenceSchema, {
      process: OrganismActionProcess.REPRODUCTION,
      reason: OrganismActionGateReason.OFFSPRING_MICRONUTRIENT_QUOTA_MISSING,
      availableQ: 0n,
      requiredQ: 1n,
      resourceId: 31,
    }), [cobalt])).toContain("free Cobalt for offspring");
    expect(describeOrganismActionGate(create(OrganismActionGateEvidenceSchema, {
      process: OrganismActionProcess.BIOMASS_GROWTH,
      reason: OrganismActionGateReason.BEHAVIOR_SUPPRESSED,
    }), [cobalt])).toContain("conserving behavior");
  });
});
