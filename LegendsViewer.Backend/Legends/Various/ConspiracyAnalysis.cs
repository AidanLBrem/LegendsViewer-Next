using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Legends.Various;

public record IntrigueReference(int Id, string Name, string? Path);
public record ConspiratorDto(int ActorId, string Role, string? ActorRole, bool IsTarget, string? Strategy,
    IntrigueReference? Figure, IntrigueReference? Entity, IntrigueReference? StrategyEntity,
    int StrategyPositionId, int HandleActorId, int AgreementId, bool AgreementHasMessenger,
    bool PromisedActorImmortality, bool PromisedMeImmortality, List<IntrigueField> Fields);
public record PlotReferenceDto(int FigureId, int LocalId, bool Resolved);
public record ConspiracyDto(string Key, int LocalId, IntrigueReference Owner, string Objective,
    string Status, IntrigueReference? Entity, IntrigueReference? Artifact,
    List<ConspiratorDto> Participants, List<IntrigueReference> Civilizations,
    PlotReferenceDto? Parent, PlotReferenceDto? Delegated, List<IntrigueField> Fields);
public record ConspiracyPageDto(List<ConspiracyDto> Items, int TotalCount, int TotalFilteredCount,
    List<IntrigueReference> Civilizations);

public static class ConspiracyAnalysis
{
    public static List<ConspiracyDto> Calculate(IWorld world)
    {
        var figures = world.HistoricalFigures.ToDictionary(f => f.Id);
        var entities = world.Entities.ToDictionary(e => e.Id);
        var artifacts = world.Artifacts.ToDictionary(a => a.Id);
        var result = new List<ConspiracyDto>();
        IntrigueReference? Reference(int id, string kind)
        {
            if (id < 0) return null;
            WorldObject? item = kind switch
            {
                "hf" => figures.GetValueOrDefault(id),
                "entity" => entities.GetValueOrDefault(id),
                "artifact" => artifacts.GetValueOrDefault(id),
                _ => null
            };
            return new(id, item?.Name ?? $"Unknown {kind} #{id}", item == null ? null : $"/{kind}/{id}");
        }
        PlotReferenceDto? Related(int figureId, int plotId) => figureId < 0 || plotId < 0 ? null :
            new(figureId, plotId, figures.GetValueOrDefault(figureId)?.IntriguePlots.Any(p => p.LocalId == plotId) == true);

        foreach (var owner in world.HistoricalFigures.Where(f => f.IntriguePlots.Count > 0))
        {
            var actors = owner.IntrigueActors.GroupBy(a => a.LocalId).ToDictionary(g => g.Key, g => g.First());
            foreach (var plot in owner.IntriguePlots)
            {
                var participants = new List<ConspiratorDto>();
                var memberships = new HashSet<int>();
                void AddEntity(int id)
                {
                    var civ = entities.GetValueOrDefault(id)?.CurrentCiv;
                    if (civ != null) memberships.Add(civ.Id);
                }
                void AddFigure(int id)
                {
                    if (!figures.TryGetValue(id, out var figure)) return;
                    // Includes historical affiliations; these are context, not proof of sponsorship.
                    foreach (var link in figure.RelatedEntities)
                        if (link.Entity != null) AddEntity(link.Entity.Id);
                }
                AddFigure(owner.Id);
                AddEntity(plot.EntityId);
                var plotActors = plot.PlotActors.ToList();
                if (plot.ActorId >= 0 && !plotActors.Any(a => a.ActorId == plot.ActorId))
                    plotActors.Add(new PlotActor([]) { ActorId = plot.ActorId, PlotRole = "Referenced actor" });
                foreach (var participant in plotActors)
                {
                    var actor = actors.GetValueOrDefault(participant.ActorId);
                    var isTarget = participant.ActorId == plot.ActorId && plot.Type is
                        "assassinate actor" or "direct war to actor" or "corrupt actor's government" or
                        "frame actor" or "kidnap actor" or "corruptly punish actor" or "sabotage actor";
                    participants.Add(new(participant.ActorId, isTarget ? "Target" : participant.PlotRole ?? "Unknown plot role",
                        actor?.Role, isTarget, actor?.Strategy, Reference(actor?.HfId ?? -1, "hf"), Reference(actor?.EntityId ?? -1, "entity"),
                        Reference(actor?.StrategyEnId ?? -1, "entity"), actor?.StrategyEppId ?? -1,
                        actor?.HandleActorId ?? -1, participant.AgreementId, participant.AgreementHasMessenger,
                        actor?.PromisedActorImmortality ?? false, actor?.PromisedMeImmortality ?? false,
                        [.. participant.Fields, .. actor?.Fields ?? []]));
                    if (actor != null)
                    {
                        AddFigure(actor.HfId);
                        AddEntity(actor.EntityId);
                        AddEntity(actor.StrategyEnId);
                    }
                }
                result.Add(new($"{owner.Id}:{plot.LocalId}", plot.LocalId, Reference(owner.Id, "hf")!,
                    string.IsNullOrWhiteSpace(plot.Type) ? "Unknown objective" : plot.Type, plot.OnHold ? "On hold" : "Not marked on hold",
                    Reference(plot.EntityId, "entity"), Reference(plot.ArtifactId, "artifact"), participants,
                    memberships.Order().Select(id => Reference(id, "entity")!).ToList(),
                    Related(plot.ParentPlotHfId, plot.ParentPlotId), Related(plot.DelegatedPlotHfId, plot.DelegatedPlotId),
                    plot.Fields));
            }
        }
        return result.OrderBy(p => p.Owner.Id).ThenBy(p => p.LocalId).ToList();
    }
}
