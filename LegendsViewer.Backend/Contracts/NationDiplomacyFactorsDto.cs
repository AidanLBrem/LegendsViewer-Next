using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

// A factual comparison from Legends records, NOT the game's internal
// diplomatic_evaluationst totals. Those totals are absent from Legends XML.
public sealed record NationDiplomacyEvidenceDto(int Year, string Kind, string Description, string? Link);

public sealed class NationDiplomacyFactorsDto
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public bool ExactScoresAvailable => false;
    public string ExactScoresNote =>
        "Legends exports do not contain DFHack's total_personal, total_general or total_historical. " +
        "The entries below are recorded context, not the hidden numerical diplomatic evaluation.";
    public List<NationDiplomacyEvidenceDto> Personal { get; set; } = [];
    public List<NationDiplomacyEvidenceDto> Historical { get; set; } = [];

    private static bool SameNation(Entity? actor, Entity nation) =>
        actor != null && (actor == nation || actor.Id == nation.Id ||
                          actor.MapNation == nation);

    public static NationDiplomacyFactorsDto FromEntities(Entity source, Entity target)
    {
        NationDiplomacyFactorsDto result = new()
        {
            SourceId = source.Id,
            TargetId = target.Id
        };

        // Shared worship is evidence of religious overlap, NOT proof of a
        // favorable diplomatic score. Differing religions are not automatically
        // a negative factor; opposition depends on specific spheres and rulers.
        HashSet<int> targetDeities = target.Worshipped.Select(deity => deity.Id).ToHashSet();
        foreach (HistoricalFigure deity in source.Worshipped
                     .Where(deity => targetDeities.Contains(deity.Id))
                     .DistinctBy(deity => deity.Id))
        {
            result.Personal.Add(new NationDiplomacyEvidenceDto(-1, "context",
                $"Both civilizations worship {deity.Name}.", $"/hf/{deity.Id}"));
        }

        void Add(int year, string kind, string description, string? link = null)
        {
            if (year >= 0)
            {
                result.Historical.Add(new NationDiplomacyEvidenceDto(year, kind, description, link));
            }
        }

        // Wars are collections, so collect their sides directly rather than
        // treating every attack or battle as a separate war declaration.
        foreach (War war in source.Wars.Concat(target.Wars).Distinct())
        {
            bool sourceAttacks = SameNation(war.Attacker, source) ||
                war.AttackingParticipants.Any(entity => SameNation(entity, source));
            bool sourceDefends = SameNation(war.Defender, source) ||
                war.DefendingParticipants.Any(entity => SameNation(entity, source));
            bool targetAttacks = SameNation(war.Attacker, target) ||
                war.AttackingParticipants.Any(entity => SameNation(entity, target));
            bool targetDefends = SameNation(war.Defender, target) ||
                war.DefendingParticipants.Any(entity => SameNation(entity, target));

            if ((sourceAttacks && targetDefends) || (sourceDefends && targetAttacks))
            {
                Add(war.StartYear, "hostile", $"Opposed each other in {war.Name}.", $"/war/{war.Id}");
            }
            else if ((sourceAttacks && targetAttacks) || (sourceDefends && targetDefends))
            {
                Add(war.StartYear, "cooperative", $"Fought on the same side in {war.Name}.", $"/war/{war.Id}");
            }
        }

        // Events attached to either civilization can overlap. Process each
        // event once, but only when the counterpart is explicitly identified.
        foreach (WorldEvent entry in source.Events.Concat(target.Events).Distinct())
        {
            bool Opponents(Entity? a, Entity? b) =>
                (SameNation(a, source) && SameNation(b, target)) ||
                (SameNation(a, target) && SameNation(b, source));

            void HostileAction(Entity? attacker, Entity? defender, string verb, string? link = null)
            {
                if (!Opponents(attacker, defender))
                {
                    return;
                }
                Add(entry.Year, "hostile", $"{attacker!.Name} {verb} {defender!.Name}.", link);
            }

            switch (entry)
            {
                case EntityAllianceFormed alliance:
                    bool SourceJoined = alliance.JoiningEntities.Any(entity => SameNation(entity, source)) ||
                        SameNation(alliance.JoiningEntity, source);
                    bool TargetJoined = alliance.JoiningEntities.Any(entity => SameNation(entity, target)) ||
                        SameNation(alliance.JoiningEntity, target);
                    if ((SameNation(alliance.InitiatingEntity, source) && TargetJoined) ||
                        (SameNation(alliance.InitiatingEntity, target) && SourceJoined))
                    {
                        Add(entry.Year, "cooperative", "Mutual-support alliance formed.");
                    }
                    break;
                case PeaceEfforts peace when Opponents(peace.Source, peace.Destination):
                    Add(entry.Year, peace is PeaceAccepted ? "cooperative" : "context",
                        peace is PeaceAccepted ? "Peace offer accepted." :
                        peace is PeaceRejected ? "Peace offer rejected." : "Peace negotiations recorded.");
                    break;
                case AttackedSite attack:
                    HostileAction(attack.Attacker, attack.Defender ?? attack.SiteEntity,
                        "attacked a settlement of",
                        attack.Site == null ? null : $"/site/{attack.Site.Id}");
                    break;
                case FieldBattle battle:
                    HostileAction(battle.Attacker, battle.Defender, "fought a field battle against");
                    break;
                case PlunderedSite plunder:
                    HostileAction(plunder.Attacker, plunder.Defender ?? plunder.SiteEntity,
                        "plundered a settlement of",
                        plunder.Site == null ? null : $"/site/{plunder.Site.Id}");
                    break;
            }
        }

        result.Historical = result.Historical
            .OrderByDescending(entry => entry.Year)
            .ThenBy(entry => entry.Description, StringComparer.Ordinal)
            .Take(100)
            .ToList();
        return result;
    }
}
