﻿using System.Linq;
using Content.Client.Computer;
using Content.Client.UserInterface.Controls;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client.Theta.ShipEvent.Console;

[GenerateTypedNameReferences]
public sealed partial class CannonConsoleWindow : FancyWindow,
    IComputerWindow<CannonConsoleBoundInterfaceState>
{
    public CannonConsoleWindow()
    {
        RobustXamlLoader.Load(this);
    }

    public void UpdateState(CannonConsoleBoundInterfaceState scc)
    {
        RadarScreen.UpdateState(scc);

        var cannons = scc.Cannons.Where(i => i.IsControlling).ToList();
        UpdateAmmo(cannons);
    }

    public void SetMatrix(EntityCoordinates? coordinates, Angle? angle)
    {
        RadarScreen.SetMatrix(coordinates, angle);
    }

    public void SetOwner(EntityUid uid)
    {
        RadarScreen.SetOwnerUid(uid);
    }

    private void UpdateAmmo(List<CannonInformationInterfaceState> controlledCannons)
    {
        AmmoStatusContents.RemoveAllChildren();
        foreach (var cannonInformation in controlledCannons)
        {
            var status = new CannonAmmoStatus();
            AmmoStatusContents.AddChild(status);
            var noMagazine = cannonInformation is { Ammo: 0, MaxCapacity: 0 };
            status.Update(!noMagazine, cannonInformation.Ammo, cannonInformation.UsedCapacity, cannonInformation.MaxCapacity);
        }
    }
}
