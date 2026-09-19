using System.Collections;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace AutoRejoin
{
    [BepInPlugin("com.example.autorejoin", "AutoRejoin", "1.1.0")]
    public class AutoRejoinPlugin : BasePlugin
    {
        public static ConfigEntry<float> RejoinDelay;
        public static ConfigEntry<int> MaxAttempts;

        // Remembered while we're in a lobby
        public static int LastGameId;
        public static int Attempts;

        public static BepInEx.Logging.ManualLogSource LogSource;
        public static void Instance_Log(string msg) => LogSource?.LogInfo(msg);

        public override void Load()
        {
            RejoinDelay = Config.Bind("General", "RejoinDelaySeconds", 5f,
                "Seconds to wait before rejoining.");
            MaxAttempts = Config.Bind("General", "MaxAttempts", 3,
                "Give up after this many failed rejoin attempts in a row.");

            LogSource = Log;
            new Harmony("com.example.autorejoin").PatchAll();
            Log.LogInfo("AutoRejoin loaded");
        }
    }

    // Remember the lobby code once we've successfully joined; reset attempt counter
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    public static class RememberLobbyPatch
    {
        public static void Postfix(AmongUsClient __instance)
        {
            AutoRejoinPlugin.LastGameId = __instance.GameId;
            AutoRejoinPlugin.Attempts = 0;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.HandleDisconnect),
        typeof(DisconnectReasons), typeof(string))]
    public static class DisconnectPatch
    {
        public static void Postfix(AmongUsClient __instance, DisconnectReasons reason, string stringReason)
        {
            // Log so you can see exactly what your game version emits
            AutoRejoinPlugin.Instance_Log($"Disconnect: {reason} / {stringReason}");

            if (!ShouldRejoin(reason)) return;
            if (AutoRejoinPlugin.LastGameId == 0) return;
            if (AutoRejoinPlugin.Attempts >= AutoRejoinPlugin.MaxAttempts.Value) return;

            AutoRejoinPlugin.Attempts++;
            __instance.StartCoroutine(
                RejoinRoutine(__instance, AutoRejoinPlugin.LastGameId).WrapToIl2Cpp());
        }

        // Rejoin on everything except bans and deliberate leaves.
        // (If you leave on purpose, you don't want to be dragged back in.)
        private static bool ShouldRejoin(DisconnectReasons reason)
        {
            switch (reason)
            {
                case DisconnectReasons.Banned:
                case DisconnectReasons.ExitGame:
                case DisconnectReasons.IntentionalLeaving:
                    return false;
                default:
                    return true;
            }
        }

        private static IEnumerator RejoinRoutine(AmongUsClient client, int gameId)
        {
            yield return new WaitForSeconds(AutoRejoinPlugin.RejoinDelay.Value);
            client.StartCoroutine(client.CoJoinOnlineGameFromCode(gameId));
        }
    }
}
