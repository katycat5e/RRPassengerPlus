using System.Reflection;
using HarmonyLib;
using Railroader.Modding;

namespace PassengerPlus
{
    public sealed class PassengerPlusEntry : IModEntryPoint
    {
        public static readonly string HarmonyId = "cc.foxden.passenger-plus";
        private static Harmony _harmony;

        public void OnModLoad(IModContext context)
        {
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void OnModUnload()
        {
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
        }
    }
}
