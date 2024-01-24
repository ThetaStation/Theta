using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using Content.Shared.Theta.ShipEvent;
using JetBrains.Annotations;

namespace Content.Client.Theta.ModularRadar.UI.ShuttleConsole;

//todo (radars): separate stealth controls from radar
[UsedImplicitly]
public sealed class ModularRadarShuttleConsoleBoundUserInterface : BoundUserInterface
{
    private ModularRadarShuttleConsoleWindow? _window;

    public ModularRadarShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new ModularRadarShuttleConsoleWindow();
        _window.UndockPressed += OnUndockPressed;
        _window.StartAutodockPressed += OnAutodockPressed;
        _window.StopAutodockPressed += OnStopAutodockPressed;
        _window.DestinationPressed += OnDestinationPressed;
        _window.ChangeNamePressed += OnChangeNamePressed;
        _window.StealthButtonPressed += OnStealthButtonPressed;
        _window.OpenCentered();
        SendMessage(new ShipEventRequestStealthStatusMessage());
        _window.OnClose += OnClose;
    }

    private void OnDestinationPressed(NetEntity obj)
    {
        SendMessage(new ShuttleConsoleFTLRequestMessage()
        {
            Destination = obj,
        });
    }

    private void OnStealthButtonPressed()
    {
        SendMessage(new ShipEventToggleStealthMessage());
    }

    private void OnChangeNamePressed(string name)
    {
        SendMessage(new ShuttleConsoleChangeShipNameMessage(name));
    }

    private void OnClose()
    {
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
        }
    }

    private void OnStopAutodockPressed(NetEntity obj)
    {
        SendMessage(new StopAutodockRequestMessage() { DockEntity = obj });
    }

    private void OnAutodockPressed(NetEntity obj)
    {
        SendMessage(new AutodockRequestMessage() { DockEntity = obj });
    }

    private void OnUndockPressed(NetEntity obj)
    {
        SendMessage(new UndockRequestMessage() { DockEntity = obj });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ShuttleConsoleBoundInterfaceState cState)
            _window?.SetMatrix(EntMan.GetCoordinates(cState.Coordinates), cState.Angle);

        _window?.UpdateState(state);
        _window?.SetOwner(Owner);
    }

    public void SetStealthStatus(bool ready)
    {
        _window?.SetStealthStatus(ready);
    }
}
