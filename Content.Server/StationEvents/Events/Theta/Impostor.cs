using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.Shuttles.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Theta.Impostor.Components;
using Content.Server.Theta.Impostor.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Theta.Impostor.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events.Theta;

[RegisterComponent, Access(typeof(ImpostorRuleSystem))]
public sealed partial class ImpostorRuleComponent : Component
{
    [DataField("impostorAntagId", required: true)] public string ImpostorAntagId = "";
    [DataField("impostorMandatoryObjectives", required: true)] public List<string> ImpostorMandatoryObjectives = default!;
    [DataField("impostorRandomObjectivesGroupId")] public string? ImpostorRandomObjectivesGroupId;
    [DataField("impostorAmount")] public int ImpostorAmount = 1;
    [DataField("randomObjectiveAmount")] public int RandomObjectiveAmount;
    [DataField("evacMinDeathCount", required: true)] public int EvacMinDeathCount;
    //to prevent impostor from using gay tactics, like plasmaflooding or sitting in the bunker alone during radstorm
    [DataField("evacMaxDeathCount", required: true)] public int EvacMaxDeathCount;
    [DataField("evacLaunchDelay", required: true)] public int EvacLaunchDelay; //in minutes
    [DataField("impostorGreetingSound")] public SoundSpecifier? ImpostorGreetingSound;
}

public sealed partial class ImpostorRuleSystem : StationEventSystem<ImpostorRuleComponent>
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MindSystem _mindSys = default!;
    [Dependency] private JobSystem _jobSys = default!;
    [Dependency] private RoleSystem _roleSys = default!;
    [Dependency] private ObjectivesSystem _objectiveSys = default!;
    [Dependency] private ChatSystem _chatSys = default!;
    [Dependency] private AudioSystem _audioSys = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private ImpostorEvacSystem _specEvacSys = default!;
    [Dependency] private EntityStorageSystem _storageSys = default!;
    [Dependency] private IPrototypeManager _protMan = default!;
    [Dependency] private HumanoidAppearanceSystem _appearanceSys = default!;

    private const string ImpostorAntagId = "Impostor";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnAfterSpawn);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        ImpostorRuleComponent? rule = EntityQuery<ImpostorRuleComponent>().FirstOrDefault();
        if (rule == null)
            return;
        
        EntityQueryEnumerator<ImpostorRoleComponent> query = EntityQueryEnumerator<ImpostorRoleComponent>();
        while(query.MoveNext(out EntityUid uid, out ImpostorRoleComponent? _))
        {
            if (TryComp(uid, out MindComponent? mind))
            {
                if (mind.OwnedEntity == null || mind.TimeOfDeath != null)
                {
                    ev.AddLine(Loc.GetString("impostor-roundend-impostoralive"));
                }
            }
        }
        
        ev.AddLine(Loc.GetString("impostor-roundend-impostordead"));
    }

    protected override void Started(EntityUid uid, ImpostorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _specEvacSys.RuleSelected = true;
        _specEvacSys.MinDeathCount = component.EvacMinDeathCount;
        _specEvacSys.MaxDeathCount = component.EvacMaxDeathCount;
        _specEvacSys.LaunchDelay = component.EvacLaunchDelay;
    }

    private void OnBeforeSpawn(RulePlayerSpawningEvent ev)
    {
        ImpostorRuleComponent? rule = EntityQuery<ImpostorRuleComponent>().FirstOrDefault();
        if (rule == null)
            return;

        var pods = EntityQueryEnumerator<ImpostorCryoPodComponent, EntityStorageComponent>();
        
        foreach (IPlayerSession session in ev.PlayerPool)
        {
            if (pods.MoveNext(out EntityUid uid, out _, out EntityStorageComponent? storage))
            {
                HumanoidCharacterProfile profile = ev.Profiles[session.UserId];
                EntityUid bodyUid = SpawnInContainerOrDrop("MobHuman", uid, storage.Contents.ID);
                EntityUid mindUid = _mindSys.CreateMind(session.UserId);
                _appearanceSys.LoadProfile(bodyUid, profile);
                _mindSys.SetUserId(mindUid, session.UserId);
                _mindSys.TransferTo(mindUid, bodyUid);
                _ticker.PlayerJoinGame(session, true);
            }
        }
        
        ev.PlayerPool.Clear();
    }

    private void OnAfterSpawn(RulePlayerJobsAssignedEvent ev)
    {
        ImpostorRuleComponent? rule = EntityQuery<ImpostorRuleComponent>().FirstOrDefault();
        if (rule == null)
            return;
        
        List<IPlayerSession> candidates = GetPotentialImpostors(ev.Players, ev.Profiles, rule);
        if (candidates.Count == 0)
            return;
        
        for (int i = 0; i < rule.ImpostorAmount; i++)
        {
            MakeImpostor(_rand.Pick(candidates), rule);
        }
    }

    private void MakeImpostor(IPlayerSession player, ImpostorRuleComponent rule)
    {
        if (_mindSys.TryGetMind(player.UserId, out var mindUid, out var mind))
        {
            _roleSys.MindAddRole(mindUid.Value, new ImpostorRoleComponent {PrototypeId = ImpostorAntagId});

            foreach (string mandatoryObjective in rule.ImpostorMandatoryObjectives)
            {
                EntityUid? objectiveUid = _objectiveSys.TryCreateObjective(mindUid.Value, mind, mandatoryObjective);
                if(objectiveUid != null)
                    _mindSys.AddObjective(mindUid.Value, mind, objectiveUid.Value);
            }

            if (rule.ImpostorRandomObjectivesGroupId != null)
            {
                for (int i = 0; i < rule.RandomObjectiveAmount; i++)
                {
                    EntityUid? objective = _objectiveSys.GetRandomObjective(mindUid.Value, mind, rule.ImpostorRandomObjectivesGroupId);
                    if (objective == null) 
                        return;
                    _mindSys.AddObjective(mindUid.Value, mind, objective.Value);
                }
            }

            _chatSys.SendSimpleMessage(Loc.GetString("impostor-greeting"), player);
            _audioSys.PlayGlobal(rule.ImpostorGreetingSound, player);
        }
    }

    private List<IPlayerSession> GetPotentialImpostors(IPlayerSession[] playerPool, 
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles, ImpostorRuleComponent rule)
    {
        List<IPlayerSession> validPlayers = new();
        EntityQuery<PendingClockInComponent> pendingPlayers = GetEntityQuery<PendingClockInComponent>();
        foreach (IPlayerSession player in playerPool)
        {
            if (!_jobSys.CanBeAntag(player))
                continue;
            
            if (player.AttachedEntity != null && pendingPlayers.HasComponent(player.AttachedEntity.Value))
                continue;

            validPlayers.Add(player);
        }

        List<IPlayerSession> playersWithPref = new();
        foreach (IPlayerSession player in validPlayers)
        {
            if (!profiles.ContainsKey(player.UserId))
                continue;
            
            if (profiles[player.UserId].AntagPreferences.Contains(rule.ImpostorAntagId))
                playersWithPref.Add(player);
        }
        if (playersWithPref.Count < rule.ImpostorAmount)
        {
            Log.Info("Insufficient preferred impostors, picking at random.");
            return validPlayers;
        }
        
        return playersWithPref;
    }
}
