using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

public sealed record MapNationPolicy(int Id, string Race, string RaceName, bool Necromancer, bool Civilization, IReadOnlyDictionary<string, string> Responses);
public sealed record NationRelation(int A, int B, string Kind, int Year, int EndYear = -1);
public sealed record NationMapColorsDto(List<MapNationPolicy> Nations, List<NationRelation> Relations)
{
    public static NationMapColorsDto FromWorld(IWorld world)
    {
        var relations = new HashSet<NationRelation>();
        var firstContacts = new Dictionary<(int A, int B), int>();
        var necromancers = new HashSet<int>();
        void Record(Entity? a, Entity? b, string kind, int year, int endYear = -1)
        {
            if (a == null || b == null || year < 0) return;
            int left = a.MapNation.Id, right = b.MapNation.Id;
            if (left == right) return;
            if (left > right) (left, right) = (right, left);
            if (kind == "contact")
            {
                if (!firstContacts.TryGetValue((left, right), out int previous) || year < previous)
                    firstContacts[(left, right)] = year;
            }
            else relations.Add(new(left, right, kind, year, endYear));
        }

        foreach (var entry in world.Events)
        {
            switch (entry)
            {
                case FirstContact contact:
                    Record(contact.Contactor, contact.Contacted, "contact", entry.Year);
                    break;
                case AttackedSite attack:
                    Record(attack.Attacker, attack.Defender, "contact", entry.Year);
                    break;
                case Trade trade when trade.TraderEntity != null && trade.DestSite != null:
                    var host = trade.DestSite.OwnerHistory.LastOrDefault(p =>
                        p.StartYear <= entry.Year && (p.EndYear < 0 || entry.Year < p.EndYear));
                    Record(trade.TraderEntity, host?.Owner, "contact", entry.Year);
                    break;
                case DiplomatLost diplomat:
                    Record(diplomat.Entity, diplomat.InvolvedEntity, "contact", entry.Year);
                    break;
                case PeaceEfforts peace:
                    Record(peace.Source, peace.Destination, peace is PeaceAccepted ? "peace" : "contact", entry.Year);
                    break;
                case EntityAllianceFormed alliance:
                    foreach (var member in alliance.JoiningEntities.Append(alliance.JoiningEntity).Distinct())
                        Record(alliance.InitiatingEntity, member, "alliance", entry.Year);
                    break;
                case EntityCreated { Entity: { } entity, Creator: { } creator } when entity == entity.MapNation:
                    if (DarkForcesAnalysis.IsNecromancer(creator)) necromancers.Add(entity.Id);
                    break;
            }
        }
        foreach (var war in world.EventCollections.OfType<War>())
        {
            var attackers = war.AttackingParticipants.Append(war.Attacker).OfType<Entity>().Select(e => e.MapNation).ToHashSet();
            var defenders = war.DefendingParticipants.Append(war.Defender).OfType<Entity>().Select(e => e.MapNation).ToHashSet();
            foreach (var a in attackers.Except(defenders))
                foreach (var b in defenders.Except(attackers))
                    Record(a, b, "war", war.StartYear, war.EndYear);
        }
        foreach (var (pair, year) in firstContacts)
            relations.Add(new(pair.A, pair.B, "contact", year));
        var nations = world.Entities.Where(e => e == e.MapNation)
            .Select(e => new MapNationPolicy(e.Id, e.Race.Id, e.Race.NameSingular, necromancers.Contains(e.Id), e.IsCiv || e.EntityType == EntityType.Civilization, e.ExportedEthics)).ToList();
        return new(nations, relations.ToList());
    }
}
