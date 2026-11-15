using System;
using System.Collections.Generic;
using System.Reflection;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops;
using Model.Ops.Data;
using UI.Builder;
using static Game.MapEditor.MapEditorCreateMenu;
using static Game.MapEditor.DtoInspectorPanel;
using static PassengerPlus.EditorHelpers;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(MapEditorWindow))]
    internal static class MapEditorWindowPatches
    {
        [HarmonyPatch(nameof(MapEditorWindow.Create))]
        [HarmonyPrefix]
        internal static void CreateWrapper(MapEditor mapEditor, Item item)
        {
            if (item.Kind == EditorConstants.Timetable.ItemKind)
            {
                CreateTimetable(mapEditor);
            }
        }

        [HarmonyPatch(nameof(MapEditorWindow.BuildInspectorColumn))]
        [HarmonyPrefix]
        internal static bool BuildTimetableInspectorColumn(UIPanelBuilder builder, MapEditor mapEditor, MapEditorWindow __instance)
        {
            if (string.IsNullOrEmpty(__instance._outlineSelection.Value)) return true;

            if (__instance._outlineSelection.Value == EditorConstants.Timetable.OutlineId)
            {
                MapEditorInspectorContext context = __instance.BuildInspectorContext(mapEditor);
                BuildTimetablePanel(builder, context);
                return false;
            }
            if (__instance._outlineSelection.Value.StartsWith(EditorConstants.TimetableBranch.OutlineId))
            {
                if (!TryGetTimetableBranch(mapEditor, __instance._outlineSelection.Value, out var branch))
                {
                    branch = null;
                }
                MapEditorInspectorContext context = __instance.BuildInspectorContext(mapEditor);
                BuildTimetableBranchPanel(builder, context, branch);
                return false;
            }
            return true;
        }

        internal static void BuildTimetablePanel(UIPanelBuilder builder, MapEditorInspectorContext context)
        {
            var mapEditor = context.MapEditor;
            var timetable = mapEditor.OpsEditor.Ops.timetable;
            builder.AddLabel("<style=H3>Timetable</style>");

            int branchCount = (timetable?.branches is null) ? 0 : timetable.branches.Count;
            builder.AddField("Branches", branchCount.ToString());
            builder.HStack(uiPanelBuilder =>
            {
                uiPanelBuilder.AddButtonCompact("Create Timetable Branch", delegate
                {
                    CreateTimetableBranch(context.MapEditor);
                    context.Rebuild();
                });
            });
        }

        internal static void BuildTimetableBranchPanel(UIPanelBuilder builder, MapEditorInspectorContext context, TimetableBranchDTO branch)
        {
            var mapEditor = context.MapEditor;
            builder.AddLabel("<style=H3>Timetable Branch");
            if (branch is null)
            {
                builder.AddLabel("Invalid timetable branch");
                return;
            }

            OpsInspectorPanel.BuildFields(builder, mapEditor, branch, context.Rebuild, context.RefreshOutline, null,
            (fieldBuilder, dto, field) =>
            {
                if (dto is TimetableStationDTO station)
                {
                    BuildTimetableStationPanel(fieldBuilder, context, mapEditor.OpsEditor.SetProperty, station, field);
                    return true;
                }
                return false;
            });
        }

        private static class TimetableStationProps
        {
            public const string StopId = nameof(TimetableStationDTO.passengerStopId);
            public const string Code = nameof(TimetableStationDTO.code);
            public const string Name = nameof(TimetableStationDTO.name);
            public const string MapFeatureId = nameof(TimetableStationDTO.mapFeatureId);
            public const string TimeToNext = nameof(TimetableStationDTO.traverseTimeToNext);

            private static PropertyInfo For(string name) => AccessTools.Property(typeof(TimetableStationDTO), name);

            public static bool Matches(DtoField field, PropertyInfo property)
            {
                return (field.Property.DeclaringType == typeof(TimetableStationProps)) && (field.Property.Name == property.Name);
            }
        }

        internal static void BuildTimetableStationPanel(UIPanelBuilder builder, MapEditorInspectorContext context, DtoSetProperty mutator, TimetableStationDTO station, DtoField field)
        {
            var metadata = MapObjectInspectorMetadata.Instance;
            switch (field.Property.Name)
            {
                case TimetableStationProps.StopId:
                    var presentation = Presentation(context.MapEditor, mutator, station, field, context.Rebuild, context.RefreshOutline);
                    var stops = GetAllPassengerStopIds(context.MapEditor);
                    BuildCustomDropdown(builder, context.MapEditor, mutator, station, field, context.Rebuild, context.RefreshOutline, presentation, stops, OnTimetableStationStopChanged);
                    break;

                // read-only labels for auto-set properties
                case TimetableStationProps.Code:
                    builder.AddLabel($"{field.Label}: {station.code}");
                    break;

                case TimetableStationProps.Name:
                    builder.AddLabel($"{field.Label}: {station.name}");
                    break;

                case TimetableStationProps.MapFeatureId:
                case TimetableStationProps.TimeToNext:
                    BuildField(builder, context.MapEditor, mutator, station, field, metadata, context.Rebuild, context.RefreshOutline, null);
                    break;
            }
        }

        private static DtoField GetDtoField(PropertyInfo property, IDtoInspectorMetadata metadata)
        {
            Type type = property.DeclaringType;
            string name = property.Name;

            return new DtoField(
                property,
                metadata.LabelOverrideFor(type, name) ?? DtoInspectorModel.Nicify(name),
                DtoInspectorModel.Classify(type, property, metadata),
                metadata.TooltipFor(type, name),
                metadata.RefreshScopeFor(type, name),
                metadata.ItemNounFor(type, name)
            );
        }

        private static void OnTimetableStationStopChanged(MapEditor mapEditor, object dto, string newValue)
        {
            var station = (TimetableStationDTO)dto;
            var newStop = GetStopById(mapEditor, newValue);

            station.code = newStop.timetableCode;
            station.name = newStop.name;
        }


        //===================================================================================================
        #region Ops Management

        private static void CreateTimetable(MapEditor mapEditor)
        {
            if (mapEditor.OpsEditor.Ops.timetable != null) return;

            var mapDTO = new TimetableMapDTO();
            mapEditor.PerformUndoable("Create Timetable", () =>
            {
                OpsAddTimetable(mapEditor.OpsEditor, mapDTO);
            });
            mapEditor.NotifyItemCreated();
        }

        private static void OpsAddTimetable(OpsEditor opsEditor, TimetableMapDTO mapDTO)
        {
            opsEditor.Ops.timetable = mapDTO;
            opsEditor.UndoManager.RegisterUndo(() =>
            {
                opsEditor.Ops.timetable = null;
            }, "Add Timetable");
            opsEditor.NotifyChange();
        }

        private static void CreateTimetableBranch(MapEditor mapEditor)
        {
            if (mapEditor.OpsEditor.Ops.timetable is null) return;

            var branchDTO = new TimetableBranchDTO();
            mapEditor.PerformUndoable("Create Timetable Branch", () =>
            {
                OpsAddTimetableBranch(mapEditor.OpsEditor, branchDTO);
            });
        }

        private static void OpsAddTimetableBranch(OpsEditor opsEditor, TimetableBranchDTO branchDTO)
        {
            var timetable = opsEditor.Ops.timetable;
            if (timetable.branches is null) timetable.branches = new List<TimetableBranchDTO>();

            timetable.branches.Add(branchDTO);
            opsEditor.UndoManager.RegisterUndo(() =>
            {
                timetable.branches?.Remove(branchDTO);
            }, "Add Timetable Branch");
            opsEditor.NotifyChange();
        }

        #endregion
    }
}