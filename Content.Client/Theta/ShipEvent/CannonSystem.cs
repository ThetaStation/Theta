﻿using Content.Client.Weapons.Ranged.Systems;
using Content.Shared.Theta.ShipEvent;
using Robust.Client.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Theta.ShipEvent;

public sealed class CannonSystem : SharedCannonSystem
{
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Vector2> _toUpdateRotation = new();

    private readonly Dictionary<EntityUid, Vector2> _firingCannons = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(GunSystem));
        SubscribeLocalEvent<CannonComponent, RotateCannonEvent>(RotateCannons);
        SubscribeLocalEvent<CannonComponent, StartCannonFiringEvent>(RequestCannonShoot);
        SubscribeLocalEvent<CannonComponent, StopCannonFiringEventEvent>(RequestStopCannonShoot);
    }

    private void RequestCannonShoot(EntityUid uid, CannonComponent component, ref StartCannonFiringEvent args)
    {
        _firingCannons[uid] = args.Coordinates;
    }

    private void RequestStopCannonShoot(EntityUid uid, CannonComponent component, ref StopCannonFiringEventEvent args)
    {
        if (_firingCannons.Remove(uid))
        {
            RaisePredictiveEvent(new RequestStopCannonShootEvent
            {
                Cannon = uid,
            });
        }
    }

    private void RotateCannons(EntityUid uid, CannonComponent component, ref RotateCannonEvent args)
    {
        _toUpdateRotation[uid] = args.Coordinates;
        UpdateCoordinates(uid, args.Coordinates);
    }

    private void UpdateCoordinates(EntityUid uid, Vector2 coords)
    {
        if(!_firingCannons.ContainsKey(uid))
            return;
        _firingCannons[uid] = coords;
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        Firing();

        var aggregateByVector = new Dictionary<Vector2, List<EntityUid>>();
        foreach (var (uid, vector2) in _toUpdateRotation)
        {
            // https://github.com/space-wizards/space-station-14/issues/11446
            // some kind of prediction bug for the client
            //foreach (var uid in list)
            //{
            //    RotateToFaceSystem.TryFaceCoordinates(uid, coordinates);
            //}
            var list = aggregateByVector.GetOrNew(vector2);
            list.Add(uid);
        }

        foreach (var (coordinates, list) in aggregateByVector)
        {
            RaiseNetworkEvent(new RotateCannonsEvent(coordinates, list));
        }

        _toUpdateRotation.Clear();
    }

    private void Firing()
    {
        foreach (var (uid, vector2) in _firingCannons)
        {
            var gun = _gunSystem.GetGun(uid);
            if (gun == null)
                return;
            if(!_gunSystem.CanShoot(gun))
                return;

            RaisePredictiveEvent(new RequestCannonShootEvent
            {
                Cannon = uid,
                Coordinates = vector2
            });
        }
    }
}

[ByRefEvent]
public readonly struct StartCannonFiringEvent
{
    public readonly Vector2 Coordinates;

    public StartCannonFiringEvent(Vector2 coordinates)
    {
        Coordinates = coordinates;
    }
}

[ByRefEvent]
public readonly struct StopCannonFiringEventEvent
{
}

[ByRefEvent]
public readonly struct RotateCannonEvent
{
    public readonly Vector2 Coordinates;

    public RotateCannonEvent(Vector2 coordinates)
    {
        Coordinates = coordinates;
    }
}
