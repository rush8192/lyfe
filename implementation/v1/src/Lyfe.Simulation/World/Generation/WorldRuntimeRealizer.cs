using System.Collections.Immutable;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.World.Generation;

internal static class WorldRuntimeRealizer
{
    public static CompiledWorldRules Realize(
        CompiledWorldRules rules,
        RootRandomSeed rootSeed)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.WorldProfile.Generator is null)
        {
            return rules;
        }

        if (rules.GeneratedWorld is not null)
        {
            return rules;
        }

        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, rootSeed);
        var gas = rules.WorldProfile.GasEnvironment;
        var hydrogenSlot = FindDominantEmissionProfile(gas, ResourceId.From(1));
        var sulfurSlot = FindDominantEmissionProfile(gas, ResourceId.From(10));
        var tiles = generated.Tiles.Select(tile => new CompiledTileProfile(
            tile.TileIndex,
            tile.X,
            tile.Y,
            tile.ElevationMeters,
            tile.BaselineVolcanismQ,
            tile.BaselineVolcanismQ == 0
                ? -1
                : tile.StartingRole == StartingTileRole.Sulfur
                    ? sulfurSlot
                    : hydrogenSlot,
            tile.ResourceQuantitiesByDenseSlot)).ToImmutableArray();
        var profile = rules.WorldProfile with { Tiles = tiles };
        return new CompiledWorldRules(
            rules.RulePack,
            rules.Scenario,
            rules.Identity,
            profile,
            rules.TickDurationHours,
            generated);
    }

    private static int FindDominantEmissionProfile(
        CompiledGasEnvironment gas,
        ResourceId resourceId)
    {
        var gasSlot = -1;
        for (var index = 0; index < gas.Gases.Length; index++)
        {
            if (gas.Gases[index].Resource.Id == resourceId)
            {
                gasSlot = index;
                break;
            }
        }

        if (gasSlot < 0 || gas.EmissionProfiles.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Generated worlds require an emission profile containing resource {resourceId}.");
        }

        var selected = 0;
        for (var index = 1; index < gas.EmissionProfiles.Length; index++)
        {
            if (gas.EmissionProfiles[index].FullActivityQuantitiesPerHourByGasSlot[gasSlot] >
                gas.EmissionProfiles[selected].FullActivityQuantitiesPerHourByGasSlot[gasSlot])
            {
                selected = index;
            }
        }
        return selected;
    }
}
