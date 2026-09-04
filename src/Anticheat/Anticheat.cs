using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace TenkaiMenu;

public static class Anticheat
{
    public enum ResponseMode { Notify, Kick, Ban }

    public static bool IsEnabled = true;
    public static float KickCooldownSeconds = 2f;
    public static ResponseMode DetectionResponseMode = ResponseMode.Notify;
    public static bool DetectInvalidFriendCodes = true;
    public static bool BlockInvalidSabotages = true;
    public static bool UsePlayerBanList = true;
    public static bool UseNameBanList = true;
    public static bool UseWordBanList = true;
    public static bool BanWordsLobbyOnly = true;
    public static bool HideKickReason;
    public static bool DetectPlayerLevels;
    public static int MaxLevelThreshold = 500;
    public static bool AutoKickLevels;
    public static int MinLevelThreshold = 20;
    public static bool DetectInvalidRpcs = true;
    public static bool LimitRpcRate = true;
    public static int PacketRateLimit = 50;

    private const float FriendCodeSettleSeconds = 3f;
    private const float FriendCodeReportInterval = 10f;
    private const float RepeatedSabotageInterval = 2f;
    private const float FastTaskInterval = 1.25f;
    private static readonly Dictionary<int, float> LastAction = new();
    private static readonly Dictionary<int, int> RpcCounts = new();
    private static readonly Dictionary<int, TaskState> PlayerStates = new();
    private sealed class TaskState
    {
        public uint LastTaskId = uint.MaxValue;
        public float LastTaskTime = -999f;
        public float LastSabotageTime = -999f;
        public byte LastSabotageAmount;
        public string LastFriendCode = "";
        public float FriendCodeStableSince = -999f;
        public float LastInvalidFriendCodeReportTime = -999f;
        public bool HasSetLevel;
        public bool HasSetName;
        public int TimesAttemptedKilled;
    }

    private static float lastRateReset;
    private static float lastBanCheck;
    private static readonly string SaveDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "TenkaiMenu");
    private static string PlayerList => Path.Combine(SaveDirectory, "BanPlayerList.txt");
    private static string NameList => Path.Combine(SaveDirectory, "BanNameList.txt");
    private static string WordList => Path.Combine(SaveDirectory, "BanWordList.txt");

    public static void Initialize() => EnsureLists();

    public static void ResetRoundState()
    {
        PlayerStates.Clear();
        RpcCounts.Clear();
        LastAction.Clear();
    }

    public static void Update()
    {
        if (!IsEnabled || !Utils.isInGame || Time.time - lastBanCheck < 1f) return;
        lastBanCheck = Time.time;
        EnsureLists();

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (IsIgnoredPlayer(player) || player.Data == null) continue;
            var state = GetState(player);
            string friendCode = GetDataString(player.Data, "FriendCode");
            TrackFriendCode(state, friendCode);

            bool settled = !string.IsNullOrWhiteSpace(friendCode) && Time.time - state.FriendCodeStableSince >= FriendCodeSettleSeconds;
            bool reportDue = Time.time - state.LastInvalidFriendCodeReportTime >= FriendCodeReportInterval;
            if (DetectInvalidFriendCodes && settled && reportDue && !IsValidFriendCode(friendCode))
            {
                state.LastInvalidFriendCodeReportTime = Time.time;
                Report(player, "Invalid friend code.");
            }
            else if (UsePlayerBanList && MatchesPlayerList(player)) Report(player, "Player is on the ban player list.");
            else if (UseNameBanList && MatchesNameList(player.Data.PlayerName)) Report(player, "Player name is on the ban name list.");
        }
    }

    public static bool CheckRpc(PlayerControl player, byte callId, MessageReader reader)
    {
        if (!IsEnabled || IsIgnoredPlayer(player) || player.Data == null) return true;
        int initialPosition = reader.Position;
        try
        {
            if (!DetectInvalidRpcs) return true;
            if (!AllowRate(player)) return false;
            if (!IsKnownRpc(callId) && player.OwnerId != AmongUsClient.Instance.HostId)
                return Report(player, $"Unregistered RPC received: {callId}");

            if (player.OwnerId != AmongUsClient.Instance.HostId && CheckGameplayRpcRules(player, callId)) return false;
            if (callId == (byte)RpcCalls.SetLevel && !CheckLevel(player, reader)) return false;
            if (callId == (byte)RpcCalls.SetName && !CheckName(player, reader)) return false;
            if (callId == (byte)RpcCalls.CompleteTask && !CheckTask(player, reader)) return false;
            if (callId == (byte)RpcCalls.MurderPlayer && !CheckMurder(player, reader)) return false;
            if (callId == (byte)RpcCalls.SendChat && UseWordBanList && (!BanWordsLobbyOnly || Utils.isLobby) && reader.BytesRemaining > 0 && FileMatches(WordList, reader.ReadString()))
                return Report(player, "Chat message contains a banned word.");
            return true;
        }
        finally { reader.Position = initialPosition; }
    }

    private static bool CheckGameplayRpcRules(PlayerControl player, byte callId)
    {
        if (callId is (byte)RpcCalls.EnterVent or (byte)RpcCalls.ExitVent && !player.Data.IsDead && !player.Data.Role.CanVent)
            return !Report(player, "Player without a vent-capable role attempted to use a vent.");
        if (callId == (byte)RpcCalls.CloseDoorsOfType && !RoleManager.IsImpostorRole(player.Data.RoleType))
            return !Report(player, "Non-impostor attempted to close doors.");
        if (callId is (byte)RpcCalls.SetTasks or (byte)RpcCalls.ExtendLobbyTimer or (byte)RpcCalls.CloseMeeting)
            return !Report(player, "Host-only RPC received from a client.");
        if (Utils.isInGame && callId is (byte)RpcCalls.SetColor or (byte)RpcCalls.SetHatStr or (byte)RpcCalls.SetSkinStr or (byte)RpcCalls.SetVisorStr or (byte)RpcCalls.SetPetStr or (byte)RpcCalls.SetNamePlateStr)
            return !Report(player, "Cosmetic RPC received during gameplay.");
        if (Utils.isLobby && callId is (byte)RpcCalls.StartMeeting or (byte)RpcCalls.ReportDeadBody or (byte)RpcCalls.SendChatNote or (byte)RpcCalls.CloseMeeting or (byte)RpcCalls.Exiled or (byte)RpcCalls.CastVote or (byte)RpcCalls.ClearVote or (byte)RpcCalls.SetRole or (byte)RpcCalls.CompleteTask or (byte)RpcCalls.MurderPlayer)
            return !Report(player, "Gameplay RPC received in the lobby.");
        return false;
    }

    private static bool CheckLevel(PlayerControl player, MessageReader reader)
    {
        int level = reader.ReadPackedInt32() + 1;
        var state = GetState(player);
        if (state.HasSetLevel && !Utils.isLocalGame) return Report(player, "Player attempted to set their level more than once.");
        state.HasSetLevel = true;
        if (DetectPlayerLevels && level > MaxLevelThreshold) Report(player, $"Player level {level} exceeds the configured limit.", false);
        return !(AutoKickLevels && level <= MinLevelThreshold) || Report(player, $"Player level {level} is below the configured minimum.");
    }

    private static bool CheckName(PlayerControl player, MessageReader reader)
    {
        if (Utils.isHost || reader.BytesRemaining <= 4) return true;
        var state = GetState(player);
        reader.ReadUInt32();
        string name = reader.ReadString();
        if (state.HasSetName && !Utils.isLocalGame) return Report(player, "Player attempted to change their name more than once.");
        state.HasSetName = true;
        return !name.Contains("<") || Report(player, "Invalid name content.");
    }

    private static bool CheckTask(PlayerControl player, MessageReader reader)
    {
        uint taskId = reader.ReadPackedUInt32();
        var state = GetState(player);
        bool assigned = false;
        foreach (var task in player.Data.Tasks) if (task.Id == taskId) { assigned = true; break; }
        if (RoleManager.IsImpostorRole(player.Data.RoleType)) return Report(player, "Impostor attempted to complete a task.");
        if (!assigned) return Report(player, $"Task ID {taskId} is not assigned to the player.");
        if (state.LastTaskId == taskId) return Report(player, $"Task ID {taskId} was completed more than once.");
        float elapsed = Time.time - state.LastTaskTime;
        if (elapsed < FastTaskInterval) return Report(player, $"Tasks were completed too quickly ({elapsed:0.##} seconds apart).");
        state.LastTaskTime = Time.time;
        state.LastTaskId = taskId;
        return true;
    }

    private static bool CheckMurder(PlayerControl player, MessageReader reader)
    {
        PlayerControl target = reader.ReadNetObject<PlayerControl>();
        if (target == null) return true;
        if (!RoleManager.IsImpostorRole(player.Data.RoleType) || player.Data.IsDead || RoleManager.IsImpostorRole(target.Data.RoleType))
            return Report(player, "Invalid murder attempt.");
        if (!target.Data.IsDead) return true;
        var state = GetState(player);
        state.TimesAttemptedKilled++;
        if (state.TimesAttemptedKilled >= 10) return Report(player, "Repeated murder attempts against a dead player.");
        return false;
    }

    public static bool CheckSystemUpdate(PlayerControl player, SystemTypes systemType, MessageReader reader)
    {
        if (!IsEnabled || !BlockInvalidSabotages || IsIgnoredPlayer(player) || player.Data == null || player.OwnerId == AmongUsClient.Instance.HostId) return true;
        if (systemType is not (SystemTypes.Sabotage or SystemTypes.Electrical or SystemTypes.Comms or SystemTypes.Reactor or SystemTypes.LifeSupp or SystemTypes.Laboratory or SystemTypes.HeliSabotage)) return true;
        int initialPosition = reader.Position;
        try
        {
            byte amount = reader.ReadByte();
            var state = GetState(player);
            bool repeated = amount == state.LastSabotageAmount && Time.time - state.LastSabotageTime <= RepeatedSabotageInterval;
            bool invalid = systemType == SystemTypes.Sabotage
                ? !RoleManager.IsImpostorRole(player.Data.RoleType) || (ShipStatus.Instance.Systems[SystemTypes.Sabotage].Cast<SabotageSystemType>().Timer > 0f && !repeated)
                : IsDirectSabotage(systemType, amount);
            if (!invalid)
            {
                if (systemType == SystemTypes.Sabotage) { state.LastSabotageAmount = amount; state.LastSabotageTime = Time.time; }
                return true;
            }
            Notify(player, "Invalid sabotage RPC received.");
            Respond(player, "Invalid sabotage RPC received.");
            return false;
        }
        finally { reader.Position = initialPosition; }
    }

    private static bool IsDirectSabotage(SystemTypes systemType, byte amount) => systemType is SystemTypes.Electrical or SystemTypes.Comms or SystemTypes.Reactor or SystemTypes.Laboratory or SystemTypes.HeliSabotage or SystemTypes.LifeSupp && (amount & 128) != 0;
    private static TaskState GetState(PlayerControl player) { if (!PlayerStates.TryGetValue(player.OwnerId, out var state)) PlayerStates[player.OwnerId] = state = new TaskState(); return state; }
    private static void TrackFriendCode(TaskState state, string friendCode) { if (friendCode == state.LastFriendCode) return; state.LastFriendCode = friendCode; state.FriendCodeStableSince = Time.time; state.LastInvalidFriendCodeReportTime = -999f; }
    private static bool AllowRate(PlayerControl player) { if (!LimitRpcRate) return true; if (Time.time - lastRateReset >= 1f) { RpcCounts.Clear(); lastRateReset = Time.time; } RpcCounts.TryGetValue(player.OwnerId, out int count); RpcCounts[player.OwnerId] = ++count; return count <= Mathf.Max(1, PacketRateLimit) || Report(player, "RPC rate limit exceeded."); }
    private static bool Report(PlayerControl player, string reason, bool respond = true) { if (IsIgnoredPlayer(player)) return true; Notify(player, reason); if (respond) Respond(player, reason); return !Utils.isHost || DetectionResponseMode == ResponseMode.Notify || !respond; }
    private static void Respond(PlayerControl player, string reason) { if (!Utils.isHost || player == null || Time.time - (LastAction.TryGetValue(player.OwnerId, out float time) ? time : -999f) < KickCooldownSeconds) return; LastAction[player.OwnerId] = Time.time; if (DetectionResponseMode != ResponseMode.Notify) AmongUsClient.Instance.KickPlayer(player.OwnerId, DetectionResponseMode == ResponseMode.Ban); }
    private static void Notify(PlayerControl player, string reason) { if (HideKickReason) reason = "Unauthorized action detected."; HudManager.Instance?.Notifier?.AddDisconnectMessage($"<color=#4f92ff><b>[TenkaiMenu Anti-Cheat]</b></color> {player?.Data?.PlayerName}: {reason}"); }
    private static bool IsKnownRpc(byte callId) { foreach (RpcCalls rpc in Enum.GetValues(typeof(RpcCalls))) if ((byte)rpc == callId) return true; return false; }
    private static bool IsValidFriendCode(string value) { if (string.IsNullOrWhiteSpace(value)) return false; int separator = value.LastIndexOf('#'); if (separator <= 0 || value.Length - separator - 1 != 4) return false; for (int i = separator + 1; i < value.Length; i++) if (!char.IsDigit(value[i])) return false; return true; }
    private static bool IsIgnoredPlayer(PlayerControl player) => player == null || player == PlayerControl.LocalPlayer || player.AmOwner || (AmongUsClient.Instance != null && player.OwnerId == AmongUsClient.Instance.ClientId);
    private static string GetDataString(object data, string property) { try { return data.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.GetValue(data)?.ToString() ?? ""; } catch { return ""; } }
    private static bool MatchesPlayerList(PlayerControl player) => FileMatches(PlayerList, GetDataString(player.Data, "FriendCode")) || FileMatches(PlayerList, GetDataString(player.Data, "Puid"));
    private static bool MatchesNameList(string name) => FileMatches(NameList, name);
    private static bool FileMatches(string path, string value) { if (string.IsNullOrWhiteSpace(value) || !File.Exists(path)) return false; foreach (string raw in File.ReadAllLines(path)) { string pattern = raw.Trim(); if (pattern.Length == 0 || pattern.StartsWith("//") || pattern.StartsWith("#")) continue; if (pattern.StartsWith("**") && pattern.EndsWith("**") && value.Contains(pattern[2..^2], StringComparison.OrdinalIgnoreCase)) return true; if (pattern.StartsWith("**") && value.EndsWith(pattern[2..], StringComparison.OrdinalIgnoreCase)) return true; if (pattern.EndsWith("**") && value.StartsWith(pattern[..^2], StringComparison.OrdinalIgnoreCase)) return true; if (string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase)) return true; } return false; }
    private static void EnsureLists() { if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory); foreach (string path in new[] { PlayerList, NameList, WordList }) if (!File.Exists(path)) File.WriteAllText(path, "# Enter one entry per line\n"); }
    private static bool IsPlatformValid(object platformData) { string platform = platformData.GetType().GetProperty("Platform")?.GetValue(platformData)?.ToString() ?? ""; if (string.IsNullOrWhiteSpace(platform) || platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return false; string property = platform.Contains("Playstation", StringComparison.OrdinalIgnoreCase) ? "PsnPlatformId" : "XboxPlatformId"; string id = platformData.GetType().GetProperty(property)?.GetValue(platformData)?.ToString() ?? ""; if (platform.Contains("StandaloneWin10", StringComparison.OrdinalIgnoreCase) || platform.Contains("Xbox", StringComparison.OrdinalIgnoreCase)) return id.Length is >= 10 and <= 16; if (platform.Contains("Playstation", StringComparison.OrdinalIgnoreCase)) return id.Length is >= 14 and <= 20; return true; }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    private static class PlayerStartPlatformPatch
    {
        private static void Postfix(PlayerControl __instance) { if (!IsEnabled || !Utils.isLobby || IsIgnoredPlayer(__instance)) return; try { var client = AmongUsClient.Instance.GetClientFromCharacter(__instance); var platformData = client?.PlatformData; if (platformData != null && !IsPlatformValid(platformData)) Report(__instance, "Invalid platform data detected."); } catch { } }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    private static class PlayerControlRpcPatch { private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader) => CheckRpc(__instance, callId, reader); }
    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
    private static class PlayerPhysicsRpcPatch { private static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader) => CheckRpc(__instance.myPlayer, callId, reader); }
    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
    private static class NetworkTransformRpcPatch { private static bool Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader) => CheckRpc(__instance.myPlayer, callId, reader); }
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(MessageReader))]
    private static class UpdateSystemPatch { private static bool Prefix(SystemTypes systemType, PlayerControl player, MessageReader msgReader) => CheckSystemUpdate(player, systemType, msgReader); }
}
