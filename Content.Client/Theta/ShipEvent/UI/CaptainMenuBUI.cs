using System.Linq;
using Content.Shared.Theta.ShipEvent.UI;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Theta.ShipEvent.UI;

[UsedImplicitly]
public sealed class CaptainMenuBoundUserInterface : BoundUserInterface
{
    private CaptainMenuWindow? _window;

    public CaptainMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new CaptainMenuWindow();
        _window.OpenCentered();
        _window.OnClose += Close;
        _window.ShipPickerButtonPressed += _ =>
        {
            SendMessage(new GetShipPickerInfoMessage());
        };
        _window.KickButtonPressed += _ =>
        {
            SendMessage(new ShipEventCaptainMenuKickMemberMessage(_window.KickCKey));
        };
        _window.shipPicker.OnSelectionMade += _ =>
        {
            if (_window.shipPicker.Selection == null)
                return;

            SendMessage(new ShipEventCaptainMenuChangeShipMessage(_window.shipPicker.Selection));
        };
        _window.SetMaxMembersButtonPressed += _ =>
        {
            SendMessage(new ShipEventCaptainMenuSetMaxMembersMessage(_window.MaxMembers));
        };
        _window.SetPasswordButtonPressed += _ =>
        {
            var password = _window.Password != "" ? _window.Password : null;
            SendMessage(new ShipEventCaptainMenuSetPasswordMessage(password));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        switch (state)
        {
            case ShipEventCaptainMenuBoundUserInterfaceState msg:
                base.UpdateState(state);
                _window?.UpdateState(msg);
                break;
            case ShipPickerBoundUserInterfaceState pickerMsg:
                _window?.shipPicker.UpdateState(pickerMsg);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
        }
    }
}
