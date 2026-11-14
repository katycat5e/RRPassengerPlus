using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops.Data;
using UI.Builder;
using UnityEngine;
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
            bool isStationAgent = dto is StationAgentDTO;

            foreach (DtoField item in DtoInspectorModel.EnumerateFields(dto, metadata))
            {
                if (isStationAgent && (item.Property.Name == nameof(StationAgentDTO.AreaId)))
                {
                    var presentation = Presentation(mapEditor, mutator, dto, item, rebuild, refreshOutline);
                    var areas = GetAllAreaIds(mapEditor);
                    BuildCustomDropdown(builder, mapEditor, mutator, dto, item, rebuild, refreshOutline, presentation, areas, OnAreaIdChanged);
                }
                else if (isStationAgent && (item.Property.Name == nameof(StationAgentDTO.PassengerStopId)))
                {
                    var presentation = Presentation(mapEditor, mutator, dto, item, rebuild, refreshOutline);
                    string currentArea = GetCurrentAreaId(dto);
                    var stops = GetAllPassengerStopIds(mapEditor, currentArea);
                    BuildCustomDropdown(builder, mapEditor, mutator, dto, item, rebuild, refreshOutline, presentation, stops);
                }
                else
                {
                    BuildField(builder, mapEditor, mutator, dto, item, metadata, rebuild, refreshOutline, customRenderer);
                }
            }

            return false;
        }

        private static DropdownOptions GetAllAreaIds(MapEditor mapEditor)
        {
            var names = new List<string>();
            var values = new List<string>();

            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                values.Add(area.Key);
                names.Add(string.IsNullOrEmpty(area.Value.name) ? area.Key : area.Value.name);
            }

            return new DropdownOptions(names, values);
        }

        private static readonly PropertyInfo AgentAreaIdProperty = AccessTools.Property(typeof(StationAgentDTO), nameof(StationAgentDTO.AreaId));

        private static string GetCurrentAreaId(object dto)
        {
            return AgentAreaIdProperty.GetValue(dto) as string;
        }

        private delegate void StringChangedCallback(MapEditor mapEditor, object dto, string newValue);

        private static void OnAreaIdChanged(MapEditor mapEditor, object dto, string newAreaId)
        {
            if (dto is StationAgentDTO agentDTO)
            {
                var stopOptions = GetAllPassengerStopIds(mapEditor, newAreaId);
                if (!stopOptions.Values.Contains(agentDTO.PassengerStopId))
                {
                    agentDTO.PassengerStopId = stopOptions.Values.FirstOrDefault();
                }
            }
        }

        private readonly struct DropdownOptions
        {
            public readonly List<string> Names;
            public readonly List<string> Values;
            public DropdownOptions(List<string> names, List<string> values)
            {
                Names = names;
                Values = values;
            }
        }

        private static DropdownOptions GetAllPassengerStopIds(MapEditor mapEditor, string areaId)
        {
            var names = new List<string>();
            var values = new List<string>();
            bool hasAreaFilter = !string.IsNullOrEmpty(areaId);

            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                if (hasAreaFilter && area.Key != areaId) continue;

                foreach (var industry in area.Value.industries)
                {
                    foreach (var component in industry.Value.components)
                    {
                        if (component.Value is PassengerStopDTO stop)
                        {
                            values.Add(component.Key);
                            names.Add(string.IsNullOrEmpty(stop.name) ? component.Key : stop.name);
                        }
                    }
                }
            }
            return new DropdownOptions(names, values);
        }

        private static void BuildCustomDropdown(UIPanelBuilder builder, MapEditor mapEditor, DtoSetProperty mutator,
            object dto, DtoField field, Action rebuild, Action refreshOutline, FieldPresentation presentation,
            DropdownOptions options, StringChangedCallback extraChangeCallback = null)
        {
            string currentValue = field.Property.GetValue(dto)?.ToString();
            int currentIndex = Mathf.Max(0, options.Values.IndexOf(currentValue));
            AddStackedField(builder, presentation.Label, presentation.Tooltip, row =>
            {
                row.AddDropdown(options.Names, currentIndex, newIndex =>
                {
                    string newValue = options.Values[newIndex];
                    Commit(mapEditor, mutator, dto, field, newValue, rebuild, refreshOutline);
                    extraChangeCallback?.Invoke(mapEditor, dto, newValue);
                });
            }, presentation.Accessory);
        }
    }
}