using System;
using Game.MapEditor;
using HarmonyLib;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(MapObjectInspectorMetadata))]
    internal static class DtoInspectorMetadataPatches
    {
        [HarmonyPatch(nameof(MapObjectInspectorMetadata.RefreshScopeFor))]
        [HarmonyPrefix]
        internal static bool RefreshScopeForWrapper(Type declaringType, string propertyName, ref DtoRefreshScope __result)
        {
            if (declaringType == typeof(StationAgentDTO) && propertyName == nameof(StationAgentDTO.AreaId))
            {
                __result = DtoRefreshScope.Outline | DtoRefreshScope.Inspector;
                return false;
            }
            return true;
        }
    }
}