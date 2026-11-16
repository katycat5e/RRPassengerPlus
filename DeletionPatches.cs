using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops.Data;

namespace PassengerPlus
{
    [HarmonyPatch]
    internal static class DeletionPatches
    {
        [HarmonyPatch(typeof(DeleteSelectionPreview), nameof(DeleteSelectionPreview.For))]
        [HarmonyPostfix]
        internal static void AddTimetableToSelection(MapEditor mapEditor, DeleteSelectionPreview __result)
        {
            var selection = mapEditor.Selection;
            var tally = (IList<string>)__result.Tally;

            int timetableMapCount = selection.Items.Count(i => i.ItemKind == EditorConstants.Timetable.SelectableKind);
            if (timetableMapCount > 0)
            {
                tally.Add(timetableMapCount.Pluralize(EditorConstants.Timetable.OutlineName));
            }

            int timetableBranchCount = selection.Items.Count(i => i.ItemKind == EditorConstants.TimetableBranch.SelectableKind);
            if (timetableBranchCount > 0)
            {
                tally.Add(timetableBranchCount.Pluralize(EditorConstants.TimetableBranch.OutlineName));
            }
        }

        private static IEnumerable<SelectableItem> OfKind(IEnumerable<SelectableItem> items, SelectableItem.Kind kind) =>
            items.Where(i => i.ItemKind == kind);

        [HarmonyPatch(typeof(MapEditor), nameof(MapEditor.DeleteSelection))]
        [HarmonyPrefix]
        internal static void DeleteTimetableSelections(MapEditor __instance)
        {
            // string itemString = string.Join("\n", __instance._selection.Items.Select(i => $"{i.ItemKind}: {i.Item}"));
            // PassengerPlusEntry.LogInfo($"[PP] Delete requested:\n{itemString}");

            IEnumerable<SelectableItem> selected = OfKind(__instance._selection.Items, EditorConstants.Timetable.SelectableKind);
            if (selected.Any())
            {
                __instance._selection.Deselect(selected.First());
                var timetable = __instance.OpsEditor.Ops.timetable;
                __instance.OpsEditor.Ops.timetable = null;
                __instance.UndoManager.RegisterUndo(() =>
                {
                    __instance.OpsEditor.Ops.timetable = timetable;
                }, "Delete Timetable");
                return;
            }

            selected = OfKind(__instance._selection.Items, EditorConstants.TimetableBranch.SelectableKind);
            if (selected.Any())
            {
                var timetable = __instance.OpsEditor.Ops.timetable;
                __instance.PerformUndoable("Delete Timetable Branches", () =>
                {
                    foreach (var selectedBranch in selected.ToList())
                    {
                        __instance._selection.Toggle(selectedBranch);
                        if (timetable.branches.Remove((TimetableBranchDTO)selectedBranch.Item))
                        {
                            __instance.UndoManager.RegisterUndo(() =>
                            {
                                timetable.branches.Add((TimetableBranchDTO)selectedBranch.Item);
                            }, "Delete Timetable Branch");
                        }
                        else
                        {
                            PassengerPlusEntry.LogWarning($"[PP] DeleteSelection: selected timetable branch not found, skipping");
                        }
                    }
                });
            }
        }
    }
}