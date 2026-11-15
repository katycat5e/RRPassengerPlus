using System;
using System.Collections.Generic;
using Game.MapEditor;
using Model.Ops.Data;
using UI.Builder;
using UnityEngine;
using static Game.MapEditor.DtoInspectorPanel;

namespace PassengerPlus
{
    internal static class EditorHelpers
    {
        public static bool TryGetTimetableBranch(MapEditor mapEditor, string identifier, out TimetableBranchDTO branch)
        {
            branch = null;
            var branches = mapEditor.OpsEditor.Ops.timetable?.branches;

            if (branches is null || branches.Count == 0) return false;

            if (int.TryParse(identifier.Split('/')[1], out int branchNum) && (branchNum - 1 < branches.Count))
            {
                branch = branches[branchNum - 1];
                return true;
            }

            return false;
        }

        public static DropdownOptions GetAllPassengerStopIds(MapEditor mapEditor, string areaId = null)
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

        public static PassengerStopDTO GetStopById(MapEditor mapEditor, string stopId)
        {
            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                foreach (var industry in area.Value.industries)
                {
                    foreach (var component in industry.Value.components)
                    {
                        if ((component.Key == stopId) && (component.Value is PassengerStopDTO stop))
                        {
                            return stop;
                        }
                    }
                }
            }
            return null;
        }

        public delegate void StringChangedCallback(MapEditor mapEditor, object dto, string newValue);

        public static void BuildCustomDropdown(UIPanelBuilder builder, MapEditor mapEditor, DtoSetProperty mutator,
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

    internal readonly struct DropdownOptions
    {
        public readonly List<string> Names;
        public readonly List<string> Values;
        public DropdownOptions(List<string> names, List<string> values)
        {
            Names = names;
            Values = values;
        }

        public void Add(string name, string value)
        {
            Names.Add(name);
            Values.Add(value);
        }

        public static DropdownOptions Create() =>
            new DropdownOptions(new List<string>(), new List<string>());
    }
}