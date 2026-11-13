using System;
using System.Reflection;
using HarmonyLib;
using Railroader.Modding;

namespace PassengerPlus
{
    public sealed class PassengerPlusEntry : IModEntryPoint
    {
        public static readonly string HarmonyId = "cc.foxden.passenger-plus";
        private static Harmony _harmony;

        private static IModLogger _logger;

        public void OnModLoad(IModContext context)
        {
            _logger = context.Log;

            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            StationAgentBuilder.Register();
        }

        public void OnModUnload()
        {
            StationAgentBuilder.UnRegister();

            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;

            _logger = null;
        }

        #region Logging

        public static void LogInfo(string message)
        {
            _logger?.Info(message);
        }

        public static void LogWarning(string message)
        {
            _logger?.Warning(message);
        }
        
        public static void LogError(string message, Exception ex = null)
        {
            if (ex is null)
            {
                _logger?.Error(message);
            }
            else
            {
                _logger?.Error(ex, message);
            }
        }

        #endregion
    }
}
