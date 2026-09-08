using System.Collections.Immutable;
using System.Security.Cryptography;
using Google.Protobuf;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server.Evolution;

public sealed class EvolutionProtocolService
{
    private const int MaximumRememberedCommands = 1_024;
    private readonly object gate = new();
    private readonly Dictionary<string, CachedCommand> commands =
        new(StringComparer.Ordinal);
    private readonly Queue<string> commandOrder = new();
    private ulong nextCommandOrder = 1;

    public Proto.EvolutionDecisionSurface CaptureDecisionSurface(WorldRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        lock (gate)
        {
            return CaptureDecisionSurfaceCore(runner);
        }
    }

    public Proto.SpeciationProposal Preview(
        WorldRunner runner,
        Proto.SpeciationProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var before = CaptureDecisionSurfaceCore(runner);
            var command = ToCommand(runner, request);
            return ToProtocol(
                runner.PreviewSpeciation(command),
                before,
                runner.CaptureSnapshot().CompletedTick);
        }
    }

    public Proto.ApplySpeciationResponse Apply(
        WorldRunner runner,
        Proto.ApplySpeciationRequest request)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientCommandId) ||
            request.ClientCommandId.Length > 128 ||
            request.Proposal is null)
        {
            throw new ArgumentException("A bounded client command ID and proposal are required.",
                nameof(request));
        }

        lock (gate)
        {
            var fingerprint = Convert.ToHexStringLower(
                SHA256.HashData(request.Proposal.ToByteArray()));
            if (commands.TryGetValue(request.ClientCommandId, out var cached))
            {
                if (!string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new EvolutionCommandConflictException(request.ClientCommandId);
                }
                return Proto.ApplySpeciationResponse.Parser.ParseFrom(cached.ResponseBytes);
            }

            var before = CaptureDecisionSurfaceCore(runner);
            var command = ToCommand(runner, request.Proposal);
            var result = runner.ApplySpeciation(command);
            var response = new Proto.ApplySpeciationResponse
            {
                ClientCommandId = request.ClientCommandId,
                CommandOrder = nextCommandOrder++,
                Proposal = ToProtocol(
                    result.Preview,
                    before,
                    runner.CaptureSnapshot().CompletedTick),
                Applied = result.Preview.Accepted,
                EventId = result.EventId,
                DescendantSpeciesId = result.DescendantSpeciesId.Value,
                DescendantGenomeId = result.DescendantGenomeId.Value,
                ApplicationTick = runner.CaptureSnapshot().CompletedTick,
                WorldRevision = result.WorldRevision,
            };
            Remember(
                request.ClientCommandId,
                new CachedCommand(fingerprint, response.ToByteArray()));
            return response;
        }
    }

    private static Proto.EvolutionDecisionSurface CaptureDecisionSurfaceCore(
        WorldRunner runner)
    {
        var publication = runner.CapturePublicationSnapshot();
        var controlledId = publication.Gameplay.ControlledSpeciesId;
        var species = publication.Species.Single(value => value.SpeciesId == controlledId);
        var acquired = species.AcquiredTraits.ToHashSet();
        var maximumComplexity = runner.Rules.RulePack.Traits
            .Where(trait => acquired.Contains(trait.Id) &&
                trait.MaximumChangeComplexity.HasValue)
            .Select(trait => trait.MaximumChangeComplexity ?? 0)
            .Aggregate(0U, Math.Max);
        var result = new Proto.EvolutionDecisionSurface
        {
            WorldId = publication.WorldId.Value,
            CompletedTick = publication.CompletedTick,
            WorldRevision = publication.WorldRevision,
            TickDurationHours = publication.TickDurationHours,
            ControlledSpeciesId = controlledId.Value,
            ControlledPopulation = species.Population,
            EvolutionRevision = species.EvolutionRevision,
            GenomeHash = species.GenomeHash,
            MutationBalanceQ = species.MutationBalanceQ,
            LastMutationIncomeQ = species.LastMutationIncomeQ,
            AverageHealthQ = species.AverageHealthQ,
            MutationIncomeModifierQ = species.MutationIncomeModifierQ,
            SpeciationNotBeforeTick = species.SpeciationNotBeforeTick,
            MaximumChangeComplexity = maximumComplexity,
        };
        result.Traits.Add(runner.Rules.RulePack.Traits
            .OrderBy(trait => trait.Id.Value)
            .Select(trait =>
            {
                var option = new Proto.EvolutionTraitOption
                {
                    TraitId = trait.Id.Value,
                    StableKey = trait.StableKey,
                    DisplayName = trait.DisplayName,
                    Family = trait.Family,
                    Selectable = trait.Selectable,
                    Acquired = acquired.Contains(trait.Id),
                    MutationPointCostQ = trait.MutationPointCostQ,
                    ChangeComplexity = trait.ChangeComplexity,
                };
                option.PrerequisiteTraitIds.Add(trait.Prerequisites.Select(id => id.Value));
                option.IncompatibleTraitIds.Add(
                    trait.Incompatibilities.Select(id => id.Value));
                return option;
            }));
        result.OccupiedTiles.Add(publication.Organisms
            .Where(organism => organism.SpeciesId == controlledId)
            .GroupBy(organism => organism.TileId)
            .OrderBy(group => group.Key.Value)
            .Select(group => new Proto.EvolutionOccupiedTile
            {
                TileId = group.Key.Value,
                Population = checked((ulong)group.LongCount()),
            }));
        return result;
    }

    private static SpeciationCommand ToCommand(
        WorldRunner runner,
        Proto.SpeciationProposalRequest request)
    {
        if (request.WorldId != runner.WorldId.Value)
        {
            throw new ArgumentException("The proposal targets a different world.",
                nameof(request));
        }
        return new SpeciationCommand(
            SpeciesId.From(request.AncestorSpeciesId),
            request.ExpectedEvolutionRevision,
            request.ExpectedGenomeHash,
            request.NewTraitIds.Select(TraitId.From).ToImmutableArray(),
            request.SelectedTileIds.Select(TileId.FromRowMajorIndex).ToImmutableArray(),
            SpeciationActorKind.Player,
            request.FollowDescendantIfPermitted);
    }

    private static Proto.SpeciationProposal ToProtocol(
        SpeciationPreview preview,
        Proto.EvolutionDecisionSurface before,
        ulong applicationTick)
    {
        var founders = preview.FounderCounts.Aggregate(
            0UL,
            (total, value) => checked(total + value.Count));
        var result = new Proto.SpeciationProposal
        {
            Accepted = preview.Accepted,
            Failure = (Proto.SpeciationFailure)(int)preview.Failure,
            MutationPriceQ = preview.MutationPriceQ,
            ChangeComplexity = preview.ChangeComplexity,
            ProposedGenomeHash = preview.ProposedGenomeHash,
            BalanceBeforeQ = before.MutationBalanceQ,
            DuplicatedBalanceAfterQ = preview.Accepted
                ? checked(before.MutationBalanceQ - preview.MutationPriceQ)
                : 0,
            AncestorPopulationBefore = before.ControlledPopulation,
            AncestorPopulationAfter = before.ControlledPopulation >= founders
                ? before.ControlledPopulation - founders
                : 0,
            DescendantPopulation = founders,
            ResultingSpeciationNotBeforeTick = checked(applicationTick +
                (SpeciationRules.RefractoryHours + before.TickDurationHours - 1) /
                before.TickDurationHours),
        };
        result.FounderCounts.Add(preview.FounderCounts.Select(value =>
            new Proto.SpeciationFounderCount
            {
                TileId = value.TileId.Value,
                Count = value.Count,
            }));
        return result;
    }

    private void Remember(string id, CachedCommand command)
    {
        commands.Add(id, command);
        commandOrder.Enqueue(id);
        if (commandOrder.Count <= MaximumRememberedCommands) return;
        commands.Remove(commandOrder.Dequeue());
    }

    private sealed record CachedCommand(string Fingerprint, byte[] ResponseBytes);
}

public sealed class EvolutionCommandConflictException(string commandId)
    : InvalidOperationException(
        $"Client command ID '{commandId}' was already used for a different proposal.");
