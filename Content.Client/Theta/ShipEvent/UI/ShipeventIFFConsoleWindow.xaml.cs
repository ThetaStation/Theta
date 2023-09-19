using Content.Client.Computer;
using Content.Client.Theta.ShipEvent.Console;
using Content.Client.UserInterface.Controls;
using Content.Shared.Theta.ShipEvent.UI;
using Content.Shared.Shuttles.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Theta.ShipEvent.UI;

[GenerateTypedNameReferences]
public sealed partial class ShipeventIFFConsoleWindow : FancyWindow,
    IComputerWindow<ShipeventIFFConsoleBoundUserInterface>
{
    private readonly ButtonGroup _showVesselButtonGroup = new();
    public event Action<bool>? ShowVessel;

    public ShipeventIFFConsoleWindow()
    {
        RobustXamlLoader.Load(this);
        ShowVesselOffButton.Group = _showVesselButtonGroup;
        ShowVesselOnButton.Group = _showVesselButtonGroup;
        ShowVesselOnButton.OnPressed += args => ShowVesselPressed(true);
        ShowVesselOffButton.OnPressed += args => ShowVesselPressed(false);
    }

    private void ShowVesselPressed(bool pressed)
    {
        ShowVessel?.Invoke(pressed);
    }

    public void UpdateState(ShipeventIFFConsoleBoundUserInterfaceState state)
    {
        if ((state.AllowedFlags & IFFFlags.Hide) != 0x0)
        {
            ShowVesselOffButton.Disabled = false;
            ShowVesselOnButton.Disabled = false;

            if ((state.Flags & IFFFlags.Hide) != 0x0)
            {
                ShowVesselOffButton.Pressed = true;
            }
            else
            {
                ShowVesselOnButton.Pressed = true;
            }
        }
        else
        {
            ShowVesselOffButton.Disabled = true;
            ShowVesselOnButton.Disabled = true;
        }
    }
}
