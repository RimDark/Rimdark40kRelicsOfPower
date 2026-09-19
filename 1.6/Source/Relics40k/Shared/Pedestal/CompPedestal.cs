using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Relics40k;

/// <summary>
/// Holds a single item unspawned, draws it on top of the pedestal at a tunable offset, and offers a
/// take job. Whatever action the stocker installed runs whenever the item leaves, however it leaves.
/// </summary>
public class CompPedestal : CompThingContainer, IThingHolderEvents<Thing>
{
    private const float DevNudgeStep = 0.05f;

    public PedestalAction action;

    private Pawn takingPawn;

    private Map removalMap;

    private Graphic scaledGraphic;

    private ThingDef scaledGraphicFor;

    private float scaledGraphicScale;

    private Vector3? devOffset;

    private float? devScale;

    public new CompProperties_Pedestal Props => (CompProperties_Pedestal)props;

    public Thing HeldItem => ContainedThing;

    private Vector3 HeldItemOffset => devOffset ?? Props.heldItemOffset;

    private float HeldItemScale => devScale ?? Props.heldItemScale;

    /// <summary>Puts an item on the pedestal, despawning it first if it is on the map. False if occupied.</summary>
    public bool TryPlace(Thing item)
    {
        if (item == null || !Empty)
        {
            return false;
        }

        if (item.Spawned)
        {
            item.DeSpawn();
        }

        return innerContainer.TryAdd(item);
    }

    public AcceptanceReport CanTake(Pawn pawn)
    {
        if (Empty)
        {
            return "Relics.Pedestal.Empty".Translate();
        }

        if (action != null)
        {
            var report = action.CanTake(this, pawn);

            if (!report.Accepted)
            {
                return report;
            }
        }

        return true;
    }

    /// <summary>Hands the item to the taker's inventory, dropping it beside them if that fails.</summary>
    public void Take(Pawn taker)
    {
        var item = HeldItem;

        if (item == null || taker == null)
        {
            return;
        }

        takingPawn = taker;

        try
        {
            if (!innerContainer.TryTransferToContainer(item, taker.inventory.innerContainer, false))
            {
                innerContainer.TryDrop(item, taker.Position, parent.Map, ThingPlaceMode.Near, out _);
            }
        }
        finally
        {
            takingPawn = null;
        }
    }

    public void Notify_ItemAdded(Thing item)
    {
    }

    public void Notify_ItemRemoved(Thing item)
    {
        action?.Notify_Taken(this, item, takingPawn, parent.MapHeld ?? removalMap);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        removalMap = map;
        base.PostDeSpawn(map, mode);
        removalMap = null;
    }

    public override void PostDraw()
    {
        var item = HeldItem;

        if (item == null || !Props.drawContainedThing)
        {
            return;
        }

        var offset = HeldItemOffset;
        var loc = parent.DrawPos + new Vector3(offset.x, 0f, offset.z);
        loc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        GraphicFor(item).Draw(loc, Rot4.North, item);
    }

    private Graphic GraphicFor(Thing item)
    {
        var scale = HeldItemScale;

        if (Mathf.Approximately(scale, 1f))
        {
            return item.Graphic;
        }

        if (scaledGraphic == null || scaledGraphicFor != item.def || !Mathf.Approximately(scaledGraphicScale, scale))
        {
            scaledGraphicFor = item.def;
            scaledGraphicScale = scale;
            scaledGraphic = item.Graphic.GetCopy(item.Graphic.drawSize * scale, null);
        }

        return scaledGraphic;
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        var item = HeldItem;

        if (item == null)
        {
            yield break;
        }

        var label = action?.TakeLabel(this) ?? (string)"Relics.Pedestal.Take".Translate(item.LabelNoCount);
        var report = CanTake(selPawn);

        if (!report.Accepted)
        {
            yield return new FloatMenuOption(label + " (" + report.Reason + ")", null);
            yield break;
        }

        if (!selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
        {
            yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }

        if (!selPawn.CanReserve(parent))
        {
            yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
            yield break;
        }

        yield return new FloatMenuOption(label, delegate
        {
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(PedestalDefOf.BEWH_TakeFromPedestal, parent, item), JobTag.Misc);
        });
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        if (!Prefs.DevMode || Empty)
        {
            yield break;
        }

        yield return DevNudge("DEV: item left", new Vector3(-DevNudgeStep, 0f, 0f));
        yield return DevNudge("DEV: item right", new Vector3(DevNudgeStep, 0f, 0f));
        yield return DevNudge("DEV: item up", new Vector3(0f, 0f, DevNudgeStep));
        yield return DevNudge("DEV: item down", new Vector3(0f, 0f, -DevNudgeStep));
        yield return DevScale("DEV: item bigger", DevNudgeStep);
        yield return DevScale("DEV: item smaller", -DevNudgeStep);
        yield return new Command_Action
        {
            defaultLabel = "DEV: reset item",
            action = delegate
            {
                devOffset = null;
                devScale = null;
                LogDevValues();
            }
        };
    }

    private Command_Action DevNudge(string label, Vector3 delta)
    {
        return new Command_Action
        {
            defaultLabel = label,
            action = delegate
            {
                devOffset = HeldItemOffset + delta;
                LogDevValues();
            }
        };
    }

    private Command_Action DevScale(string label, float delta)
    {
        return new Command_Action
        {
            defaultLabel = label,
            action = delegate
            {
                devScale = Mathf.Max(0.05f, HeldItemScale + delta);
                LogDevValues();
            }
        };
    }

    private void LogDevValues()
    {
        var offset = HeldItemOffset;
        Log.Message("[Relics of Power] " + parent.def.defName + ": <heldItemOffset>(" + offset.x.ToString("0.00") + ", 0, "
            + offset.z.ToString("0.00") + ")</heldItemOffset> <heldItemScale>" + HeldItemScale.ToString("0.00") + "</heldItemScale>");
    }

    public override string CompInspectStringExtra()
    {
        var item = HeldItem;

        return item == null
            ? "Relics.Pedestal.InspectEmpty".Translate()
            : "Relics.Pedestal.InspectHolding".Translate(item.LabelCap);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref action, "action");
    }
}
