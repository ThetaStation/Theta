using Content.Shared.Theta.ShipEvent;
using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Theta.ShipEvent.UI;

[GenerateTypedNameReferences]
public sealed partial class CaptainMenuWindow : DefaultWindow
{
    public ShipPickerWindow ShipPicker = default!;

    public event Action<BaseButton.ButtonEventArgs>? ShipPickerButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? KickButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? SetMaxMembersButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? SetPasswordButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? SetCaptainButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? RespawnTeamButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? DisbandTeamButtonPressed;

    public string KickCKey => KickCKeyEdit.Text;
    public string CaptainCKey => CaptainCKeyEdit.Text;
    public string Password => PasswordEdit.Text;
    public int MaxMembers => MaxMembersEdit.Value;

    public CaptainMenuWindow()
    {
        RobustXamlLoader.Load(this);

        ShipPicker = new ShipPickerWindow();
        ShipPicker.OnSelectionMade += SetChosenShipLabel;

        MaxMembersEdit.IsValid = value => value >= 0 && value <= 100;
        MaxMembersEdit.InitDefaultButtons();

        ShipPickerButton.OnPressed += _ =>
        {
            ShipPicker.OpenCentered();
            ShipPickerButtonPressed?.Invoke(_);
        };
        KickButton.OnPressed += _ =>
        {
            KickButtonPressed?.Invoke(_);
        };
        SetMaxMembersButton.OnPressed += _ =>
        {
            SetMaxMembersButtonPressed?.Invoke(_);
        };
        SetPasswordButton.OnPressed += _ =>
        {
            SetPasswordButtonPressed?.Invoke(_);
        };
        SetCaptainButton.OnPressed += _ =>
        {
            SetCaptainButtonPressed?.Invoke(_);
        };
        RespawnTeamButton.OnPressed += _ =>
        {
            RespawnTeamButtonPressed?.Invoke(_);
        };
        DisbandTeamButton.OnPressed += _ =>
        {
            DisbandTeamButtonPressed?.Invoke(_);
        };
    }

    private void SetChosenShipLabel(ShipTypePrototype selection)
    {
        ShipName.Text = Loc.GetString(selection!.Name);
    }

    public void UpdateState(CaptainMenuBoundUserInterfaceState state)
    {
        TeamName.Text = state.Name;
        MemberList.Text = string.Join(';', state.Members);
        ShipName.Text = Loc.GetString(state.CurrentShipType?.Name ?? "N/A");
        PasswordEdit.Text = state.Password ?? "";
        MaxMembersEdit.Value = state.MaxMembers;
    }
}
