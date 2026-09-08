using Lyfe.Simulation.Rules.Identity;

namespace Lyfe.Simulation.Rules.Runtime;

public enum BiologicalForm : byte
{
    Inorganic = 1,
    Organic = 2,
    Boundary = 3,
}

public enum EnvironmentalPhase : byte
{
    Gas = 1,
    Dissolved = 2,
    Boundary = 3,
    Particulate = 4,
}

public enum ChemicalElement : byte
{
    Hydrogen = 1,
    Carbon = 6,
    Nitrogen = 7,
    Oxygen = 8,
    Fluorine = 9,
    Sodium = 11,
    Magnesium = 12,
    Phosphorus = 15,
    Sulfur = 16,
    Potassium = 19,
    Calcium = 20,
    Manganese = 25,
    Iron = 26,
    Cobalt = 27,
    Nickel = 28,
    Copper = 29,
    Zinc = 30,
    Selenium = 34,
    Molybdenum = 42,
    Iodine = 53,
}

public enum ProcessKind : byte
{
    ExternalEnergyCapture = 1,
    ParticulateDigestion = 2,
    BiomassAssembly = 3,
    MandatoryMaintenance = 4,
}

public enum DefinitionKind : byte
{
    Resource = 1,
    Reaction = 2,
    FounderGenome = 3,
    Scenario = 4,
    Trait = 5,
    FounderAllocation = 6,
}

public readonly record struct ResourceHandle
{
    internal ResourceHandle(ResourceId id, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        Id = id;
        DenseSlot = denseSlot;
    }

    public ResourceId Id { get; }

    public int DenseSlot { get; }
}

public readonly record struct ReactionHandle
{
    internal ReactionHandle(ReactionId id, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        Id = id;
        DenseSlot = denseSlot;
    }

    public ReactionId Id { get; }

    public int DenseSlot { get; }
}

public readonly record struct FounderGenomeHandle
{
    internal FounderGenomeHandle(FounderGenomeId id, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        Id = id;
        DenseSlot = denseSlot;
    }

    public FounderGenomeId Id { get; }

    public int DenseSlot { get; }
}

public readonly record struct FounderAllocationHandle
{
    internal FounderAllocationHandle(FounderAllocationId id, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        Id = id;
        DenseSlot = denseSlot;
    }

    public FounderAllocationId Id { get; }

    public int DenseSlot { get; }
}

public readonly record struct ScenarioHandle
{
    internal ScenarioHandle(ScenarioId id, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        Id = id;
        DenseSlot = denseSlot;
    }

    public ScenarioId Id { get; }

    public int DenseSlot { get; }
}

public readonly record struct PhenotypeHandle
{
    internal PhenotypeHandle(FounderGenomeId founderGenomeId, int denseSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(denseSlot);
        FounderGenomeId = founderGenomeId;
        DenseSlot = denseSlot;
    }

    public FounderGenomeId FounderGenomeId { get; }

    public int DenseSlot { get; }
}
