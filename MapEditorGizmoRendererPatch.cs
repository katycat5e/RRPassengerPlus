using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.MapEditor;
using HarmonyLib;
using Helpers;
using static Game.MapEditor.MapEditorGizmoRenderer;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(MapEditorGizmoRenderer))]
    internal static class MapEditorGizmoRendererPatch
    {
        [HarmonyPatch(nameof(MapEditorGizmoRenderer.DrawMapObjects))]
        [HarmonyPostfix]
        internal static void AfterDrawMapObjects(Vector3 cameraPos, MapEditorGizmoRenderer __instance)
        {
            if (__instance.MapObjectSource == null) return;

            foreach (var objectKVP in __instance.MapObjectSource.Objects)
            {
                if (!(objectKVP.Value is StationAgentDTO agentDTO)) continue;

                DrawAgentOutline(__instance, agentDTO);
            }
        }

        private static readonly float[] CUBE_OFFSETS = { -1, 1 };

        private static (Vector3, Vector3)[] _cubeEdges = null;
        private static (Vector3, Vector3)[] CubeEdges
        {
            get
            {
                if (_cubeEdges is null) _cubeEdges = GenerateCubeEdges().ToArray();
                return _cubeEdges;
            }
        }

        private static IEnumerable<(Vector3 start, Vector3 end)> GenerateCubeEdges()
        {
            var vertices = new Vector3[8];

            int i = 0;
            foreach (float x in CUBE_OFFSETS)
            {
                foreach (float y in CUBE_OFFSETS)
                {
                    foreach (float z in CUBE_OFFSETS)
                    {
                        vertices[i] = new Vector3(x, y, z);
                        i++;
                    }
                }
            }

            foreach (Vector3 start in vertices)
            {
                foreach (Vector3 end in vertices)
                {
                    // filter to only pairs that are a single edge apart
                    if (Vector3.SqrMagnitude(end - start) == 4f)
                    {
                        yield return (start, end);
                    }
                }
            }
        }

        private static void DrawAgentOutline(MapEditorGizmoRenderer renderer, StationAgentDTO dto)
        {
            Color outlineColor = SpawnNormalColor;
            if (renderer.IsSelectedOrPendingAdd(SelectableItem.FromMapObject(dto)))
            {
                outlineColor = SpawnSelectedColor;
            }

            foreach ((Vector3 start, Vector3 end) in CubeEdges)
            {
                Vector3 worldStart = dto.Position + dto.Rotation * Vector3.Scale(start, dto.Scale).GameToWorld();
                Vector3 worldEnd = dto.Position + dto.Rotation * Vector3.Scale(end, dto.Scale).GameToWorld();

                renderer.DrawLine(worldStart, worldEnd, outlineColor);
            }
        }
    }
}