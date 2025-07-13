using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PawnStorages;

public static class Utility
{
    // Shared state
    public static List<ThingDef> producerAnimals;
    public static List<ThingDef> animals;

    public static bool IsWall(this ThingDef def)
    {
        if (def.category != ThingCategory.Building) return false;
        if (!def.graphicData?.Linked ?? true) return false;
        return (def.graphicData.linkFlags & LinkFlags.Wall) != LinkFlags.None &&
               def.graphicData.linkType == LinkDrawerType.CornerFiller &&
               def.fillPercent >= 1f &&
               def.blockWind &&
               def.coversFloor &&
               def.castEdgeShadows &&
               def.holdsRoof &&
               def.blockLight;
    }

    public static Mesh SetUVs(this Mesh mesh, bool flipped)
    {
        Vector2[] array2 = new Vector2[4];
        if (!flipped)
        {
            array2[0] = new Vector2(0f, 0f);
            array2[1] = new Vector2(0f, 1f);
            array2[2] = new Vector2(1f, 1f);
            array2[3] = new Vector2(1f, 0f);
        }
        else
        {
            array2[0] = new Vector2(1f, 0f);
            array2[1] = new Vector2(1f, 1f);
            array2[2] = new Vector2(0f, 1f);
            array2[3] = new Vector2(0f, 0f);
        }

        mesh.uv = array2;
        return mesh;
    }

    public static Texture2D GetGreyscale(this RenderTexture source)
    {
        Texture2D texture = MakeReadableTextureInstance(source);
        Color[] colors = texture.GetPixels();

        for (int i = 0; i < colors.Length; i++)
        {
            Color c = colors[i];
            float gray = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            colors[i] = new Color(gray, gray, gray, c.a);
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    public static Texture2D MakeReadableTextureInstance(this RenderTexture source)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        temporary.name = "MakeReadableTexture_Temp";
        Graphics.Blit(source, temporary);
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = temporary;
        Texture2D texture2D = new(source.width, source.height);
        texture2D.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(temporary);
        return texture2D;
    }

    public static void ReleasePawn(CompPawnStorage store, Pawn pawn, IntVec3 cell, Map map)
    {

        if (store.parent is Building bld && bld.def.hasInteractionCell)
        {
           cell = store.parent.Position + bld.def.interactionCellOffset;
        }

        if (!cell.Walkable(map))
            foreach (IntVec3 t in GenRadial.RadialPattern)
            {
                IntVec3 intVec = pawn.Position + t;
                if (!intVec.Walkable(map)) continue;
                cell = intVec;
                break;
            }

        GenSpawn.Spawn(pawn, cell, map);

        //Spawn the release effecter
        store.Props.releaseEffect?.Spawn(cell, map);

        if (store.Props.lightEffect) FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 0.5f);
        if (store.Props.transformEffect)
            FleckMaker.ThrowExplosionCell(cell, map, FleckDefOf.ExplosionFlash, Color.white);
        map.mapDrawer.MapMeshDirty(store.parent.Position, MapMeshFlagDefOf.Things);

        store.SetLabelDirty();
        store.ApplyNeedsForStoredPeriodFor(pawn);
        pawn.guest?.WaitInsteadOfEscapingFor(1250);
        store.Notify_ReleasedFromStorage(pawn);
        store.GetDirectlyHeldThings().Remove(pawn);
    }

    /**
     * Return if the storage requires a station
     * If it does, the out param station will have the selected station
     */
    public static bool CheckStation(CompPawnStorage store, Pawn releaser, out Thing station)
    {
        if (store.RequiresStation())
        {
            station = GenClosest.ClosestThingReachable(releaser.Position, releaser.Map,
                ThingRequest.ForDef(store.Props.storageStation), PathEndMode.InteractionCell,
                TraverseParms.For(releaser),
                9999f, x => releaser.CanReserve(x) && (x.TryGetComp<CompPowerTrader>()?.PowerOn ?? true));
            return true;
        }

        station = null;
        return false;
    }

    public static bool CanRelease(CompPawnStorage store, Pawn releaser) => !CheckStation(store, releaser, out Thing station) || station != null;

    public static Job ReleaseJob(CompPawnStorage store, Pawn releaser, Pawn toRelease)
    {
        if (!CheckStation(store, releaser, out Thing station))
        {
            return JobMaker.MakeJob(PS_DefOf.PS_Release, store.parent, null, toRelease);
        }

        if (station == null) return null;
        Job job = JobMaker.MakeJob(PS_DefOf.PS_Release, store.parent, station, toRelease);
        job.count = 1;
        return job;
    }

    public static bool CompPropertiesIsProducer(CompProperties c)
    {
        return typeof(CompEggLayer).IsAssignableFrom(c.compClass) || typeof(CompHasGatherableBodyResource).IsAssignableFrom(c.compClass);
    }

    public static Lazy<List<PawnKindDef>> AllAnimalKinds = new(() => DefDatabase<PawnKindDef>.AllDefs.Where(d=> d.race != null && d.race.race.Animal && d.race.GetStatValueAbstract(StatDefOf.Wildness) < 1 && !d.race.race.Dryad && !d.race.IsCorpse).ToList());

    public static bool ValidateThingDef(ThingDef td, bool IsProducer)
    {
        bool raceIsAnimal = td.race is { Animal: true, Dryad: false } && td.GetStatValueAbstract(StatDefOf.Wildness) < 1.0 && !td.IsCorpse;
        bool categoryIsAnimal = td.category == ThingCategory.Pawn && td.thingCategories != null && td.thingCategories.Contains(ThingCategoryDefOf.Animals);
        return (categoryIsAnimal || raceIsAnimal) &&
               (!IsProducer || (td.comps?.Any(CompPropertiesIsProducer) ?? false));
    }

    public static List<ThingDef> Animals(bool IsProducer)
    {
        if (IsProducer)
        {
            return producerAnimals ??= DefDatabase<ThingDef>.AllDefs.Where(td => ValidateThingDef(td, true)).ToList();
        }

        return animals ??= AllAnimalKinds.Value.Select(k => k.race).ToList();
    }
}
