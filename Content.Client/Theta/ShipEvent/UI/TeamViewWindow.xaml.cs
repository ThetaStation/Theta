﻿using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Theta.ShipEvent.UI;

/// <summary>
/// Window for ship event team-view action
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class TeamViewWindow : DefaultWindow
{
    public TeamViewWindow()
    {
        RobustXamlLoader.Load(this);
    }

    public void Update(TeamViewBoundUserInterfaceState state)
    {
        TeamList.Update(state.Teams, null);

        ModifierContainer.DisposeAllChildren();
        foreach (string modifier in state.Modifiers)
        {
            ModifierContainer.AddChild(new Label() { Text = modifier });
        }
    }
}
