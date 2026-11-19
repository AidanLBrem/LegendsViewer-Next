using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Extensions;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

public class SiteMarkerDto
{
    public SiteMarkerDto(Site site)
    {
        Entity? currentOwner = site.CurrentOwner;
        Entity? currentNation = currentOwner?.MapNation;
        Entity? originNation = site.OriginNation;

        Id = site.Id;
        Name = site.ToLink();
        Owner = currentNation?.ToLink() ?? "Others";
        OwnerText = currentNation?.Name ?? "Others";
        OwnerId = currentNation?.Id;
        CurrentOwnerId = currentOwner?.Id;
        Color = currentNation?.LineColor.ToRgbaString() ?? "#666";
        OriginOwner = originNation?.ToLink() ?? "Unknown origin";
        OriginOwnerText = originNation?.Name ?? "Unknown origin";
        OriginOwnerId = originNation?.Id;
        OriginColor = originNation?.LineColor.ToRgbaString() ?? "#666";
        TypeAsString = site.SiteType.GetDescription();
        Type = site.SiteType;
        Coordinates = site.Coordinates;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Owner { get; set; }
    public string OwnerText { get; set; }
    public int? OwnerId { get; set; }
    public int? CurrentOwnerId { get; set; }
    public string Color { get; set; }
    public string OriginOwner { get; set; }
    public string OriginOwnerText { get; set; }
    public int? OriginOwnerId { get; set; }
    public string OriginColor { get; set; }
    public string TypeAsString { get; set; }
    public SiteType Type { get; set; }
    public List<Location> Coordinates { get; set; }
}
