using Content.Server.Theta.MapGen;
using Content.Server.Theta.MapGen.Distributions;
using Content.Server.Theta.MapGen.Generators;
using Content.Server.Theta.MapGen.Processors;
using Content.Server.Theta.MapGen.Prototypes;
using Content.Server.Theta.ShipEvent.Components;
using Content.Server.Theta.ShipEvent.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random;
using Content.Shared.Shuttles.Components;
using Content.Shared.Theta.ShipEvent;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.StationEvents.Events.Theta;

[RegisterComponent, Access(typeof(ShipEventRule))]
public sealed partial class ShipEventRuleComponent : Component
{
    //all time related fields are in seconds

    //time
    [DataField("roundDuration")] public int RoundDuration; //set to negative if you don't need a timed round end
    [DataField("teamCheckInterval")] public float TeamCheckInterval;
    [DataField("playerCheckInterval")] public float PlayerCheckInterval;
    [DataField("respawnDelay")] public int RespawnDelay;
    [DataField("bonusInterval")] public int BonusInterval;
    [DataField("boundsCompressionInterval")] public float BoundsCompressionInterval;
    [DataField("pickupsSpawnInterval")] public float PickupsSpawnInterval;
    [DataField("anomalyUpdateInterval")] public float AnomalyUpdateInterval;
    [DataField("anomalySpawnInterval")] public float AnomalySpawnInterval;
    [DataField("modifierUpdateInterval")] public float ModifierUpdateInterval;

    //points
    [DataField("pointsPerInterval")] public int PointsPerInterval;
    [DataField("pointsPerHitMultiplier")] public float PointsPerHitMultiplier;
    [DataField("pointsPerAssist")] public int PointsPerAssist;
    [DataField("pointsPerKill")] public int PointsPerKill;
    [DataField("outOfBoundsPenalty")] public int OutOfBoundsPenalty;

    //mapgen
    [DataField("initialObstacleAmount")] public int InitialObstacleAmount;
    [DataField("obstacleTypes")] public List<string> ObstacleTypes = new();
    [DataField("obstacleAmountAmplitude")] public int ObstacleAmountAmplitude;
    [DataField("obstacleSizeAmplitude")] public int ObstacleSizeAmplitude;
    [DataField("minFieldSize")] public int MinFieldSize;
    [DataField("maxFieldSize")] public int MaxFieldSize;
    [DataField("metersPerPlayer")] public int MetersPerPlayer; //scaling field based on online (at roundstart)
    [DataField("roundFieldSizeTo")] public int RoundFieldSizeTo;
    [DataField("useNoise")] public bool UseNoise;
    [DataField("noiseGenerator")] public FastNoiseLite? NoiseGenerator;
    [DataField("noiseThreshold")] public float NoiseThreshold;

    //misc
    [DataField("spaceLightColor")] public Color? SpaceLightColor = null;
    [DataField("hudPrototypeId")] public string HUDPrototypeId = "";
    [DataField("captainHudPrototypeId")] public string CaptainHUDPrototypeId = "";
    [DataField("shipTypes")] public List<string> ShipTypes = new();
    [DataField("boundsCompressionDistance")] public int BoundsCompressionDistance;
    [DataField("pickupsPositions")] public int PickupsPositionsCount;
    [DataField("pickupMinDistance")] public float PickupMinDistance;
    [DataField("pickupPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<WeightedRandomEntityPrototype>))]
    public string PickupPrototype = default!;
    [DataField("anomalyPrototypes", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> AnomalyPrototypes = new();
    [DataField("modifierAmount")] public int ModifierAmount;
    [DataField("modifierPrototypes", customTypeSerializer: typeof(PrototypeIdListSerializer<ShipEventModifierPrototype>))]
    public List<string> ModifierPrototypes = new();
}

public sealed class ShipEventRule : StationEventSystem<ShipEventRuleComponent>
{
    [Dependency] private ShipEventTeamSystem _shipSys = default!;
    [Dependency] private MapGenSystem _mapGenSys = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private IRobustRandom _rand = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipEventRuleComponent, ComponentShutdown>(OnEventShutDown);
    }

    // for tests
    private void OnEventShutDown(Entity<ShipEventRuleComponent> ent, ref ComponentShutdown args)
    {
        _shipSys.RuleSelected = false;
    }

    //Creates ComponentRegistryEntry for ChangeIFFOnSplit comp. Used by AddComponentProcessor to prevent splitted grids from getting labels.
    //todo: this is ugly hardcode, better to move it into prototype or somethin, when I will understand how
    private EntityPrototype.ComponentRegistryEntry CreateIFFCompEntry()
    {
        var iffSplitComp = new ChangeIFFOnSplitComponent();
        iffSplitComp.NewFlags = IFFFlags.HideLabel;
        iffSplitComp.Replicate = true;
        iffSplitComp.DeleteInheritedGridsDelay = 120;

        MappingDataNode mapping = new MappingDataNode(new Dictionary<DataNode, DataNode>
        {
            {new ValueDataNode("flags"), new ValueDataNode(IFFFlags.HideLabel.ToString())},
            {new ValueDataNode("replicate"), new ValueDataNode("true")}
        });

        return new EntityPrototype.ComponentRegistryEntry(iffSplitComp, mapping);
    }

    protected override void Started(EntityUid uid, ShipEventRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var map = _mapMan.CreateMap();
        if (component.SpaceLightColor != null)
            EnsureComp<MapLightComponent>(_mapMan.GetMapEntityId(map)).AmbientLightColor = component.SpaceLightColor.Value;
        _shipSys.TargetMap = map;

        _shipSys.RoundDuration = component.RoundDuration;
        _shipSys.TimedRoundEnd = component.RoundDuration > 0;
        _shipSys.TeamCheckInterval = component.TeamCheckInterval;
        _shipSys.PlayerCheckInterval = component.PlayerCheckInterval;
        _shipSys.RespawnDelay = component.RespawnDelay;
        _shipSys.BonusInterval = component.BonusInterval;
        _shipSys.PointsPerInterval = component.PointsPerInterval;
        _shipSys.PointsPerHitMultiplier = component.PointsPerHitMultiplier;
        _shipSys.PointsPerAssist = component.PointsPerAssist;
        _shipSys.PointsPerKill = component.PointsPerKill;
        _shipSys.OutOfBoundsPenalty = component.OutOfBoundsPenalty;

        _shipSys.PickupsPositionsCount = component.PickupsPositionsCount;
        _shipSys.PickupSpawnInterval = component.PickupsSpawnInterval;
        _shipSys.PickupMinDistance = component.PickupMinDistance;
        _shipSys.PickupPrototype = component.PickupPrototype;

        _shipSys.HUDPrototypeId = component.HUDPrototypeId;
        _shipSys.CaptainHUDPrototypeId = component.CaptainHUDPrototypeId;

        _shipSys.MaxSpawnOffset = Math.Clamp(
            (int) Math.Round((float) _playerMan.PlayerCount * component.MetersPerPlayer / component.RoundFieldSizeTo) * component.RoundFieldSizeTo,
            component.MinFieldSize,
            component.MaxFieldSize);

        _shipSys.BoundsCompressionInterval = component.BoundsCompressionInterval;
        _shipSys.BoundsCompression = component.BoundsCompressionInterval > 0;
        _shipSys.BoundsCompressionDistance = component.BoundsCompressionDistance;

        foreach (var shipTypeProtId in component.ShipTypes)
        {
            _shipSys.ShipTypes.Add(_protMan.Index<ShipTypePrototype>(shipTypeProtId));
        }

        _shipSys.AnomalyUpdateInterval = component.AnomalyUpdateInterval;
        _shipSys.AnomalySpawnInterval = component.AnomalySpawnInterval;
        foreach (var anomalyProtId in component.AnomalyPrototypes)
        {
            _shipSys.AnomalyPrototypes.Add(_protMan.Index<EntityPrototype>(anomalyProtId));
        }

        _shipSys.ModifierUpdateInterval = component.ModifierUpdateInterval;
        _shipSys.ModifierAmount = component.ModifierAmount;
        foreach (var modifierProtId in component.ModifierPrototypes)
        {
            _shipSys.AllModifiers.Add(_protMan.Index<ShipEventModifierPrototype>(modifierProtId));
        }

        List<StructurePrototype> obstacleStructProts = new();
        foreach (var structProtId in component.ObstacleTypes)
        {
            var structProt = _protMan.Index<StructurePrototype>(structProtId);

            //todo: remove this horror after proper map gen adjustment system is made
            var randomSize = _rand.Next(-component.ObstacleSizeAmplitude, component.ObstacleSizeAmplitude);
            if (structProt.Generator is AsteroidGenerator gen)
            {
                var ratio = (gen.Size + randomSize) / gen.Size;
                gen.MaxCircleRadius *= ratio;
                gen.MaxCircleRadius *= ratio;
                structProt.MinDistance += randomSize;
                gen.Size += randomSize;
            }

            obstacleStructProts.Add(structProt);
        }

        AddComponentsProcessor iffSplitProc = new();
        iffSplitProc.Components = new ComponentRegistry(
            new()
            {
                {"ChangeIFFOnSplit", CreateIFFCompEntry()}
            }
        );

        FlagIFFProcessor iffFlagProc = new();
        iffFlagProc.Flags = new() { IFFFlags.HideLabel };
        iffFlagProc.ColorOverride = Color.Gold;

        List<IMapGenProcessor> globalProcessors = new() { iffSplitProc, iffFlagProc };

        IMapGenDistribution distribution = new SimpleDistribution();
        if (_rand.Prob(0.05f))
        {
            distribution = new FunnyDistribution();
        }
        else
        {
            if (component.UseNoise && component.NoiseGenerator != null)
                distribution = new NoiseDistribution(
                component.NoiseGenerator,
                _shipSys.MaxSpawnOffset / MapGenSystem.SectorSize,
                component.NoiseThreshold);
        }

        _mapGenSys.SpawnStructures(map,
            Vector2i.Zero,
            component.InitialObstacleAmount + _rand.Next(-component.ObstacleAmountAmplitude, component.ObstacleAmountAmplitude),
            _shipSys.MaxSpawnOffset,
            obstacleStructProts,
            globalProcessors,
            distribution);
        _shipSys.ShipProcessors.Add(iffSplitProc);

        _shipSys.RuleSelected = true;
    }
}
