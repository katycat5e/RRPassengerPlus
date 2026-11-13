using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Model.Ops;
using UI.CarInspector;

namespace PassengerPlus
{
    [HarmonyPatch(typeof(CarInspector))]
    internal static class CarInspectorPatches
    {
        [HarmonyPatch(nameof(CarInspector.PopulatePassengerCarPanel))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspilePopulatePassengerCarPanel(IEnumerable<CodeInstruction> instructions)
        {
            var source = new TranspileHelper(instructions);

            // skip to orderedStops array definition
            foreach (var i in source.Until(OpCodes.Ldc_I4_S))
            {
                yield return i;
            }

            yield return CodeInstruction.Call(() => GetOrderedStopsArray());

            // skip section that builds the passenger stop list, up until the line that saves the list to the local var
            source.Until(OpCodes.Stfld).Skip();

            // reuse the remainder of the original method, but with our custom stop list
            foreach (var i in source.Remaining())
            {
                yield return i;
            }
        }
        
        private static List<PassengerStop> GetOrderedStopsArray()
        {
            return PassengerStop.FindAll()
                .Where(s => s.IsEnabledReachable)
                .OrderBy(s => s.name)
                .ToList();
        }
    }
}