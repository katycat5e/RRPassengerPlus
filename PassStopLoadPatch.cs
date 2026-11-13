using System.Linq;
using HarmonyLib;
using Model.Ops;
using Serilog;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(OpsController))]
    internal static class PassStopLoadPatch
    {
        [HarmonyPatch(nameof(OpsController.RebuildCollections))]
        [HarmonyPostfix]
        public static void PrintNeighbors()
        {
            foreach (var stop in PassengerStop.FindAll())
            {
                if (stop.neighbors is null || stop.neighbors.Length == 0)
                {
                    Log.Information($"[PP] Passenger stop {stop.name} has no neighbors");
                }
                else
                {
                    string neighbors = string.Join(", ", stop.neighbors.Select(s => s.name));
                    Log.Information($"[PP] Passenger stop {stop.name} neighbors: [{neighbors}]");
                }
            }
        }
    }
}