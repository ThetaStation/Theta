using System.Numerics;
using Content.Server.Theta.ShipEvent.Components;
using Content.Shared.Theta.ShipEvent.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Theta.ShipEvent.Systems;

//todo: create one component for all kinds of anomalies, so it's behaviour depends on a list of effects instead of a specific comp
//also consider using same system for circular shields
public sealed partial class ShipEventFactionSystem
{
    public float AnomalyUpdateInterval;
    public float AnomalySpawnInterval;
    public List<EntityPrototype> AnomalyPrototypes = new();

    private void AnomalyInit()
    {
    }

    private void AnomalyUpdate()
    {
        var query = EntityManager.EntityQueryEnumerator<ShipEventProximityAnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anomaly, out var form))
        {
            Vector2 worldPos = _formSys.GetWorldPosition(form);
            Vector2 bottomLeft = worldPos - new Vector2(anomaly.Range / 2);
            Vector2 topRight = worldPos + new Vector2(anomaly.Range / 2);

            if (IsPositionOutOfBounds(worldPos))
            {
                Vector2 delta = GetPlayAreaBounds().Center - worldPos / 2;
                _physSys.SetLinearVelocity(uid, delta);
                _physSys.ApplyLinearImpulse(uid, delta);
            }

            foreach (var grid in _mapMan.FindGridsIntersecting(TargetMap, new Box2(bottomLeft, topRight), true))
            {
                if (!TryComp<ShipEventFactionMarkerComponent>(grid.Owner, out var marker))
                    return;

                var gridForm = Transform(grid.Owner);
                SpawnAtPosition(anomaly.ToSpawn, Transform(Pick(gridForm.ChildEntities)).Coordinates);
            }
        }
    }

    private void AnomalySpawn()
    {
        if (AnomalyPrototypes.Count == 0)
            return;

        string protId = Pick(AnomalyPrototypes).ID;
        for (int c = 0; c < 50; c++)
        {
            Box2 bounds = GetPlayAreaBounds();
            Vector2 pos = _random.NextVector2Box(bounds.BottomLeft.X, bounds.BottomLeft.Y, bounds.TopRight.X, bounds.TopRight.Y);

            if (_mapMan.TryFindGridAt(new(pos, TargetMap), out _, out _))
                continue;

            SpawnAtPosition(protId, new(_mapMan.GetMapEntityId(TargetMap), pos));
            break;
        }
    }
}