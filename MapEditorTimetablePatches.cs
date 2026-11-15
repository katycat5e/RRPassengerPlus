using System.Collections.Generic;
using System.Linq;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops.Data;
using Track.Data;
using UI.Builder;
using static Game.MapEditor.MapEditorCreateMenu;
using static Game.MapEditor.MapEditorOutline;

namespace PassengerPlus
{
    [HarmonyPatch]
    internal static class VariousPatches
    {
        [HarmonyPatch(typeof(UIOutlineFlattener), nameof(UIOutlineFlattener.TryGetPath))]
        [HarmonyPostfix]
        internal static void TryGetPath(IReadOnlyList<UIOutlineItem> roots, string identifier, List<UIOutlineItem> path)
        {
            string fullRoots = string.Join(", ", roots.Select(i => i.Identifier));

            string fullPath;
            if (path is null) fullPath = string.Empty;
            else fullPath = string.Join(", ", path.Select(i => i.Identifier));

            PassengerPlusEntry.LogInfo($"[PP] ID: {identifier} Roots: [{fullRoots}], Path: {fullPath}");
        }
    }

    [HarmonyPatch(typeof(MapEditorCreateMenu))]
    internal static class MapEditorCreateMenuPatches
    {
        [HarmonyPatch(nameof(Build))]
        [HarmonyPostfix]
        internal static void AddTimeTableEntry(List<Entry> __result)
        {
            __result.Add(new Entry(new Item(EditorConstants.Timetable.ItemKind, "Timetable")));
        }
    }

    [HarmonyPatch(typeof(MapEditorOutline))]
    internal static class MapEditorOutlinePatches
    {
        [HarmonyPatch(nameof(MapEditorOutline.Build))]
        [HarmonyPostfix]
        internal static void AddTimetableOutlineItem(List<UIOutlineItem> __result, MapDTO mapData, string search, MapEditorCategories categories, TombstoneView tombstones)
        {
            if (categories.Includes(MapEditorCategories.Ops) && Matches(search, EditorConstants.Timetable.OutlineId, EditorConstants.Timetable.OutlineName, null))
            {
                if (mapData.ops.timetable != null)
                {
                    AddTimetableGroup(__result, mapData.ops.timetable, search, categories, tombstones);
                }
            }
        }

        private static void AddTimetableGroup(List<UIOutlineItem> items, TimetableMapDTO timetable, string search, MapEditorCategories categories, TombstoneView tombstones)
        {
            var timetableItem = new UIOutlineItem(EditorConstants.Timetable.OutlineId, EditorConstants.Timetable.OutlineName);

            if (timetable.branches != null)
            {
                for (int i = 0; i < timetable.branches.Count; i++)
                {
                    var branch = timetable.branches[i];
                    int branchNum = i + 1;
                    var branchItem = new UIOutlineItem(EditorConstants.TimetableIdentifier(branchNum), NameOrId(branch.name, branchNum.ToString()), branch);
                    timetableItem.AddChild(branchItem);
                }
            }
            
            items.Add(timetableItem);
        }

        // [HarmonyPatch(nameof(MapEditorOutline.VisitEntities))]
        // [HarmonyPrefix]
        // internal static bool VisitEntitiesWrapper(ref bool __result, MapDTO mapData, MapEditorOutline.EntityVisitor visit)
        // {
        //     if (mapData.ops.timetable != null)
        //     {
        //         if (visit(TimelineConstants.OutlinePrefix, TimelineConstants.OutlineId, null, null, TimelineConstants.OutlineId, mapData.ops.timetable))
        //         {
        //             __result = true;
        //             return false;
        //         }
        //     }
        //     return true;
        // }
    }

    [HarmonyPatch(typeof(OutlineSelectionSync))]
    internal static class OutlineSelectionSyncPatches
    {
        [HarmonyPatch(nameof(OutlineSelectionSync.ApplyOutlineRowSelection))]
        [HarmonyPrefix]
        internal static bool OutlineSelectTimetable(MapEditor mapEditor, string identifier)
        {
            var timetable = mapEditor.OpsEditor.Ops.timetable;

            if (identifier == EditorConstants.Timetable.OutlineId)
            {
                mapEditor.Selection.Select(new SelectableItem(EditorConstants.Timetable.SelectableKind, timetable));
                return false;
            }
            else if (identifier.StartsWith(EditorConstants.TimetableBranch.OutlineId))
            {
                if (EditorHelpers.TryGetTimetableBranch(mapEditor, identifier, out var branch))
                {
                    mapEditor.Selection.Select(new SelectableItem(EditorConstants.TimetableBranch.SelectableKind, branch));
                }
                else
                {
                    PassengerPlusEntry.LogWarning($"Invalid timetable branch id: {identifier}");
                }
            }
            return true;
        }

        [HarmonyPatch(nameof(OutlineSelectionSync.TryGetOpsOutlineIdentifier))]
        [HarmonyPostfix]
        internal static void TryGetOpsOutlineIdentifier(ref bool __result, MapEditor mapEditor, SelectableItem item, ref string identifier)
        {
            if (!__result)
            {
                if (item.ItemKind == EditorConstants.Timetable.SelectableKind)
                {
                    identifier = EditorConstants.Timetable.OutlineId;
                    __result = true;
                }
                else if ((item.ItemKind == EditorConstants.TimetableBranch.SelectableKind) && (mapEditor.OpsEditor.Ops.timetable?.branches != null))
                {
                    var branch = item.Item as TimetableBranchDTO;
                    int index = mapEditor.OpsEditor.Ops.timetable.branches.IndexOf(branch);
                    identifier = $"{EditorConstants.TimetableBranch.OutlineId}{index + 1}";
                    __result = true;
                }
            }
        }
    }
}