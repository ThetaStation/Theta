using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Theta.ShipEvent;
using Content.Shared.Theta.ShipEvent.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Content.Shared.Throwing;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Theta.ShipEvent.Systems;

public sealed class TurretLoaderSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slotSys = default!;
    [Dependency] private readonly SharedAudioSystem _audioSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TurretLoaderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TurretLoaderComponent, ComponentRemove>(OnRemoval);
        SubscribeLocalEvent<TurretLoaderComponent, EntInsertedIntoContainerMessage>(OnContainerInsert);
        SubscribeLocalEvent<TurretLoaderComponent, EntRemovedFromContainerMessage>(OnContainerRemove);
        SubscribeLocalEvent<TurretLoaderComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<TurretLoaderComponent, ThrowHitByEvent>(HandleThrowCollide);
        SubscribeLocalEvent<TurretLoaderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TurretLoaderComponent, TurretLoaderAfterShotMessage>(AfterShot);
        SubscribeLocalEvent<TurretLoaderComponent, ComponentGetState>(GetLoaderState);
        SubscribeLocalEvent<TurretLoaderComponent, NewLinkEvent>(OnLink);
        SubscribeLocalEvent<TurretAmmoContainerComponent, ExaminedEvent>(OnContainerExamine);
        SubscribeNetworkEvent<TurretLoaderSyncMessage>(OnSync);
    }

    private void OnContainerExamine(EntityUid uid, TurretAmmoContainerComponent container, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gun-magazine-examine", ("color", "yellow"), ("count", container.AmmoCount)));
    }

    private void GetLoaderState(EntityUid uid, TurretLoaderComponent loader, ref ComponentGetState args)
    {
        args.State = new TurretLoaderState(GetNetEntity(loader.BoundTurretUid), loader.ContainerSlot?.ID);
    }

    public void SetupLoader(EntityUid uid, TurretLoaderComponent loader, EntityUid? turretUid = null)
    {
        if (EntityManager.EntityExists(loader.BoundTurretUid))
        {
            loader.BoundTurretUid = turretUid!.Value;
        }
        else
        {
            if (CheckNetwork(uid, out EntityUid turret))
                loader.BoundTurretUid = turret;
        }

        if (EntityManager.TryGetComponent<ItemSlotsComponent>(uid, out var slots))
        {
            loader.ContainerSlot = slots.Slots["ammoContainer"];

            if (loader.BoundTurretUid != null)
            {
                if (EntityManager.TryGetComponent<CannonComponent>(loader.BoundTurretUid, out var cannon))
                {
                    if(cannon.BoundLoaderUid == null)
                    {
                        cannon.BoundLoaderUid = uid;
                        Dirty(loader.BoundTurretUid.Value, cannon);
                    }
                    else
                    {
                        loader.BoundTurretUid = null;
                    }
                }
            }
        }

        Dirty(uid, loader);
    }

    //ejects ammo container if turret does not accept its ammo type/it's empty
    private void CheckAmmoContainer(EntityUid loaderUid, TurretLoaderComponent loader)
    {
        List<string>? turretAmmoProts = null;
        if (loader.BoundTurretUid != null && EntityManager.EntityExists(loader.BoundTurretUid))
            turretAmmoProts = EntityManager.GetComponent<CannonComponent>(loader.BoundTurretUid.Value).AmmoPrototypes;

        var ammoContainerUid = loader.ContainerSlot?.Item;
        if (TryComp<TurretAmmoContainerComponent>(ammoContainerUid, out var ammoContainer))
        {
            Dirty(ammoContainerUid.Value, ammoContainer);

            if (ammoContainer.AmmoCount == 0 || turretAmmoProts == null || !turretAmmoProts.Contains(ammoContainer.AmmoPrototype))
            {
                _audioSys.PlayPredicted(loader.InvalidAmmoTypeSound, loaderUid, loaderUid);

                if (loader.ContainerSlot == null)
                    return;
                _slotSys.TryEject(loaderUid, loader.ContainerSlot, loaderUid, out _);
            }
        }

        Dirty(loaderUid, loader);
    }

    private bool CheckNetwork(EntityUid uid, out EntityUid turret)
    {
        if (EntityManager.TryGetComponent<DeviceLinkSourceComponent>(uid, out var source))
        {
            if (source.LinkedPorts.Count > 0)
            {
                turret = source.LinkedPorts.Keys.First();
                return true;
            }
        }

        turret = EntityUid.Invalid;
        return false;
    }

    private void OnInit(EntityUid uid, TurretLoaderComponent loader, ComponentInit args)
    {
        SetupLoader(uid, loader);
        CheckAmmoContainer(uid, loader);
    }

    private void OnRemoval(EntityUid uid, TurretLoaderComponent loader, ComponentRemove args)
    {
        if (Exists(loader.BoundTurretUid) && TryComp<CannonComponent>(loader.BoundTurretUid, out var cannon))
            cannon.BoundLoaderUid = null;
    }

    private void OnContainerInsert(EntityUid uid, TurretLoaderComponent loader, EntInsertedIntoContainerMessage args)
    {
        CheckAmmoContainer(uid, loader);
    }

    private void OnContainerRemove(EntityUid uid, TurretLoaderComponent loader, EntRemovedFromContainerMessage args)
    {
        Dirty(uid, loader);
    }

    private void OnLink(EntityUid uid, TurretLoaderComponent loader, NewLinkEvent args)
    {
        SetupLoader(uid, loader, args.Sink);
    }

    private void AfterShot(EntityUid uid, TurretLoaderComponent loader, TurretLoaderAfterShotMessage args)
    {
        CheckAmmoContainer(uid, loader);
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

        _slotSys.TryInsert(uid, component.ContainerSlot, args.Thrown, args.Component.Thrower);
    }

    private void OnHandInteract(EntityUid uid, TurretLoaderComponent component, InteractHandEvent args)
    {
        if (component.ContainerSlot == null)
            return;
        _slotSys.TryEject(uid, component.ContainerSlot, null, out var item);
    }

    private void OnExamined(EntityUid uid, TurretLoaderComponent loader, ExaminedEvent args)
    {
        var ammoCount = 0;

        if (loader.ContainerSlot?.Item != null && TryComp<TurretAmmoContainerComponent>(uid, out var ammoContainer))
            ammoCount = ammoContainer.AmmoCount;

        args.PushMarkup(Loc.GetString("shipevent-turretloader-ammocount-examine", ("count", ammoCount)));
    }

    private void OnSync(TurretLoaderSyncMessage ev)
    {
        EntityUid uid = GetEntity(ev.LoaderUid);
        if (!uid.IsValid() || !TryComp<TurretLoaderComponent>(uid, out var loader))
            return;
        Dirty(uid, loader);
    }
}

public sealed class TurretLoaderAfterShotMessage : EntityEventArgs { }
