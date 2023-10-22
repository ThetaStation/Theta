using System.Threading;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Theta.Impostor.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Theta.Impostor.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Theta.Impostor.Systems;

//tldr: default evac system is shit
public sealed class ImpostorEvacSystem : EntitySystem
{
    /// <summary>
    /// True if evacuation is happening right now
    /// </summary>
    public bool EvacActive;

    /// <summary>
    /// True if evacuation has already happened
    /// </summary>
    public bool EvacFinished;

    public int TriggerDeathCount;

    public int CurrentDeathCount;

    /// <summary>
    /// In minutes
    /// </summary>
    public int LaunchDelay;

    private CancellationTokenSource evacTimerTokenSource = new CancellationTokenSource();
    private CancellationToken evacTimerToken;

    [Dependency] private AudioSystem _audioSys = default!;
    [Dependency] private ChatSystem _chatSys = default!;
    [Dependency] private ShuttleSystem _shuttleSys = default!;
    [Dependency] private ThrusterSystem _thrustSys = default!;
    [Dependency] private DockingSystem _dockSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImpostorEscapeConditionComponent, ObjectiveGetProgressEvent>(OnObjectiveProgressRequest);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnPlayerDeath);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        evacTimerToken = evacTimerTokenSource.Token;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        EvacActive = EvacFinished = false;
        TriggerDeathCount = CurrentDeathCount = 0;
    }

    private void OnPlayerDeath(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !HasComp<MindContainerComponent>(uid))
            return;

        CurrentDeathCount++;
        _audioSys.PlayGlobal("/Audio/Theta/Impostor/flatline.ogg", Filter.Broadcast(), true);

        if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.HasMind)
        {
            if (HasComp<ImpostorRoleComponent>(mindContainer.Mind))
            {
                if (EvacActive && !evacTimerToken.IsCancellationRequested)
                {
                    Announce(Loc.GetString("impostor-announcement-cancelevac"));
                    evacTimerTokenSource.Cancel();
                }
            }
        }

        if (!(EvacActive || EvacFinished) && CurrentDeathCount >= TriggerDeathCount)
        {
            Announce(Loc.GetString("impostor-announcement-beginevac"));
            EvacActive = true;
            Timer.Spawn(LaunchDelay, LaunchPods, evacTimerToken);
        }
    }

    private void LaunchPods()
    {
        Announce(Loc.GetString("impostor-announcement-endevac"));
        EvacActive = false;
        EvacFinished = true;
        
        foreach ((TransformComponent form, ImpostorLocationMarkerComponent marker) in EntityQuery<TransformComponent, ImpostorLocationMarkerComponent>())
        {
            if (marker.Type == ImpostorLandmarkType.EvacPod)
            {
                if (!TryComp<ShuttleComponent>(form.GridUid, out var shuttle))
                    return;

                while(EntityQueryEnumerator<DockingComponent>().MoveNext(out EntityUid uid, out DockingComponent? dock))
                {
                    if (Transform(uid).GridUid != form.GridUid)
                        continue;
                    _dockSys.Undock(uid, dock);
                }
                
                foreach (List<EntityUid> thrustersByDir in shuttle.LinearThrusters)
                {
                    foreach (EntityUid uid in thrustersByDir)
                    {
                        ThrusterComponent thruster = Comp<ThrusterComponent>(uid);
                        _thrustSys.EnableThruster(uid, thruster);
                    }
                }
            }
        }
    }

    private void Announce(string message)
    {
        _chatSys.DispatchGlobalAnnouncement(message, Loc.GetString("impostor-announcement-sender"), true, new SoundPathSpecifier("delta.ogg"), Color.DarkRed);
    }

    private void OnObjectiveProgressRequest(EntityUid uid, ImpostorEscapeConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0;
        
        if (args.Mind.OwnedEntity == null || args.Mind.TimeOfDeath != null)
            return;

        foreach ((TransformComponent form, ImpostorLocationMarkerComponent marker) in EntityQuery<TransformComponent, ImpostorLocationMarkerComponent>())
        {
            if (marker.Type == ImpostorLandmarkType.EvacPod)
            {
                if ((Transform(args.Mind.OwnedEntity.Value).LocalPosition - form.LocalPosition).Length() <= 1 && EvacFinished)
                    args.Progress = 1;
            }
        }
    }
}
