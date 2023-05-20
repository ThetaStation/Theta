using System.Linq;
using Content.Server.Theta.DebrisGeneration.Prototypes;
using Content.Shared.Follower;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Theta.DebrisGeneration;

/// <summary>
/// System providing modular, procedural generation of various stru
/// </summary>
public sealed class DebrisGenerationSystem : EntitySystem
{
    //public fields are for the systems most commonly used by generators & processors,
    //to prevent wasting a bit of time calling IoC every time & for convenience
    [Dependency] public readonly IMapManager MapMan = default!;
    [Dependency] public readonly MapLoaderSystem MapLoader = default!;
    [Dependency] public readonly ITileDefinitionManager TileDefMan = default!;
    [Dependency] public readonly TransformSystem FormSys = default!;
    [Dependency] public readonly IPrototypeManager ProtMan = default!;
    [Dependency] public readonly IRobustRandom Rand = default!;
    public IEntityManager EntMan => EntityManager;
    
    public MapId TargetMap = MapId.Nullspace;
    public List<EntityUid> SpawnedGrids = new();

    //primitive quad tree (aka plain grid) for optimising collision checks
    private const int spawnSectorSize = 100;
    private Dictionary<Vector2, HashSet<SectorRange>> spawnSectors = new(); //sector pos => free ranges in this sector
    private Dictionary<Vector2, double> spawnSectorVolumes = new(); //sector pos => occupied volume in this sector

    /// <summary>
    /// Randomly places specified structures onto map
    /// </summary>
    /// <param name="targetMap">selected map</param>
    /// <param name="startPos">starting position from which to spawn structures</param>
    /// <param name="structures">list of structure prototypes to spawn</param>
    /// <param name="globalProcessors">list of processors which should run after all structures were spawned</param>
    /// <param name="debrisAmount">amount of structures to spawn</param>
    /// <param name="maxDebrisOffset">max offset from startPos. startPos is being the left-lower corner of the square in which spawning positions are chosen</param>
    public void SpawnStructures(
        MapId targetMap,
        Vector2 startPos,
        int structureAmount,
        int maxOffset,
        List<StructurePrototype> structures, 
        List<Processor> globalProcessors)
    {
        if (targetMap == MapId.Nullspace || !MapMan.MapExists(targetMap))
            return;
        TargetMap = targetMap;
        MapMan.SetMapPaused(TargetMap, true);
        
        SetupGrid(startPos, maxOffset);
        
        for (int n = 0; n < structureAmount; n++)
        {
            var structProt = PickStructure(structures);
            if (structProt == null)
            {
                Logger.Warning("Debris generation, GenerateDebris: Could not pick structure prototype, skipping");
                continue;
            }

            var grid = structProt.Generator.Generate(this, TargetMap);
            var gridComp = EntMan.GetComponent<MapGridComponent>(grid);
            var gridForm = EntMan.GetComponent<TransformComponent>(grid);
            
            var spawnPos = GenerateSpawnPosition(gridComp.LocalAABB.Enlarged(structProt.MinDistance));
            if (spawnPos == null)
            {
                Logger.Error("Debris generation, GenerateDebris: Failed to find spawn position, deleting grid");
                EntityManager.DeleteEntity(grid);
                continue;
            }
            
            gridForm.Coordinates = new EntityCoordinates(gridForm.Coordinates.EntityId, spawnPos.Value);
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
        Logger.Info($"Debris generation, GenerateDebris: Spawned {SpawnedGrids.Count} grids");
        SpawnedGrids.Clear();

        spawnSectors.Clear();
        spawnSectorVolumes.Clear();
    }
    
    /// <summary>
    /// Randomly places specified structure onto map. Does not optimise collision checking in any way
    /// </summary>
    public EntityUid RandomPosSpawn(MapId targetMap, Vector2 startPos, int maxOffset, int tries, StructurePrototype structure, List<Processor> extraProcessors)
    {
        TargetMap = targetMap;
        
        var grid = structure.Generator.Generate(this, TargetMap);
        var gridComp = EntMan.GetComponent<MapGridComponent>(grid);
        var gridForm = EntMan.GetComponent<TransformComponent>(grid);

        var finalDistance = (int)Math.Ceiling(structure.MinDistance + Math.Max(gridComp.LocalAABB.Height, gridComp.LocalAABB.Width));

        Vector2i mapPos = Vector2i.Zero;
        var result = false;
        for (int n = 0; n < tries; n++)
        {
            mapPos = (Vector2i) Rand.NextVector2Box(
                startPos.X, 
                startPos.Y, 
                startPos.X + maxOffset, 
                startPos.Y + maxOffset).Rounded();
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
            Logger.Info($"Debris generation, RandomPosSpawn: Spawned grid {grid.ToString()} successfully");
            gridForm.Coordinates = new EntityCoordinates(gridForm.Coordinates.EntityId, mapPos);
            return grid;
        }
        
        Logger.Error($"Debris generation, RandomPosSpawn: Failed to find spawn position, deleting grid {grid.ToString()}");
        EntityManager.DeleteEntity(grid);
        return EntityUid.Invalid;
    }

    //Set's up collision grid
    private void SetupGrid(Vector2 startPos, int maxDebrisOffset)
    {
        for (int y = 0; y < maxDebrisOffset; y += spawnSectorSize)
        {
            for (int x = 0; x < maxDebrisOffset; x += spawnSectorSize)
            {
                Vector2 sectorPos = new Vector2(startPos.X + x, startPos.Y + y);
                spawnSectors[sectorPos] = new HashSet<SectorRange>
                {
                    new SectorRange(sectorPos.Y, sectorPos.Y + spawnSectorSize, 
                        new List<(float, float)>{(sectorPos.X, sectorPos.X + spawnSectorSize)})
                };
                spawnSectorVolumes[sectorPos] = 0;
            }
        }
    }

    // Randomly picks structure from structure list, accounting for their weight
    // todo: maybe it's worth to PR this to RT (weighted random selection)
    private StructurePrototype? PickStructure(List<StructurePrototype> structures)
    {
        float totalWeight = structures.Select(s => s.SpawnWeight).Sum();
        float randFloat = Rand.NextFloat(0, totalWeight);

        StructurePrototype? picked = null;
        foreach(var structProt in structures)
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
    
    //Generates spawn position in random sector of the grid
    private Vector2? GenerateSpawnPosition(Box2 bounds)
    {
        var volume = bounds.Height * bounds.Width;
        var shuffledSectors = spawnSectors.Keys.ToList();
        Rand.Shuffle(shuffledSectors);
        
        foreach(var randomSector in shuffledSectors)
        {
            if (spawnSectorSize * spawnSectorSize - spawnSectorVolumes[randomSector] < volume)
                continue;
            var result = TryPlaceInSector(randomSector, bounds, out var spawnPos);
            if (result)
                return spawnPos;
        }
        
        return null;
    }
    
    /// <summary>
    /// Tries to find spot with enough space to fit given bounding box
    /// </summary>
    /// <param name="sectorPos">position of sector for search</param>
    /// <param name="bounds">bounding box</param>
    /// <param name="resultPos">resulting position</param>
    /// <returns></returns>
    private bool TryPlaceInSector(Vector2 sectorPos, Box2 bounds, out Vector2 resultPos)
    {
        bool result = false;
        resultPos = Vector2.NaN;

        HashSet<SectorRange> ranges = CutRanges(spawnSectors[sectorPos], sectorPos.X + spawnSectorSize - bounds.Width, sectorPos.Y + spawnSectorSize - bounds.Height);
        foreach (SectorRange range in ranges)
        {
            foreach ((float start, float end) in range.XRanges)
            {
                if (end - start >= bounds.Width)
                {
                    if (range.Top - range.Bottom >= bounds.Height)
                    {
                        result = true;
                        resultPos = new Vector2(Rand.NextFloat(start, end), Rand.NextFloat(range.Bottom, range.Top));
                        break;
                    }
                    SectorRange combinedRange = CombineRangesVertically(ranges, start, end, range.Bottom, bounds.Width);
                    if (combinedRange.Top - combinedRange.Bottom >= bounds.Height) 
                    {
                        result = true;
                        (float startc, float endc) = combinedRange.XRanges.First();
                        resultPos = new Vector2(Rand.NextFloat(startc, endc), Rand.NextFloat(range.Bottom, range.Top));
                        break;
                    }
                }
            }

            if (result)
                break;
        }

        if (result)
        {
            spawnSectors[sectorPos] = AddRange(spawnSectors[sectorPos], 
                CreateRange(Box2.FromDimensions(sectorPos, new Vector2(spawnSectorSize, spawnSectorSize)), 
                    Box2.FromDimensions(resultPos, new Vector2(bounds.Width, bounds.Height))));
            spawnSectorVolumes[sectorPos] += bounds.Height * bounds.Width;
        }
        return result;
    }
    
    private SectorRange CreateRange(Box2 parent, Box2 child)
    {
        List<(float, float)> xr = new() {(parent.Left, child.Left), (child.Right, parent.Right)};
        return new SectorRange(child.Bottom, child.Top, xr);
    }

    private HashSet<SectorRange> CutRanges(HashSet<SectorRange> ranges, float maxX, float maxY)
    {
        HashSet<SectorRange> rangesNew = new();
        foreach (SectorRange range in ranges)
        {
            SectorRange rangeN = range;
            rangeN.XRanges.Clear();
            
            if (range.Bottom > maxY)
                continue;
            if (range.Top > maxY)
                rangeN.Top = maxY;

            foreach ((float start, float end) in range.XRanges)
            {
                if (start > maxX)
                    continue;
                rangeN.XRanges.Add((start, end > maxX ? maxX : end));
            }

            rangesNew.Add(rangeN);
        }

        return rangesNew;
    }

    //Adds range to the set of ranges, combining it with existing ranges if needed
    private HashSet<SectorRange> AddRange(HashSet<SectorRange> ranges, SectorRange range)
    {
        HashSet<SectorRange> rangesNew = new();
        foreach (SectorRange rangeOther in ranges)
        {
            if (rangeOther.Bottom < range.Top && rangeOther.Top > range.Bottom) //height overlap
            {
                if (XRangesOverlap(range.XRanges, rangeOther.XRanges))
                    Logger.Error("Debris generation system, AddRange: Got both height & x-range overlap (COLLISION). Fix it pls.");

                if (Math.Abs(rangeOther.Bottom - range.Bottom) <= 0.1 && Math.Abs(rangeOther.Top - range.Top) <= 0.1)
                {
                    range.XRanges.Concat(rangeOther.XRanges);
                    rangesNew.Add(new SectorRange(range.Bottom, range.Top, range.XRanges));
                }
                
                (SectorRange rangeHigh, SectorRange rangeLow) = range.Top > rangeOther.Top ? (range, rangeOther) : (rangeOther, range);

                rangesNew.Add(new SectorRange(rangeLow.Top, rangeHigh.Top, rangeHigh.XRanges));
                rangesNew.Add(new SectorRange(rangeHigh.Bottom, rangeLow.Top, rangeHigh.XRanges.Concat(rangeLow.XRanges).ToList()));
                rangesNew.Add(new SectorRange(rangeLow.Bottom, rangeHigh.Bottom, rangeLow.XRanges));
            }
        }

        return rangesNew;
    }

    //Combines all ranges lying between endX & start X, and above/below height into a single range (with single X range)
    //with width above minWidth and combined height of included ranges
    private SectorRange CombineRangesVertically(HashSet<SectorRange> ranges, float start, float end, float height, float minWidth)
    {
        (float, float) GetFreeRange(SectorRange range)
        {
            foreach ((float startn, float endn) in range.XRanges)
            {
                if (startn < end && endn > start)
                {
                    return (startn, endn);
                }
            }

            return (0, 0);
        }

        float bottom, top, startn, endn;
        bottom = top = startn = endn = 0;

        List<SectorRange> sorted = ranges.Where(r => r.Top < height).OrderBy(r => r.Top).ToList();
        foreach (SectorRange range in sorted)
        {
            (float startf, float endf) = GetFreeRange(range);
            if (endf - startf < minWidth)
                break;
            bottom = range.Bottom;
            startn = startf > startn ? startf : startn;
            endn = endf < endn ? endf : endn;
        }
        
        sorted = ranges.Where(r => r.Bottom > height).OrderByDescending(r => r.Top).ToList();
        foreach (SectorRange range in sorted)
        {
            (float startf, float endf) = GetFreeRange(range);
            if (endf - startf < minWidth)
                break;
            top = range.Top;
            start = startf > start ? startf : start;
            endn = endf < endn ? endf : endn;
        }

        return new SectorRange(bottom, top, new List<(float, float)>{(startn, endn)});
    }

    //Returns true if at least one x-range overlaps another
    private bool XRangesOverlap(List<(float, float)> ranges1, List<(float, float)> ranges2)
    {
        foreach ((float start1, float end1) in ranges1)
        {
            foreach ((float start2, float end2) in ranges2)
            {
                if (start1 < end2 && end1 > start2)
                    return true;
            }
        }

        return false;
    }

    //SectorRange represents single 'line' of sector space. It contains info about it's height (Bottom, Top) & free spaces on that height level (XRanges)
    private struct SectorRange
    {
        public float Bottom, Top = 0;
        public List<(float, float)> XRanges;

        public SectorRange(float bottom, float top, List<(float, float)> xRanges)
        {
            Bottom = bottom;
            Top = top;
            XRanges = xRanges;
        }
    }
}

/// <summary>
/// Generator is a base class for generating debris
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract class Generator
{
    public abstract EntityUid Generate(DebrisGenerationSystem sys, MapId targetMap);
}

/// <summary>
/// Processor is a base class for post-processing debris
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract class Processor
{
    public abstract void Process(DebrisGenerationSystem sys, MapId targetMap, EntityUid gridUid, bool isGlobal);
}
