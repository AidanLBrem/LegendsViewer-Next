using LegendsViewer.Backend.Legends.Parser;

namespace LegendsViewer.Backend.Legends.Various;

public class IntriguePlot
{
    public int LocalId { get; set; } = -1;
    public string? Type { get; set; }
    public int EntityId { get; } = -1;
    public bool OnHold { get; set; }
    public int ArtifactId { get; set; } = -1;
    public int ActorId { get; set; } = -1;
    public int ParentPlotId { get; set; } = -1;
    public int ParentPlotHfId { get; set; } = -1;
    public int DelegatedPlotId { get; set; } = -1;
    public int DelegatedPlotHfId { get; set; } = -1;
    public List<PlotActor> PlotActors { get; set; } = [];

    public List<IntrigueField> Fields { get; }

    public IntriguePlot(List<Property> properties)
    {
        Fields = IntrigueField.Read(properties);
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "local_id": LocalId = Convert.ToInt32(property.Value); break;
                case "type": Type = property.Value; break;
                case "entity_id": EntityId = Convert.ToInt32(property.Value); break;
                case "artifact_id": ArtifactId = Convert.ToInt32(property.Value); break;
                case "actor_id": ActorId = Convert.ToInt32(property.Value); break;
                case "parent_plot_id": ParentPlotId = Convert.ToInt32(property.Value); break;
                case "parent_plot_hfid": ParentPlotHfId = Convert.ToInt32(property.Value); break;
                case "delegated_plot_id": DelegatedPlotId = Convert.ToInt32(property.Value); break;
                case "delegated_plot_hfid": DelegatedPlotHfId = Convert.ToInt32(property.Value); break;
                case "on_hold":
                    property.Known = true;
                    OnHold = IntrigueField.Flag(property);
                    break;
                case "plot_actor":
                    property.Known = true;
                    if (property.SubProperties != null)
                    {
                        PlotActors.Add(new PlotActor(property.SubProperties));
                    }
                    break;
            }
        }
    }
}
