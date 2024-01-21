using Content.Client.Message;
using Content.Client.Theta.ModularRadar.Modules.ShipEvent;
using Content.Shared.Theta.ShipEvent.UI;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client.Theta.ShipEvent.Console;

[GenerateTypedNameReferences]
public sealed partial class CircularShieldConsoleWindow : DefaultWindow
{
    public event Action? OnEnableButtonPressed;

    public event Action<Angle?, Angle?, int?>? OnShieldParametersChanged;

    public CircularShieldConsoleWindow()
    {
        RobustXamlLoader.Load(this);

        ShieldEnableButton.OnPressed += _ => OnEnableButtonPressed?.Invoke();

        if (RadarScreen.TryGetModule<RadarControlShield>(out var controlShield))
        {
            controlShield.UpdateShieldRotation += angle => OnShieldParametersChanged?.Invoke(angle, null, null);
        }

        ShieldWidthSlider.OnValueChanged += width => OnShieldParametersChanged?.Invoke(null, Angle.FromDegrees(width.Value), null);
        ShieldRadiusSlider.OnValueChanged += radius => OnShieldParametersChanged?.Invoke(null, null, (int)radius.Value);
    }

    public void SetMatrix(EntityCoordinates? coordinates, Angle? angle)
    {
        RadarScreen.SetMatrix(coordinates, angle);
    }

    public void SetOwner(EntityUid uid)
    {
        RadarScreen.SetOwnerUid(uid);
    }

    public void UpdateState(ShieldConsoleBoundsUserInterfaceState shieldState)
    {
        RadarScreen.UpdateState(shieldState);
        var state = shieldState.Shield;

        ShieldPowerStatusLabel.SetMarkup(Loc.GetString(state.Powered ? "shipevent-shieldconsole-powered" : "shipevent-shieldconsole-nopower"));
        ShieldWidthSlider.SetValueWithoutEvent((float)state.Width.Degrees);
        ShieldWidthSlider.MaxValue = (float)state.MaxWidth; //todo: send min/max values only once to save state bytes
        ShieldRadiusSlider.SetValueWithoutEvent(state.Radius);
        ShieldRadiusSlider.MaxValue = state.MaxRadius;
    }
}
