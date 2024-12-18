﻿using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Theta.ShipEvent.UI;

[GenerateTypedNameReferences]
public sealed partial class TeamLobbyWindow : DefaultWindow
{
    public event Action<BaseButton.ButtonEventArgs>? CreateTeamButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? RefreshButtonPressed;
    public event Action<string, bool>? JoinButtonPressed;

    public TeamLobbyWindow()
    {
        RobustXamlLoader.Load(this);
        CreateTeam.OnPressed += _ => CreateTeamButtonPressed?.Invoke(_);
        Refresh.OnPressed += _ => RefreshButtonPressed?.Invoke(_);
    }

    public void UpdateState(ShipEventLobbyBoundUserInterfaceState msg)
    {
        TeamList.Update(msg.Teams, Loc.GetString("shipevent-lobby-jointeam"));
        TeamList.TeamSelected += state => JoinButtonPressed?.Invoke(state.Name, state.HasPassword);
    }
}
