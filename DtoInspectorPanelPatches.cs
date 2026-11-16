using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops.Data;
using UI.Builder;
using static Game.MapEditor.DtoInspectorPanel;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(DtoInspectorPanel))]
    internal static class DtoInspectorPanelPatches
    {
        [HarmonyPatch(nameof(BuildFields))]
        [HarmonyPrefix]
        internal static bool BuildFieldsReplacement(UIPanelBuilder builder, MapEditor mapEditor, DtoSetProperty mutator, object dto, IDtoInspectorMetadata metadata, Action rebuild, Action refreshOutline, CustomFieldRenderer customRenderer)
        {
            foreach (DtoField item in DtoInspectorModel.EnumerateFields(dto, metadata))
            {
                if (IsAnyProperty(_areaIdProperties, item.Property))
                {
                    var presentation = Presentation(mapEditor, mutator, dto, item, rebuild, refreshOutline);
                    var areas = GetAllAreaIds(mapEditor);
                    EditorHelpers.BuildCustomDropdown(builder, mapEditor, mutator, dto, item, rebuild, refreshOutline, presentation, areas, OnAreaIdChanged);
                }
                else if (IsAnyProperty(_stopIdProperties, item.Property))
                {
                    var presentation = Presentation(mapEditor, mutator, dto, item, rebuild, refreshOutline);
                    string currentArea = GetCurrentAreaId(dto);
                    var stops = EditorHelpers.GetAllPassengerStopIds(mapEditor, currentArea);
                    EditorHelpers.BuildCustomDropdown(builder, mapEditor, mutator, dto, item, rebuild, refreshOutline, presentation, stops);
                }
                else
                {
                    BuildField(builder, mapEditor, mutator, dto, item, metadata, rebuild, refreshOutline, customRenderer);
                }
            }

            return false;
        }

        private static readonly PropertyInfo[] _stopIdProperties =
        {
            AccessTools.Property(typeof(StationAgentDTO), nameof(StationAgentDTO.PassengerStopId)),
        };

        private static readonly PropertyInfo[] _areaIdProperties =
        {
            AccessTools.Property(typeof(StationAgentDTO), nameof(StationAgentDTO.AreaId)),
        };

        private static bool IsAnyProperty(IEnumerable<PropertyInfo> candidates, PropertyInfo property)
        {
            foreach (var candidate in candidates)
            {
                if ((candidate.DeclaringType == property.DeclaringType) && (candidate.Name == property.Name)) return true;
            }
            return false;
        }

        private static DropdownOptions GetAllStationCodes(MapEditor mapEditor)
        {
            var result = DropdownOptions.Create();

            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                foreach (var industry in area.Value.industries)
                {
                    foreach (var component in industry.Value.components)
                    {
                        if (!(component.Value is PassengerStopDTO stop) || string.IsNullOrWhiteSpace(stop.timetableCode)) continue;
                        result.Add(stop.timetableCode, stop.timetableCode);
                    }
                }
            }
            return result;
        }

        private static DropdownOptions GetAllAreaIds(MapEditor mapEditor)
        {
            var result = DropdownOptions.Create();

            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                result.Values.Add(area.Key);
                result.Names.Add(string.IsNullOrEmpty(area.Value.name) ? area.Key : area.Value.name);
            }

            return result;
        }

        private static string GetCurrentAreaId(object dto)
        {
            if (dto is StationAgentDTO agent) return agent.AreaId;
            return null;
        }

        private static void OnAreaIdChanged(MapEditor mapEditor, object dto, string newAreaId)
        {
            if (dto is StationAgentDTO agentDTO)
            {
                var stopOptions = EditorHelpers.GetAllPassengerStopIds(mapEditor, newAreaId);
                if (!stopOptions.Values.Contains(agentDTO.PassengerStopId))
                {
                    agentDTO.PassengerStopId = stopOptions.Values.FirstOrDefault();
                }
            }
        }
    }
}