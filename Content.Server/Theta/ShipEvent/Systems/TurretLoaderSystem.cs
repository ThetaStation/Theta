using System.Linq;
using Content.Server.Storage.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Theta.ShipEvent;
using Content.Shared.Theta.ShipEvent.Components;
using Content.Shared.Theta.ShipEvent.UI;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Theta.ShipEvent.Systems;

public sealed class TurretLoaderSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slotSys = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TurretLoaderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TurretLoaderComponent, ComponentRemove>(OnRemoval);

        SubscribeLocalEvent<TurretLoaderComponent, EntInsertedIntoContainerMessage>(OnContainerInsert);
        SubscribeLocalEvent<TurretLoaderComponent, EntRemovedFromContainerMessage>(OnContainerRemove);

        SubscribeLocalEvent<TurretLoaderComponent, ThrowHitByEvent>(HandleThrowCollide);

        SubscribeLocalEvent<TurretLoaderComponent, TurretLoaderEjectRequest>(OnEject);
        SubscribeLocalEvent<TurretLoaderComponent, TurretLoaderAfterShotMessage>(AfterShot);
        SubscribeLocalEvent<TurretLoaderComponent, ComponentGetState>(GetLoaderState);
        SubscribeLocalEvent<TurretLoaderComponent, NewLinkEvent>(OnLink);
    }

    private void GetLoaderState(EntityUid uid, TurretLoaderComponent loader, ref ComponentGetState args)
    {
        UpdateAmmoContainer(loader);
        args.State = new TurretLoaderState(loader);
    }

    public void SetupLoader(EntityUid uid, TurretLoaderComponent loader, EntityUid? turretUid = null)
    {
        if (!loader.BoundTurret.IsValid() && turretUid != null)
        {
            loader.BoundTurret = turretUid.Value;
        }
        else
        {
            if (CheckNetwork(uid, out EntityUid turret))
                loader.BoundTurret = turret;
        }

        if (EntityManager.TryGetComponent<ItemSlotsComponent>(uid, out var slots))
        {
            loader.ContainerSlot = slots.Slots["ammoContainer"];

            if (loader.BoundTurret.IsValid())
            {
                if (EntityManager.TryGetComponent<CannonComponent>(loader.BoundTurret, out var cannon))
                {
                    cannon.BoundLoader = loader;
                    cannon.BoundLoaderEntity = uid;
                    Dirty(cannon);
                }
            }
        }

        Dirty(loader);
    }

    private void UpdateAmmoContainer(TurretLoaderComponent loader)
    {
        ContainerAmmoProviderComponent? turretContainer = null;
        if (loader.BoundTurret.IsValid())
        {
            turretContainer = EntityManager.EnsureComponent<ContainerAmmoProviderComponent>(loader.BoundTurret);
            turretContainer.ProviderUid = null;
            turretContainer.Container = "";
        }

        loader.AmmoContainer = null;
        loader.MaxContainerCapacity = 0;
        loader.CurrentContainerCapacity = 0;

        var container = loader.ContainerSlot?.Item;
        if (EntityManager.TryGetComponent<ServerStorageComponent>(container, out var storage))
        {
            if (storage.Storage != null)
            {
                loader.AmmoContainer = storage.Storage;
                loader.MaxContainerCapacity = storage.StorageCapacityMax;
                loader.CurrentContainerCapacity = storage.StorageUsed;

                if (turretContainer != null)
                {
                    turretContainer.ProviderUid = container;
                    turretContainer.Container = storage.Storage.ID;
                }
            }
        }
        
        Dirty(loader);
        if(turretContainer != null)
            Dirty(turretContainer);
    }

    private bool CheckNetwork(EntityUid uid, out EntityUid turret)
    {
        if (EntityManager.TryGetComponent<DeviceLinkSourceComponent>(uid, out var source))
        {
            if (source.Outputs.Count > 0)
            {
                turret = source.Outputs.Values.First().First();
                return true;
            }
        }

        turret = EntityUid.Invalid;
        return false;
    }

    private void OnInit(EntityUid uid, TurretLoaderComponent loader, ComponentInit args)
    {
        SetupLoader(uid, loader);
    }

    private void OnRemoval(EntityUid uid, TurretLoaderComponent loader, ComponentRemove args)
    {
        if (loader.BoundTurret != EntityUid.Invalid)
        {
            if (EntityManager.TryGetComponent<CannonComponent>(loader.BoundTurret, out var cannon))
                cannon.BoundLoader = null;
            if (EntityManager.TryGetComponent<ContainerAmmoProviderComponent>(loader.BoundTurret, out var container))
            {
                container.ProviderUid = null;
                container.Container = "";
            }
        }
    }

    private void OnContainerInsert(EntityUid uid, TurretLoaderComponent loader, EntInsertedIntoContainerMessage args)
    {
        UpdateAmmoContainer(loader);
    }

    private void OnContainerRemove(EntityUid uid, TurretLoaderComponent loader, EntRemovedFromContainerMessage args)
    {
        UpdateAmmoContainer(loader);
    }

    private void OnEject(EntityUid uid, TurretLoaderComponent loader, TurretLoaderEjectRequest args)
    {
        if (loader.ContainerSlot?.Item == null)
            return;

        _slotSys.TryEject(uid, loader.ContainerSlot, uid, out _);
    }

    private void OnLink(EntityUid uid, TurretLoaderComponent loader, NewLinkEvent args)
    {
        SetupLoader(uid, loader, args.Sink);
    }
    
    private void AfterShot(EntityUid uid, TurretLoaderComponent loader, TurretLoaderAfterShotMessage args)
    {
        if (loader.AmmoContainer != null && loader.ContainerSlot != null)
        {
            if(loader.AmmoContainer.ContainedEntities.Count == 0)
                _slotSys.TryEject(uid, loader.ContainerSlot, uid, out _);
        }
    }

    /// <summary>
    /// Throwing ammo in loads it up.
    /// </summary>
    private void HandleThrowCollide(EntityUid uid, TurretLoaderComponent component, ThrowHitByEvent args)
    {
        if (component.ContainerSlot == null)
            return;

        if (component.ContainerSlot.HasItem && !_slotSys.TryEject(uid, component.ContainerSlot, null, out var item))
            return;

        _slotSys.TryInsert(uid, component.ContainerSlot, args.Thrown, args.User);
    }
}
