using LegendsViewer.Backend.Legends.Parser;

namespace LegendsViewer.Backend.Legends.Various;

public class PlotActor
{
    public int ActorId { get; set; } = -1;
    public string? PlotRole { get; set; }
    public int AgreementId { get; set; } = -1;
    public bool AgreementHasMessenger { get; set; }

    public List<IntrigueField> Fields { get; }

    public PlotActor(List<Property> properties)
    {
        Fields = IntrigueField.Read(properties);
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "actor_id": ActorId = Convert.ToInt32(property.Value); break;
                case "plot_role": PlotRole = property.Value; break;
                case "agreement_id": AgreementId = Convert.ToInt32(property.Value); break;
                case "agreement_has_messenger":
                    property.Known = true;
                    AgreementHasMessenger = IntrigueField.Flag(property);
                    break;
            }
        }
    }
}
