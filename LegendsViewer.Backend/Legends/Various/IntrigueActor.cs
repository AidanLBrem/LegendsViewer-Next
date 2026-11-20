using LegendsViewer.Backend.Legends.Parser;

namespace LegendsViewer.Backend.Legends.Various;

public class IntrigueActor
{
    public int LocalId { get; set; } = -1;
    public string? Role { get; set; }
    public string? Strategy { get; set; }
    public int EntityId { get; } = -1;
    public int HfId { get; set; } = -1;
    public int HandleActorId { get; set; } = -1;
    public int StrategyEnId { get; set; } = -1;
    public int StrategyEppId { get; set; } = -1;
    public bool PromisedActorImmortality { get; set; }
    public bool PromisedMeImmortality { get; set; }

    public List<IntrigueField> Fields { get; }

    public IntrigueActor(List<Property> properties)
    {
        Fields = IntrigueField.Read(properties);
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "local_id": LocalId = Convert.ToInt32(property.Value); break;
                case "entity_id": EntityId = Convert.ToInt32(property.Value); break;
                case "role": Role = property.Value; break;
                case "strategy": Strategy = property.Value; break;
                case "hfid": HfId = Convert.ToInt32(property.Value); break;
                case "handle_actor_id": HandleActorId = Convert.ToInt32(property.Value); break;
                case "strategy_enid": StrategyEnId = Convert.ToInt32(property.Value); break;
                case "strategy_eppid": StrategyEppId = Convert.ToInt32(property.Value); break;
                case "promised_actor_immortality":
                    property.Known = true;
                    PromisedActorImmortality = IntrigueField.Flag(property);
                    break;
                case "promised_me_immortality":
                    property.Known = true;
                    PromisedMeImmortality = IntrigueField.Flag(property);
                    break;
            }
        }
    }
}
