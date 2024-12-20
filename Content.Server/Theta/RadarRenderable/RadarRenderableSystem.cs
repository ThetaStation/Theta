using Content.Server.Shuttles.Systems;
using Content.Server.Theta.ShipEvent;
using Content.Server.Theta.ShipEvent.Console;
using Content.Shared.Doors.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Theta.ShipEvent;
using Content.Shared.Theta.ShipEvent.Components;

namespace Content.Server.Theta.RadarRenderable;

//todo (radars): there shouldn't be a cannon/mob/door specific code
public sealed class RadarRenderableSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly RadarConsoleSystem _radarConsoleSystem = default!;
    [Dependency] private readonly CannonSystem _cannonSystem = default!;

    public List<CommonRadarEntityInterfaceState> GetObjectsAround(EntityUid consoleUid, RadarConsoleComponent? radar = null)
    {
        var states = new List<CommonRadarEntityInterfaceState>();
        if (!Resolve(consoleUid, ref radar))
            return states;
        states.AddRange(GetRadarRenderableStates(consoleUid, radar));
        return states;
    }

    private List<CommonRadarEntityInterfaceState> GetRadarRenderableStates(EntityUid consoleUid,
        RadarConsoleComponent? radar = null,
        TransformComponent? xform = null)
    {
        var states = new List<CommonRadarEntityInterfaceState>();
        if (!Resolve(consoleUid, ref radar, ref xform))
            return states;
        var query = EntityQueryEnumerator<RadarRenderableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var radarRenderable, out var transform))
        {
            if (!_radarConsoleSystem.HasFlag(radar, (RadarRenderableGroup) radarRenderable.Group))
                continue;
            if (!xform.MapPosition.InRange(transform.MapPosition, radar.MaxRange))
                continue;

            CommonRadarEntityInterfaceState? state;
            switch ((RadarRenderableGroup) radarRenderable.Group)
            {
                case RadarRenderableGroup.ShipEventTeammate:
                    state = GetMobState(uid, radarRenderable, transform);
                    break;
                case RadarRenderableGroup.Cannon:
                    state = GetCannonState(uid, consoleUid, radarRenderable, xform, transform);
                    break;
                case RadarRenderableGroup.Door:
                    state = GetDoorState(uid, radarRenderable, transform, xform);
                    break;
                default:
                    state = GetDefaultState(uid, radarRenderable, transform);
                    break;
            }

            if (state != null)
                states.Add(state);
        }

        return states;
    }

    private CommonRadarEntityInterfaceState? GetCannonState(EntityUid uid, EntityUid consoleUid,
        RadarRenderableComponent radarRenderable, TransformComponent consoleTransform, TransformComponent xform)
    {
        if (!TryComp<CannonComponent>(uid, out var cannon))
            return null;

        var consoleGrid = consoleTransform.GridUid;
        var isCannonConsole = HasComp<CannonConsoleComponent>(consoleUid);

        if (xform.GridUid == null || !xform.Anchored)
            return null;

        if (TryComp<ShipEventTeamMarkerComponent>(consoleGrid, out var consoleMarker) &&
            consoleMarker.Team != null &&
            !consoleMarker.Team.ShipGrids.Contains(xform.GridUid.Value))
            return null;

        var (ammo, maxAmmo) = _cannonSystem.GetCannonAmmoCount(uid, cannon);
        var mainColor = (cannon.BoundConsoleUid == consoleUid) ? Color.Lime : (isCannonConsole ? Color.LightGreen : Color.YellowGreen);

        var hsvColor = Color.ToHsv(mainColor);
        const float additionalDegreeCoeff = 20f / 360f;
        // X is hue
        var hueOffset = hsvColor.X * ammo / Math.Max(1, maxAmmo);
        hsvColor.X = Math.Max(hueOffset + additionalDegreeCoeff, additionalDegreeCoeff);

        mainColor = Color.FromHsv(hsvColor);


        return new CommonRadarEntityInterfaceState(
            GetNetCoordinates(_transformSystem.GetMoverCoordinates(uid, xform)),
            _transformSystem.GetWorldRotation(xform),
            radarRenderable.ViewPrototypes,
            mainColor
        );
    }

    private CommonRadarEntityInterfaceState? GetDefaultState(EntityUid uid, RadarRenderableComponent renderable,
        TransformComponent xform)
    {
        return new CommonRadarEntityInterfaceState(
            GetNetCoordinates(_transformSystem.GetMoverCoordinates(uid, xform)),
            _transformSystem.GetWorldRotation(xform),
            renderable.ViewPrototypes,
            renderable.OverrideColor
        );
    }

    private CommonRadarEntityInterfaceState? GetMobState(EntityUid uid, RadarRenderableComponent renderable, TransformComponent xform)
    {
        if (_mobStateSystem.IsIncapacitated(uid))
            return null;

        if (TryComp<IFFComponent>(Transform(uid).GridUid, out var iff) && iff.Flags == IFFFlags.Hide)
            return null;

        Color? color = null;
        if (TryComp<ShipEventTeamMarkerComponent>(uid, out var marker) && marker.Team != null)
            color = marker.Team.Color;

        return new CommonRadarEntityInterfaceState(
            GetNetCoordinates(_transformSystem.GetMoverCoordinates(uid, xform)),
            _transformSystem.GetWorldRotation(xform),
            renderable.ViewPrototypes,
            color
        );
    }

    private CommonRadarEntityInterfaceState? GetDoorState(EntityUid uid, RadarRenderableComponent renderable,
        TransformComponent xform, TransformComponent consoleTransform)
    {
        var myGrid = consoleTransform.GridUid;

        if (Transform(uid).GridUid != myGrid)
            return null;
        if (!Transform(uid).Anchored)
            return null;
        if (!TryComp<DoorComponent>(uid, out var door))
            return null;

        Color? color = Color.White;

        if (door.State == DoorState.Open)
        {
            color = Color.LimeGreen;
        }
        else
        {
            color = Color.Red;
        }

        return new CommonRadarEntityInterfaceState(
            GetNetCoordinates(_transformSystem.GetMoverCoordinates(uid, xform)),
            _transformSystem.GetWorldRotation(xform),
            renderable.ViewPrototypes,
            color
        );
    }
}
