using System.Linq;
using System.Numerics;
using Content.Server.Theta.MapGen.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Theta.MapGen;

/// <summary>
/// System providing modular, procedural generation of various structures
/// </summary>
public sealed class MapGenSystem : EntitySystem
{
    //public fields are for the systems most commonly used by generators & processors,
    //to prevent wasting a bit of time calling IoC every time & for convenience
    [Dependency] public readonly IMapManager MapMan = default!;
    [Dependency] public readonly MapLoaderSystem MapLoader = default!;
    [Dependency] public readonly ITileDefinitionManager TileDefMan = default!;
    [Dependency] public readonly TransformSystem FormSys = default!;
    [Dependency] public readonly IPrototypeManager ProtMan = default!;
    [Dependency] public readonly IRobustRandom Random = default!;
    public IEntityManager EntMan => EntityManager;

    public MapId TargetMap = MapId.Nullspace;
    public Vector2i StartPos;
    public int MaxSpawnOffset;
    public List<EntityUid> SpawnedGrids = new();

    //primitive quad tree (aka plain grid) for optimising collision checks
    public const int SectorSize = 100;
    private Dictionary<Vector2i, HashSet<SectorRange>> _spawnSectors = new(); //sector pos => free ranges in this sector
    private Dictionary<Vector2i, double> _spawnSectorVolumes = new(); //sector pos => occupied volume in this sector

    /// <summary>
    /// Randomly places specified structures onto map
    /// </summary>
    /// <param name="targetMap">Selected map</param>
    /// <param name="startPos">Bottom-left corner of spawning square</param>
    /// <param name="structures">List of structure prototypes to spawn</param>
    /// <param name="globalProcessors">List of processors which should run after all structures were spawned</param>
    /// <param name="structureAmount">Amount of structures to spawn</param>
    /// <param name="maxOffset">Spawning square size</param>
    public void SpawnStructures(
        MapId targetMap,
        Vector2i startPos,
        int structureAmount,
        int maxOffset,
        List<StructurePrototype> structures,
        List<IMapGenProcessor> globalProcessors,
        IMapGenDistribution distribution)
    {
        if (targetMap == MapId.Nullspace || !MapMan.MapExists(targetMap))
            return;
        TargetMap = targetMap;
        MapMan.SetMapPaused(TargetMap, true);

        StartPos = startPos;
        MaxSpawnOffset = maxOffset;
        SetupGrid(startPos, maxOffset);

        for (int n = 0; n < structureAmount; n++)
        {
            var structProt = PickStructure(structures);
            if (structProt == null)
            {
                Log.Warning("MapGenSystem, SpawnStructures: Could not pick structure prototype, skipping");
                continue;
            }

            var grid = structProt.Generator.Generate(this, TargetMap);
            var gridComp = EntMan.GetComponent<MapGridComponent>(grid);

            var spawnPos = GenerateSpawnPosition((Box2i) gridComp.LocalAABB.Enlarged(structProt.MinDistance), distribution, 50);
            if (spawnPos == null)
            {
                Log.Warning("MapGenSystem, SpawnStructures: Failed to find spawn position, deleting grid");
                EntityManager.DeleteEntity(grid);
                continue;
            }

            Vector2 pos = spawnPos.Value;
            pos.X += structProt.MinDistance - gridComp.LocalAABB.Left;
            pos.Y += structProt.MinDistance - gridComp.LocalAABB.Bottom;

            FormSys.SetWorldPosition(grid, pos);
            SpawnedGrids.Add(grid);
            foreach (var proc in structProt.Processors)
            {
                proc.Process(this, TargetMap, grid, false);
            }
        }

        foreach (var proc in globalProcessors)
        {
            proc.Process(this, TargetMap, MapMan.GetMapEntityId(TargetMap), true);
        }

        MapMan.SetMapPaused(TargetMap, false);
        TargetMap = MapId.Nullspace;
        Log.Info($"MapGenSystem, SpawnStructures: Spawned {SpawnedGrids.Count} grids");
        SpawnedGrids.Clear();

        StartPos = Vector2i.Zero;
        MaxSpawnOffset = 0;

        _spawnSectors.Clear();
        _spawnSectorVolumes.Clear();
    }

    /// <summary>
    /// Randomly places specified structure onto map. Does not optimise collision checking in any way
    /// </summary>
    public EntityUid RandomPosSpawn(MapId targetMap, Vector2 startPos, int maxOffset, int tries,
        StructurePrototype structure, List<IMapGenProcessor>? extraProcessors = null, bool forceIfFailed = false)
    {
        TargetMap = targetMap;

        var grid = structure.Generator.Generate(this, TargetMap);
        var gridComp = EntMan.GetComponent<MapGridComponent>(grid);
        var gridForm = EntMan.GetComponent<TransformComponent>(grid);
        var bounds = new Box2(startPos.X,
            startPos.Y,
            startPos.X + maxOffset - gridComp.LocalAABB.Width,
            startPos.Y + maxOffset - gridComp.LocalAABB.Height);

        var finalDistance = (int) Math.Ceiling(structure.MinDistance + Math.Max(gridComp.LocalAABB.Height, gridComp.LocalAABB.Width));

        Vector2i mapPos = Vector2i.Zero;
        var result = false;
        for (int n = 0; n < tries; n++)
        {
            mapPos = (Vector2i) Random.NextVector2Box(
                bounds.Left + gridComp.LocalAABB.Width,
                bounds.Bottom + gridComp.LocalAABB.Height,
                bounds.Right - gridComp.LocalAABB.Width,
                bounds.Top - gridComp.LocalAABB.Height
                ).Rounded();
            if (!MapMan.FindGridsIntersecting(targetMap,
                    new Box2(mapPos - finalDistance, mapPos + finalDistance)).Any())
            {
                result = true;
                break;
            }
        }

        TargetMap = MapId.Nullspace;

        if (result)
        {
            Log.Info($"MapGenSystem, RandomPosSpawn: Spawned grid {grid.ToString()} successfully");
            FormSys.SetWorldPosition(grid, mapPos);
        }
        else if (forceIfFailed)
        {
            Log.Info($"MapGenSystem, RandomPosSpawn: Failed to find spawn position for grid {grid.ToString()}," +
                        "but forceIfFailed is set to true; proceeding to force-spawn");
            mapPos = (Vector2i) Random.NextVector2Box(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top).Rounded();

            FormSys.SetWorldPosition(grid, mapPos);
        }

        if (result || forceIfFailed)
        {
            if (extraProcessors != null)
            {
                foreach (IMapGenProcessor extraProc in extraProcessors)
                {
                    extraProc.Process(this, targetMap, grid, false);
                }
            }

            return grid;
        }

        Log.Error($"MapGenSystem, RandomPosSpawn: Failed to find spawn position, deleting grid {grid.ToString()}");
        EntityManager.DeleteEntity(grid);
        return EntityUid.Invalid;
    }

    /// <summary>
    /// Deletes every grid in given area
    /// </summary>
    public void ClearArea(MapId targetMap, Box2i bounds)
    {
        foreach (MapGridComponent grid in MapMan.FindGridsIntersecting(targetMap, bounds))
        {
            EntMan.DeleteEntity(grid.Owner);
        }
    }

    /// <summary>
    /// Sets up collision grid
    /// </summary>
    /// <param name="startPos">Bottom-left corner of grid's square</param>
    /// <param name="maxOffset">Grid's size</param>
    private void SetupGrid(Vector2i startPos, int maxOffset)
    {
        for (int y = 0; y < maxOffset; y += SectorSize)
        {
            for (int x = 0; x < maxOffset; x += SectorSize)
            {
                Vector2i sectorPos = new Vector2i(startPos.X + x, startPos.Y + y);
                _spawnSectors[sectorPos] = new HashSet<SectorRange>
                {
                    new SectorRange(sectorPos.Y, sectorPos.Y + SectorSize,
                        new List<(int, int)>{(sectorPos.X, sectorPos.X + SectorSize)})
                };
                _spawnSectorVolumes[sectorPos] = SectorSize * SectorSize;
            }
        }
    }

    /// <summary>
    /// Randomly picks structures from structure list, accounting for their weight
    /// </summary>
    private StructurePrototype? PickStructure(List<StructurePrototype> structures)
    {
        float totalWeight = structures.Select(s => s.SpawnWeight).Sum();
        float randFloat = Random.NextFloat(0, totalWeight);

        StructurePrototype? picked = null;
        foreach (var structProt in structures)
        {
            if (structProt.SpawnWeight > randFloat)
            {
                picked = structProt;
                break;
            }

            randFloat -= structProt.SpawnWeight;
        }

        return picked;
    }

    /// <summary>
    /// Generates spawn position in random sector of the grid
    /// </summary>
    private Vector2? GenerateSpawnPosition(Box2i bounds, IMapGenDistribution distribution, int tries)
    {
        var volume = bounds.Height * bounds.Width;

        for (int i = 0; i < tries; i++)
        {
            Vector2i sectorPos = (distribution.Generate(this) / SectorSize).Floored() * SectorSize;
            if (_spawnSectorVolumes[sectorPos] < volume)
                continue;
            if (TryPlaceInSector(sectorPos, bounds, out Vector2i spawnPos))
                return spawnPos;
        }

        return null;
    }

    /// <summary>
    /// Tries to find spot with enough space to fit given bounding box
    /// </summary>
    /// <param name="sectorPos">Position of sector for search</param>
    /// <param name="bounds">Bounds</param>
    /// <param name="resultPos">Result</param>
    /// <returns>Bool signifying whether position was found or not</returns>
    private bool TryPlaceInSector(Vector2i sectorPos, Box2i bounds, out Vector2i resultPos)
    {
        bool result = false;
        resultPos = Vector2i.Zero;
        int lx, ly, hx, hy;
        lx = ly = hx = hy = 0;

        foreach (SectorRange range in _spawnSectors[sectorPos])
        {
            foreach ((int start, int end) in range.XRanges)
            {
                if (end - start >= bounds.Width)
                {
                    if (range.Top - range.Bottom >= bounds.Height)
                    {
                        result = true;
                        lx = start;
                        hx = end - bounds.Width;
                        ly = range.Bottom;
                        hy = range.Top - bounds.Height;
                        break;
                    }
                    SectorRange combinedRange = CombineRangesVertically(_spawnSectors[sectorPos], start, end, range.Bottom, range.Top, bounds.Width);

                    if (combinedRange.Top - combinedRange.Bottom >= bounds.Height)
                    {
                        result = true;
                        (int startc, int endc) = combinedRange.XRanges.First();

                        lx = startc;
                        hx = endc - bounds.Width;
                        ly = combinedRange.Bottom;
                        hy = combinedRange.Top - bounds.Height;
                        break;
                    }
                }
            }

            if (result)
            {
                resultPos = new Vector2i(Random.Next(lx, hx), Random.Next(ly, hy));
                break;
            }
        }

        if (result)
        {
            _spawnSectors[sectorPos] = SubtractRange(_spawnSectors[sectorPos],
                    RangeFromBox(
                        Box2i.FromDimensions(resultPos, new Vector2i(bounds.Width, bounds.Height))
                        )
                    );
            _spawnSectorVolumes[sectorPos] -= bounds.Height * bounds.Width;
        }

        return result;
    }

    /// <summary>
    /// Converts Box2i to SectorRange (area inside the box is considered free)
    /// </summary>
    private SectorRange RangeFromBox(Box2i box)
    {
        return new SectorRange(box.Bottom, box.Top, new List<(int, int)> {(box.Left, box.Right)});
    }

    /// <summary>
    /// Subtracts range from existing ranges
    /// </summary>
    private HashSet<SectorRange> SubtractRange(HashSet<SectorRange> ranges, SectorRange range)
    {
        HashSet<SectorRange> rangesNew = new();
        foreach (SectorRange rangeOther in ranges)
        {
            if (!(rangeOther.Top < range.Bottom || rangeOther.Bottom > range.Top)) //height overlap
            {
                int overlapBottom = rangeOther.Bottom;
                int overlapTop = rangeOther.Top;

                if (range.Bottom > rangeOther.Bottom)
                {
                    rangesNew.Add(new SectorRange(rangeOther.Bottom, range.Bottom, rangeOther.XRanges));
                    overlapBottom = range.Bottom;
                }
                if (range.Top < rangeOther.Top)
                {
                    rangesNew.Add(new SectorRange(range.Top, rangeOther.Top, rangeOther.XRanges));
                    overlapTop = range.Top;
                }

                rangesNew.Add(new SectorRange(overlapBottom, overlapTop, SubtractXRanges(rangeOther.XRanges, range.XRanges)));
            }
            else
            {
                rangesNew.Add(rangeOther);
            }
        }

        foreach (SectorRange nrange in rangesNew)
        {
            if (nrange.Top - nrange.Bottom == 0 || nrange.XRanges.Count == 0)
                rangesNew.Remove(nrange);
        }

        return rangesNew;
    }

    /// <summary>
    /// Combines all ranges lying between end X & start X, and above/below height into a single range (with single X range)
    /// with width above minWidth and combined height of included ranges
    /// </summary>
    private SectorRange CombineRangesVertically(HashSet<SectorRange> ranges, int start, int end, int heightBottom, int heightTop, int minWidth)
    {
        int startn, endn, bottomn, topn;
        (startn, endn, bottomn, topn) = (start, end, heightBottom, heightTop);

        SectorRange? GetNextFreeRange(bool above, int height)
        {
            List<SectorRange> sranges = ranges.Where(x => above ? x.Bottom >= height : x.Top <= height).
                OrderBy(x => above ? x.Top : x.Bottom).ToList();
            if (!above)
                sranges.Reverse();

            if (sranges.Count == 0)
                return null;
            SectorRange srange = sranges[0];

            if (Math.Abs((above ? srange.Bottom : srange.Top) - (above ? topn : bottomn)) > 1)
                return null;

            foreach ((int startf, int endf) in srange.XRanges)
            {
                int startnn = startf > startn ? startf : startn;
                int endnn = endf < endn ? endf : endn;
                if (endnn - startnn < minWidth)
                    continue;
                return new SectorRange(srange.Bottom, srange.Top, new List<(int, int)>{(startnn, endnn)});
            }

            return null;
        }

        while (true)
        {
            SectorRange? r = GetNextFreeRange(false, bottomn);
            if (r == null)
                break;
            bottomn = r.Value.Bottom;
            (startn, endn) = r.Value.XRanges[0];

        }

        while (true)
        {
            SectorRange? r = GetNextFreeRange(true, topn);
            if (r == null)
                break;
            topn = r.Value.Top;
            (startn, endn) = r.Value.XRanges[0];
        }

        return new SectorRange(bottomn, topn, new List<(int, int)> {(startn, endn)});
    }

    /// <summary>
    /// Returns true if atleast one x-range overlaps another
    /// </summary>
    private bool XRangesOverlap(List<(int, int)> ranges1, List<(int, int)> ranges2)
    {
        foreach ((int start1, int end1) in ranges1)
        {
            foreach ((int start2, int end2) in ranges2)
            {
                if (start1 < end2 && end1 > start2)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns list of inverted (occupied) ranges
    /// </summary>
    private List<(int, int)> InvertedXRanges(List<(int, int)> ranges)
    {
        List<(int, int)> results = new();
        foreach ((int start, int end) in ranges)
        {
            (int, int) nextRange = ranges.Where(r => r.Item1 > end).OrderBy(r => r.Item1).FirstOrDefault();
            if (nextRange == default)
                continue;
            results.Add((end, nextRange.Item1));
        }

        return results;
    }

    /// <summary>
    /// Subtracts ranges1 from ranges2
    /// </summary>
    private List<(int, int)> SubtractXRanges(List<(int, int)> ranges1, List<(int, int)> ranges2)
    {
        List<(int, int)> SubtractOne(List<(int, int)> ranges, (int, int) range)
        {
            List<(int, int)> r = new();
            foreach ((int start, int end) in ranges)
            {
                if (!(start > range.Item2 || end < range.Item1))
                {
                    if (end > range.Item1 && range.Item1 > start)
                        r.Add((start, range.Item1));
                    if (end > range.Item2 && range.Item2 > start)
                        r.Add((range.Item2, end));
                }
                else
                {
                    r.Add((start, end));
                }
            }

            return r;
        }

        List<(int, int)> newRanges = new(ranges1);
        foreach ((int, int) range2 in ranges2)
        {
            newRanges = SubtractOne(ranges1, range2);
        }

        return newRanges;
    }

    /// <summary>
    /// SectorRange represents single 'line' of sector space. It contains info about it's height (Bottom, Top) & free spaces on that height level (XRanges)
    /// </summary>
    private struct SectorRange
    {
        public int Bottom, Top;
        public List<(int, int)> XRanges;

        public SectorRange(int bottom, int top, List<(int, int)> xRanges)
        {
            Bottom = bottom;
            Top = top;
            XRanges = xRanges;
        }
    }
}

/// <summary>
/// Distribution is used for generating spawn positions. It may be simple randint or some 2D noise.
/// </summary>
public interface IMapGenDistribution
{
    public Vector2 Generate(MapGenSystem sys);
}

/// <summary>
/// Generator is a base class for generating structures
/// </summary>
[ImplicitDataDefinitionForInheritors]
public partial interface IMapGenGenerator
{
    public EntityUid Generate(MapGenSystem sys, MapId targetMap);
}

/// <summary>
/// Processor is a base class for post-processing structures
/// </summary>
[ImplicitDataDefinitionForInheritors]
public partial interface IMapGenProcessor
{
    public void Process(MapGenSystem sys, MapId targetMap, EntityUid gridUid, bool isGlobal);
}