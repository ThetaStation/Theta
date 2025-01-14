using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Theta.ShipEvent.UI;

[GenerateTypedNameReferences]
public sealed partial class AdmiralMenuWindow : DefaultWindow
{
    public Action<string>? TeamSelected;
    public Action<string>? OnCreateTeam;

    public AdmiralMenuWindow()
    {
        RobustXamlLoader.Load(this);
        CreateTeamButton.OnPressed += _ => OnCreateTeam?.Invoke(TeamNameEdit.Text);
    }

    public void Update(AdmiralMenuBoundUserInterfaceState state)
    {
        TeamList.Update(state.Teams, Loc.GetString("shipevent-admmenu-manageteam"));
        TeamList.TeamSelected += state => TeamSelected?.Invoke(state.Name);
    }
}
