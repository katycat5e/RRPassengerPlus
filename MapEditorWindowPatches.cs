using System;
using System.Linq;
using System.Reflection;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops.Data;
using UI.Builder;
using static Game.MapEditor.DtoInspectorPanel;
using static Game.MapEditor.MapEditorCreateMenu;
using static PassengerPlus.EditorHelpers;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(MapEditorWindow))]
    internal static class MapEditorWindowPatches
    {
        static MapEditorWindowPatches()
        {
            OpsInspectorMetadata.InspectorScoped.Add((typeof(TimetableBranchDTO), nameof(TimetableBranchDTO.name)));
            OpsInspectorMetadata.OutlineScoped.Add((typeof(TimetableBranchDTO), nameof(TimetableBranchDTO.name)));
        }

        [HarmonyPatch(nameof(MapEditorWindow.Create))]
        [HarmonyPrefix]
        internal static void CreateWrapper(MapEditor mapEditor, Item item)
        {
            if (item.Kind == EditorConstants.Timetable.ItemKind)
            {
                TimetableConstruction.CreateTimetable(mapEditor);
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

        private class TimetablePanelOptions
        {
            public string StartId { get; set; }
            public string EndId { get; set; }

            public static readonly PropertyInfo StartIdProp = AccessTools.Property(typeof(TimetablePanelOptions), nameof(StartId));
            public static readonly DtoField StartIdField = new DtoField(StartIdProp, "Eastern Terminus", DtoFieldKind.Text, null, DtoRefreshScope.Inspector);

            public static readonly PropertyInfo EndIdProp = AccessTools.Property(typeof(TimetablePanelOptions), nameof(EndId));
            public static readonly DtoField EndIdField = new DtoField(EndIdProp, "Western Terminus", DtoFieldKind.Text, null, DtoRefreshScope.Inspector);
        }

        private static readonly TimetablePanelOptions _panelOptions = new TimetablePanelOptions();

        internal static void BuildTimetablePanel(UIPanelBuilder builder, MapEditorInspectorContext context)
        {
            var mapEditor = context.MapEditor;
            var timetable = mapEditor.OpsEditor.Ops.timetable;
            builder.AddLabel("<style=H3>Timetable</style>");

            int branchCount = (timetable?.branches is null) ? 0 : timetable.branches.Count;
            builder.AddField("Branches", branchCount.ToString());

            builder.HStack(uiPanelBuilder =>
            {
                uiPanelBuilder.AddButtonCompact("Create Empty Timetable Branch", delegate
                {
                    TimetableConstruction.CreateTimetableBranch(context.MapEditor);
                    context.Rebuild();
                });
            });

            var stopOptions = GetAllPassengerStopIds(mapEditor);
            var mutator = (DtoSetProperty)mapEditor.OpsEditor.SetProperty;

            if (string.IsNullOrEmpty(_panelOptions.StartId)) _panelOptions.StartId = stopOptions.Values.FirstOrDefault();
            if (string.IsNullOrEmpty(_panelOptions.EndId)) _panelOptions.EndId = stopOptions.Values.FirstOrDefault();

            BuildCustomDropdown(builder, mutator, _panelOptions, TimetablePanelOptions.StartIdField, context.Rebuild, context.RefreshOutline, stopOptions);
            BuildCustomDropdown(builder, mutator, _panelOptions, TimetablePanelOptions.EndIdField, context.Rebuild, context.RefreshOutline, stopOptions);

            builder.Spacer();
            builder.HStack(uiPanelBuilder =>
            {
                uiPanelBuilder.AddButtonCompact("Create Timetable Branch From Route", delegate
                {
                    TimetableConstruction.CreateTimetableBranch(context.MapEditor, _panelOptions.StartId, _panelOptions.EndId);
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
            station.name = GetShortStopName(newStop.name);
        }

        private static readonly string[] _toTrimFromStopName =
        {
            "station", "depot", "flag stop",
        };

        private static string GetShortStopName(string fullName)
        {
            foreach (string trimmable in _toTrimFromStopName)
            {
                int start;
                while ((start = fullName.IndexOf(trimmable, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    fullName = fullName.Remove(start, trimmable.Length);
                }
            }

            return fullName;
        }
    }
}