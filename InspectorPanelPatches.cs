using System;
using System.Collections.Generic;
using Game.MapEditor;
using HarmonyLib;
using Model.Ops;
using Model.Ops.Data;
using UI.Builder;
using static Game.MapEditor.OpsInspectorPanel;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(OpsInspectorPanel))]
    internal static class OpsInspectorPanelPatches
    {
        [HarmonyPatch(nameof(BuildFields))]
        [HarmonyPrefix]
        internal static bool BuildFieldPrefix(UIPanelBuilder builder, MapEditor mapEditor, object dto, Action rebuild, Action refreshOutline, IndustryDTO owningIndustry, DtoInspectorPanel.CustomFieldRenderer extraRenderer)
        {
            DtoInspectorPanel.BuildFields(builder, mapEditor, mapEditor.OpsEditor.SetProperty, dto, OpsInspectorMetadata.Instance, rebuild, refreshOutline,
                (fieldBuilder, fieldDto, field) =>
                {
                    // Original field handlers
                    if (extraRenderer != null && extraRenderer(fieldBuilder, fieldDto, field)) return true;
                    if (field.Kind == DtoFieldKind.LoadReference)
                    {
                        BuildLoadReferenceField(fieldBuilder, mapEditor, fieldDto, field, rebuild, refreshOutline);
                        return true;
                    }
                    if (field.Kind == DtoFieldKind.TrackSpanReference && owningIndustry != null)
                    {
                        BuildSpanReferenceField(fieldBuilder, mapEditor, owningIndustry, fieldDto, field, rebuild, refreshOutline);
                        return true;
                    }

                    // Passenger field extensions
                    if (field.Kind == DtoFieldKind.StringArrayReadOnly && field.Property.Name == nameof(PassengerStopDTO.neighbors))
                    {
                        BuildPassengerNeighborsField(fieldBuilder, mapEditor, owningIndustry, dto, field, rebuild, refreshOutline);
                        return true;
                    }

                    return false;
                }
            );
            return false;
        }

        private static void BuildPassengerNeighborsField(UIPanelBuilder builder, MapEditor mapEditor, IndustryDTO owningIndustry, object dto, DtoField field, Action rebuild, Action refreshOutline)
        {
            var overrideInfo = DtoInspectorPanel.DescribeOverride(mapEditor, dto, field.Property, baseValue =>
            {
                DtoInspectorPanel.Commit(mapEditor, mapEditor.OpsEditor.SetProperty, dto, field, baseValue, rebuild, refreshOutline, $"Revert {field.Label}");
                rebuild();
            });

            // build section header and changed/not changed indicator
            if (overrideInfo.IsChanged)
            {
                builder.HStack(row =>
                {
                    row.AddLabel($"<style=SectionHeader>{field.Label}</style>{MapCompositionStyle.ChangedFieldMarker}");
                    row.Spacer();
                    overrideInfo.RevertAccessory(row);
                });
            }
            else
            {
                builder.AddLabel($"<style=SectionHeader>{field.Label}</style>");
            }

            string[] currentNeighbors = (field.Property.GetValue(dto) as string[]) ?? Array.Empty<string>();
            foreach (var potentialNeighbor in GetAllPassengerStops(mapEditor.OpsEditor))
            {
                string stopId = potentialNeighbor.Key;
                if (IsChildOfCurrentIndustry(owningIndustry, stopId)) continue;

                bool isChecked = Array.IndexOf(currentNeighbors, stopId) >= 0;

                // Create a radio button for each potential station
                builder.AddFieldToggle(stopId, () => isChecked, newValue =>
                {
                    string[] newNeighbors = newValue ?
                        TrackSpanReferences.AddToArray(currentNeighbors, stopId) :
                        TrackSpanReferences.RemoveFromArray(currentNeighbors, stopId);
                    if (newNeighbors != null)
                    {
                        OpsInspectorPanel.CommitSpanArray(mapEditor, dto, field, newNeighbors, rebuild, refreshOutline,
                            newValue ? "Add Neighbor" : "Remove Neighbor");
                    }
                });
            }
        }

        private static bool IsChildOfCurrentIndustry(IndustryDTO industry, string stopId)
        {
            return industry.components.ContainsKey(stopId);
        }

        private static IEnumerable<KeyValuePair<string, PassengerStopDTO>> GetAllPassengerStops(OpsEditor opsEditor)
        {
            foreach (var area in opsEditor.Ops.areas)
            {
                foreach (var industry in area.Value.industries)
                {
                    foreach (var component in industry.Value.components)
                    {
                        if (component.Value is PassengerStopDTO passStop)
                        {
                            yield return new KeyValuePair<string, PassengerStopDTO>(component.Key, passStop);
                        }
                    }
                }
            }
        }
    }
}