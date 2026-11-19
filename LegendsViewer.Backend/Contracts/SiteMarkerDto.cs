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
        OwnerHistory = site.OwnerHistory
            .OrderBy(period => period.StartYear < 0 ? 0 : period.StartYear)
            .Select(period => new SiteOwnershipPeriodDto(period))
            .ToList();
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
    public List<SiteOwnershipPeriodDto> OwnerHistory { get; set; }
}

// The timeline uses the same parent-faction identity and palette as the
// present-day map. Keep the actual holder separately for details.
public class SiteOwnershipPeriodDto
{
    public SiteOwnershipPeriodDto(OwnerPeriod period)
    {
        Entity? actualOwner = period.Owner;
        Entity? nation = actualOwner?.MapNation;
        StartYear = period.StartYear;
        EndYear = period.EndYear;
        Owner = nation?.ToLink() ?? "Unclaimed";
        OwnerText = nation?.Name ?? "Unclaimed";
        OwnerId = nation?.Id;
        CurrentOwnerId = actualOwner?.Id;
        ActualOwner = actualOwner?.ToLink();
        Color = nation?.LineColor.ToRgbaString() ?? "#666";
        StartCause = period.StartCause;
    }

    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public string Owner { get; set; }
    public string OwnerText { get; set; }
    public int? OwnerId { get; set; }
    public int? CurrentOwnerId { get; set; }
    public string? ActualOwner { get; set; }
    public string Color { get; set; }
    public string? StartCause { get; set; }
}
