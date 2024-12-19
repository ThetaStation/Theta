namespace Content.Shared.Roles.Theta;

/// <summary>
/// A collection of teams, controlled by the admiral (may not be present).
/// Most of the fields in this class should only be set by the methods inside the team system, 
/// I'm just too lazy to implement proper access restrictions. So please take a look at the team system before touching anything.
/// </summary>
public sealed class ShipEventFleet
{
    public string Name = string.Empty;
    public Color Color;
    public string? Admiral = null;
    public bool AdmiralLocked; //if true new admiral won't be selected when the old one disconnects
    public ShipEventTeam? ManagedByAdmiral = null; //team last managed by the admiral via captain's menu
    public List<ShipEventTeam> Teams = new();
}