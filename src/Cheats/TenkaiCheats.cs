using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TenkaiMenu;

public static class TenkaiCheats
{
    private static bool _isScanAnimActive;
    private static bool _isCamsAnimActive;
    private static bool _isInvisibleActive;
    private static float _levelFarmTimer;
    private static float _autoKillTimer;
    private static float _ventTeleportTimer;
    private static Vector2 _invisibilityOriginalPosition;
    private static readonly Dictionary<byte, ushort> _ventTeleportSequenceIds = new();

    private static bool _doorHallucinationSent;


    public static void CloseMeetingCheat()
    {
        if (!CheatToggles.closeMeeting) return;

        if (Utils.isMeeting)
        {
            MeetingHud.Instance.DespawnOnDestroy = false;
            UnityEngine.Object.Destroy(MeetingHud.Instance.gameObject);

            DestroyableSingleton<HudManager>.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false));
            PlayerControl.LocalPlayer.SetKillTimer(GameManager.Instance.LogicOptions.GetKillCooldown());
            ShipStatus.Instance.EmergencyCooldown = GameManager.Instance.LogicOptions.GetEmergencyCooldown();
            Camera.main.GetComponent<FollowerCamera>().Locked = false;
            DestroyableSingleton<HudManager>.Instance.SetHudActive(true);
            ControllerManager.Instance.CloseAndResetAll();
        }
        else if (ExileController.Instance)
        {
            ExileController.Instance.ReEnableGameplay();
            ExileController.Instance.WrapUp();
        }

        CheatToggles.closeMeeting = false;
    }

    public static void SkipMeetingCheat()
    {
        if (!CheatToggles.skipMeeting) return;

        if (Utils.isMeeting)
        {
            MeetingHud.Instance.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0L), null, true, false, 0);
        }

        CheatToggles.skipMeeting = false;
    }

    public static void CallMeetingCheat()
    {
        if (!CheatToggles.callMeeting) return;

        if (Utils.isHost)
        {
            MeetingRoomManager.Instance.AssignSelf(PlayerControl.LocalPlayer, null);
            DestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(PlayerControl.LocalPlayer);
            PlayerControl.LocalPlayer.RpcStartMeeting(null);
        }
        else
        {
            PlayerControl.LocalPlayer.CmdReportDeadBody(null);
        }

        CheatToggles.callMeeting = false;
    }

    public static void ForceStartGameCheat()
    {
        if (!CheatToggles.forceStartGame) return;

        if (Utils.isHost && Utils.isLobby)
        {
            AmongUsClient.Instance.SendStartGame();
        }

        CheatToggles.forceStartGame = false;
    }

    public static void CompleteMyTasksCheat()
    {
        if (CheatToggles.completeMyTasks)
        {
            foreach (var task in PlayerControl.LocalPlayer.myTasks)
            {
                Utils.CompleteTask(task);
            }

            CheatToggles.completeMyTasks = false;
        }
    }

    public static void OpenSabotageMapCheat()
    {
        if (!CheatToggles.sabotageMap) return;

        DestroyableSingleton<HudManager>.Instance.ToggleMapVisible(new MapOptions
        {
            Mode = MapOptions.Modes.Sabotage
        });

        CheatToggles.sabotageMap = false;
    }

    public static void HandleEngineerCheats(EngineerRole engineerRole)
    {
        if (CheatToggles.endlessVentTime)
        {
            engineerRole.inVentTimeRemaining = float.MaxValue;
        }
        else if (engineerRole.inVentTimeRemaining > engineerRole.GetCooldown())
        {
            engineerRole.inVentTimeRemaining = engineerRole.GetCooldown();
        }

        if (CheatToggles.noVentCooldown && engineerRole.cooldownSecondsRemaining > 0f)
        {
            engineerRole.cooldownSecondsRemaining = 0f;
            DestroyableSingleton<HudManager>.Instance.AbilityButton.ResetCoolDown();
            DestroyableSingleton<HudManager>.Instance.AbilityButton.SetCooldownFill(0f);
        }
    }

    public static void HandleShapeshifterCheats(ShapeshifterRole shapeshifterRole)
    {
        if (CheatToggles.endlessSsDuration)
        {
            shapeshifterRole.durationSecondsRemaining = float.MaxValue;
        }
        else if (shapeshifterRole.durationSecondsRemaining > GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.ShapeshifterDuration))
        {
            shapeshifterRole.durationSecondsRemaining = GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.ShapeshifterDuration);
        }
    }

    public static void HandleScientistCheats(ScientistRole scientistRole)
    {
        if (CheatToggles.noVitalsCooldown)
        {
            scientistRole.currentCooldown = 0f;
        }

        if (CheatToggles.endlessBattery)
        {
            scientistRole.currentCharge = float.MaxValue;
        }
        else if (scientistRole.currentCharge > scientistRole.RoleCooldownValue)
        {
            scientistRole.currentCharge = scientistRole.RoleCooldownValue;
        }
    }

    public static void HandleTrackerCheats(TrackerRole trackerRole)
    {
        if (CheatToggles.noTrackingCooldown)
        {
            trackerRole.cooldownSecondsRemaining = 0f;
            trackerRole.delaySecondsRemaining = 0f;

            DestroyableSingleton<HudManager>.Instance.AbilityButton.ResetCoolDown();
            DestroyableSingleton<HudManager>.Instance.AbilityButton.SetCooldownFill(0f);
        }

        if (CheatToggles.noTrackingDelay && MapBehaviour.Instance != null)
        {
            MapBehaviour.Instance.trackedPointDelayTime = GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.TrackerDelay);
        }

        if (CheatToggles.endlessTracking)
        {
            trackerRole.durationSecondsRemaining = float.MaxValue;
        }
        else if (trackerRole.durationSecondsRemaining > GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.TrackerDuration))
        {
            trackerRole.durationSecondsRemaining = GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.TrackerDuration);
        }
    }

    public static void UseVentCheat(HudManager hudManager)
    {
        try
        {
            if (!PlayerControl.LocalPlayer.Data.Role.CanVent && !PlayerControl.LocalPlayer.Data.IsDead)
            {
                hudManager.ImpostorVentButton.gameObject.SetActive(CheatToggles.unlockVents);
            }
        } catch { }
    }

    public static void WalkInVentCheat()
    {
        try
        {
            if (!CheatToggles.walkInVents) return;

            PlayerControl.LocalPlayer.inVent = false;
            PlayerControl.LocalPlayer.moveable = true;
        } catch { }
    }

    public static void KickVentsCheat()
    {
        if (!CheatToggles.kickVents) return;

        foreach (var vent in ShipStatus.Instance.AllVents)
        {
            VentilationSystem.Update(VentilationSystem.Operation.BootImpostors, vent.Id);
        }

        CheatToggles.kickVents = false;
    }

    public static void TeleportEveryoneToVent(int ventIndex)
    {
        if (ShipStatus.Instance?.AllVents == null) return;
        if (ventIndex < 0 || ventIndex >= ShipStatus.Instance.AllVents.Count) return;

        int ventId = ShipStatus.Instance.AllVents[ventIndex].Id;
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player == PlayerControl.LocalPlayer || player.MyPhysics == null) continue;
            SendBootFromVent(player, ventId);
        }
    }

    private static void SendBootFromVent(PlayerControl player, int ventId, int targetClientId = -1)
    {
        if (Utils.isHost && targetClientId < 0)
        {
            player.MyPhysics.RpcBootFromVent(ventId);
            return;
        }

        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return;

        if (!_ventTeleportSequenceIds.TryGetValue(player.PlayerId, out ushort sequenceId))
            sequenceId = 6767;

        MessageWriter enterWriter = MessageWriter.Get(SendOption.Reliable);
        enterWriter.Write(++sequenceId);
        enterWriter.Write((byte)VentilationSystem.Operation.Enter);
        enterWriter.Write((byte)ventId);

        MessageWriter bootWriter = MessageWriter.Get(SendOption.Reliable);
        bootWriter.Write(++sequenceId);
        bootWriter.Write((byte)VentilationSystem.Operation.BootImpostors);
        bootWriter.Write((byte)ventId);

        MessageWriter message = MessageWriter.Get(SendOption.Reliable);
        message.StartMessage(Tags.GameDataTo);
        message.Write(AmongUsClient.Instance.GameId);
        message.WritePacked(targetClientId >= 0 ? targetClientId : AmongUsClient.Instance.HostId);

        WriteVentUpdate(message, player, enterWriter);
        WriteVentUpdate(message, player, bootWriter);

        message.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(message);
        message.Recycle();
        enterWriter.Recycle();
        bootWriter.Recycle();
        _ventTeleportSequenceIds[player.PlayerId] = sequenceId;
    }

    public static void ErrorBan(PlayerControl target)
    {
        if (!Utils.isInGame)
        {
            NotifyBanUnavailable("Error Ban");
            return;
        }

        if (AmongUsClient.Instance == null || target == null || target == PlayerControl.LocalPlayer || target.OwnerId == AmongUsClient.Instance.HostId) return;

        PlayerControl hostPlayer = null;
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player != null && player.OwnerId == AmongUsClient.Instance.HostId)
            {
                hostPlayer = player;
                break;
            }
        }

        if (hostPlayer?.MyPhysics == null) return;
        SendBootFromVent(hostPlayer, 1, target.OwnerId);
    }

    public static void MassBanImps()
    {
        if (!Utils.isInGame)
        {
            NotifyBanUnavailable("Mass Ban Imps");
            return;
        }

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data == null || !RoleManager.IsImpostorRole(player.Data.RoleType)) continue;
            ErrorBan(player);
        }
    }

    public static void MassBanCrewmates()
    {
        if (!Utils.isInGame)
        {
            NotifyBanUnavailable("Mass Ban Crewmate");
            return;
        }

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data == null || RoleManager.IsImpostorRole(player.Data.RoleType)) continue;
            ErrorBan(player);
        }
    }

    public static void ToggleDestroySelectedPlayer(PlayerControl target)
    {
        if (target == null) return;

        if (CheatToggles.destroySelectedPlayer && CheatToggles.destroySelectedPlayerId == target.PlayerId)
        {
            CheatToggles.destroySelectedPlayer = false;
            CheatToggles.destroySelectedPlayerId = -1;
        }
        else
        {
            CheatToggles.destroySelectedPlayer = true;
            CheatToggles.destroySelectedPlayerId = target.PlayerId;
        }
    }

    public static void MassBanAll()
    {
        if (!Utils.isInGame)
        {
            NotifyBanUnavailable("Mass Ban All");
            return;
        }

        if (AmongUsClient.Instance == null) return;

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) continue;
            ErrorBan(player);
        }
    }

    private static void NotifyBanUnavailable(string featureName)
    {
        HudManager.Instance?.Notifier?.AddDisconnectMessage(
            $"<color=#fff><b><color=#c40033>TenkaiMenu</color></b> <color=#ffff00>{featureName}</color> failed. The game needs to be started.</color>");
    }

    private static void WriteVentUpdate(MessageWriter message, PlayerControl player, MessageWriter operation)
    {
        message.StartMessage((byte)GameDataTypes.RpcFlag);
        message.WritePacked(ShipStatus.Instance.NetId);
        message.Write((byte)RpcCalls.UpdateSystem);
        message.Write((byte)SystemTypes.Ventilation);
        message.WriteNetObject(player);
        message.Write(operation, false);
        message.EndMessage();
    }

    public static void ProcessVentTeleportCheats()
    {
        if ((!CheatToggles.spamVentTPAll && !CheatToggles.spamVentTPRandom && !CheatToggles.spamVentTPImps && !CheatToggles.destroySelectedPlayer) || ShipStatus.Instance?.AllVents == null) return;
        if (Time.time < _ventTeleportTimer) return;

        var allVents = ShipStatus.Instance.AllVents;
        if (allVents.Count == 0) return;

        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player == PlayerControl.LocalPlayer || player.MyPhysics == null) continue;

            bool isImpostor = player.Data != null && RoleManager.IsImpostorRole(player.Data.RoleType);
            bool isSelectedDestroyTarget = player.PlayerId == CheatToggles.destroySelectedPlayerId;
            if (CheatToggles.destroySelectedPlayer && !isSelectedDestroyTarget) continue;
            if (!CheatToggles.destroySelectedPlayer && CheatToggles.spamVentTPImps && !isImpostor) continue;
            if (!CheatToggles.destroySelectedPlayer && !CheatToggles.spamVentTPImps && !CheatToggles.spamVentTPAll && !CheatToggles.spamVentTPRandom) continue;

            int ventIndex = CheatToggles.spamVentTPRandom
                ? UnityEngine.Random.Range(0, allVents.Count)
                : Mathf.Clamp(CheatToggles.ventTPAllVentIdx, 0, allVents.Count - 1);
            if (CheatToggles.spamVentTPImps || CheatToggles.destroySelectedPlayer)
                ventIndex = UnityEngine.Random.Range(0, allVents.Count);
                SendBootFromVent(player, allVents[ventIndex].Id);
        }

        _ventTeleportTimer = Time.time + 0.75f;
    }

    public static void DoorHallucinationAllCheat()
    {
        if (!CheatToggles.doorHallucinationAll)
        {
            _doorHallucinationSent = false;
            return;
        }

        if (_doorHallucinationSent) return;
        _doorHallucinationSent = true;

        if (ShipStatus.Instance == null)
        {
            HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: The game must be started.");
            return;
        }

        foreach (PlayerControl target in PlayerControl.AllPlayerControls)
        {
            if (target == null || target.AmOwner || target.Data == null || target.Data.Disconnected) continue;
            DoorHallucination(target);
        }
    }

    // --- OTHER CHEATS ---

    public static void UpdateFollowSelected()
    {
        if (!CheatToggles.followSelectedPlayer)
        {
            return;
        }

        if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.MyPhysics?.body == null)
        {
            CheatToggles.followSelectedPlayer = false;
            CheatToggles.followSelectedPlayerId = -1;
            return;
        }

        PlayerControl target = null;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player.Data == null) continue;
            if (player.PlayerId != CheatToggles.followSelectedPlayerId) continue;
            if (player.AmOwner || player.Data.IsDead) continue;
            target = player;
            break;
        }

        if (target == null)
        {
            CheatToggles.followSelectedPlayer = false;
            CheatToggles.followSelectedPlayerId = -1;
            return;
        }

        try
        {
            if (Camera.main != null)
            {
                var fc = Camera.main.gameObject.GetComponent<FollowerCamera>();
                if (fc != null)
                {
                    fc.enabled = true;
                    fc.SetTarget(PlayerControl.LocalPlayer);
                }
            }

            MoveTowardPlayer(target.transform.position);
        }
        catch
        {
            CheatToggles.followSelectedPlayer = false;
            CheatToggles.followSelectedPlayerId = -1;
        }
    }

    private static void MoveTowardPlayer(Vector2 targetPos)
    {
        var body = PlayerControl.LocalPlayer.MyPhysics.body;
        Vector2 myPos = body.position;
        if (Vector2.Distance(myPos, targetPos) > 0.8f)
        {
            Vector2 dir = (targetPos - myPos).normalized;
            float speed = PlayerControl.LocalPlayer.MyPhysics.TrueSpeed;

            body.position = Vector2.MoveTowards(myPos, targetPos, speed * Time.deltaTime);
            body.velocity = dir * speed;
            PlayerControl.LocalPlayer.MyPhysics.FlipX = dir.x < 0f;
            return;
        }

        body.velocity = Vector2.zero;
    }

    public static void DoorHallucination(PlayerControl target)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            if (ShipStatus.Instance == null)
            {
                HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: Game has not started.");
            }
            else
            {
                AmongUsClient client = AmongUsClient.Instance;
                if (client == null || !client.AmConnected)
                {
                    HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: Not connected.");
                }
                else
                {
                    int targetClientId = Utils.getClientIdByPlayer(target);
                    if (targetClientId < 0)
                    {
                        HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: Could not resolve target client.");
                    }
                    else
                    {
                        HashSet<SystemTypes> rooms = new HashSet<SystemTypes>();
                        Il2CppReferenceArray<OpenableDoor> allDoors = ShipStatus.Instance.AllDoors;
                        for (int i = 0; i < allDoors.Length; i++)
                        {
                            if (allDoors[i] != null)
                            {
                                rooms.Add(allDoors[i].Room);
                            }
                        }

                        if (rooms.Count == 0)
                        {
                            HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: No doors found on this map.");
                        }
                        else
                        {
                            foreach (SystemTypes room in rooms)
                            {
                                MessageWriter w = client.StartRpcImmediately(ShipStatus.Instance.NetId, 27, SendOption.Reliable, targetClientId);
                                w.Write((byte)room);
                                client.FinishRpcImmediately(w);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            HudManager.Instance.Notifier.AddDisconnectMessage("Door Hallucination failed: exception.");
        }
    }

    public static void KillAllCheat()
    {
        if (!CheatToggles.killAll) return;

        if (Utils.isLobby)
        {
            HudManager.Instance.Notifier.AddDisconnectMessage("Killing in lobby disabled for being too buggy");
        }
        else
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                Utils.MurderPlayer(player, MurderResultFlags.Succeeded);
            }
        }

        CheatToggles.killAll = false;
    }

    public static void KillAllCrewCheat()
    {
        if (!CheatToggles.killAllCrew) return;

        if (Utils.isLobby)
        {
            HudManager.Instance.Notifier.AddDisconnectMessage("Killing in lobby disabled for being too buggy");
        }
        else
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data.Role.TeamType == RoleTeamTypes.Crewmate)
                {
                    Utils.MurderPlayer(player, MurderResultFlags.Succeeded);
                }
            }
        }

        CheatToggles.killAllCrew = false;
    }

    public static void KillAllImpsCheat()
    {
        if (!CheatToggles.killAllImps) return;

        if (Utils.isLobby)
        {
            HudManager.Instance.Notifier.AddDisconnectMessage("Killing in lobby disabled for being too buggy");
        }
        else
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data.Role.TeamType == RoleTeamTypes.Impostor)
                {
                    Utils.MurderPlayer(player, MurderResultFlags.Succeeded);
                }
            }
        }

        CheatToggles.killAllImps = false;
    }

    public static void LevelFarmCheat()
    {
        if (!CheatToggles.levelFarm || Utils.isLobby) return;

        _levelFarmTimer -= Time.deltaTime;
        if (_levelFarmTimer <= 0f)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player == PlayerControl.LocalPlayer || player.Data == null || player.Data.Role.TeamType != RoleTeamTypes.Crewmate) continue;
                Utils.MurderPlayer(player, MurderResultFlags.Succeeded);
            }
            _levelFarmTimer = Mathf.Clamp(CheatToggles.levelFarmCooldownMs, 10f, 200f) / 1000f;
        }
    }

    public static void ProtectCheat()
    {
        if (!Utils.isHost || Utils.isLobby) return;

        foreach (var player in ProtectUI.playersToProtect)
        {
            if (player.protectedByGuardianId == -1)
            {
                PlayerControl.LocalPlayer.RpcProtectPlayer(player, PlayerControl.LocalPlayer.cosmetics.ColorId);
            }
        }
    }

    public static void TeleportCursorCheat()
    {
        if (PlayerControl.LocalPlayer?.NetTransform == null || Camera.main == null) return;
        if (!CheatToggles.teleportCursor) return;

        if (Input.GetMouseButtonDown(1))
        {
            PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        }
    }

    public static void SpeedHackCheat()
    {
        if (CheatToggles.speedHackEnabled)
        {
            Time.timeScale = Mathf.Clamp(CheatToggles.gameSpeed, 0.1f, 3f);
        }
        else if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
        }
    }

    private static readonly string[] ConfuseHats =
    {
        "hat_pk05_Fedora",
        "hat_pk03_Headphones",
        "hat_screamghostface",
        "hat_pk08_Cowboy",
        "hat_pk07_Bandana",
        "hat_pk01_Beanie",
        "hat_pk02_PirateHat"
    };

    private static readonly string[] ConfuseVisors =
    {
        "visor_animesunglassesVisor",
        "visor_D2CGoggles",
        "visor_pk01_PaperMaskVisor",
        "visor_Scar",
        "visor_eliksni"
    };

    private static readonly string[] ConfuseSkins =
    {
        "skin_Hazmat-Greenskin",
        "skin_Science",
        "skin_Mech",
        "skin_SuitW",
        "skin_rhm",
        "skin_screamghostface"
    };

    public static void ConfuseNowCheat()
    {
        if (!Utils.isPlayer || PlayerControl.LocalPlayer == null) return;

        var local = PlayerControl.LocalPlayer;

        local.RpcSetHat(ConfuseHats[UnityEngine.Random.Range(0, ConfuseHats.Length)]);
        local.RpcSetVisor(ConfuseVisors[UnityEngine.Random.Range(0, ConfuseVisors.Length)]);
        local.RpcSetSkin(ConfuseSkins[UnityEngine.Random.Range(0, ConfuseSkins.Length)]);
    }

    public static void AutoKillCheat()
    {
        if (!CheatToggles.autoKill || !Utils.isPlayer) return;
        if (PlayerControl.LocalPlayer.Data.Role.TeamType != RoleTeamTypes.Impostor) return;

        _autoKillTimer -= Time.deltaTime;
        if (_autoKillTimer > 0f) return;
        _autoKillTimer = 0.25f;

        PlayerControl closest = null;
        float minDistance = float.MaxValue;
        Vector2 localPosition = PlayerControl.LocalPlayer.GetTruePosition();

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player == PlayerControl.LocalPlayer || player.Data == null || player.Data.IsDead) continue;

            float distance = Vector2.Distance(localPosition, player.GetTruePosition());
            if (distance >= minDistance) continue;

            closest = player;
            minDistance = distance;
        }

        if (closest != null)
        {
            Utils.MurderPlayer(closest, MurderResultFlags.Succeeded);
        }
    }

    public static void NoClipCheat()
    {
        try
        {
            PlayerControl.LocalPlayer.Collider.enabled = !(CheatToggles.noClip || PlayerControl.LocalPlayer.onLadder);
        } catch { }
    }

    public static void PlayScannerCheat()
    {
        if (CheatToggles.animMedScan && !_isScanAnimActive)
        {
            Utils.ForceSetScanner(PlayerControl.LocalPlayer, true);
            _isScanAnimActive = true;
        }
        else if (!CheatToggles.animMedScan && _isScanAnimActive)
        {
            Utils.ForceSetScanner(PlayerControl.LocalPlayer, false);
            _isScanAnimActive = false;
        }
    }

    public static void PlayAnimationCheat()
    {
        if (CheatToggles.animPet && Utils.isPlayer && PlayerControl.LocalPlayer.cosmetics != null && PlayerControl.LocalPlayer.cosmetics.CurrentPet != null)
        {
            RpcPetMessage rpcMessage = new(PlayerControl.LocalPlayer.MyPhysics.NetId,
                PlayerControl.LocalPlayer.cosmetics.CurrentPet.PettingPlayerPosition,
                PlayerControl.LocalPlayer.cosmetics.CurrentPet.transform.position);
            AmongUsClient.Instance.LateBroadcastReliableMessage(Unsafe.As<IGameDataMessage>(rpcMessage));
        }

        byte mapId = Utils.GetCurrentMapID();

        if (mapId == byte.MaxValue) return;

        var map = (MapNames)mapId;

        if (CheatToggles.animShields)
        {
            if (map is MapNames.Skeld or MapNames.Dleks)
            {
                Utils.ForcePlayAnimation((byte)TaskTypes.PrimeShields);
            }
            CheatToggles.animShields = false;
        }

        if (CheatToggles.animAsteroids)
        {
            if (map is MapNames.Skeld or MapNames.Dleks or MapNames.Polus)
            {
                Utils.ForcePlayAnimation((byte)TaskTypes.ClearAsteroids);
            }
            else
            {
                CheatToggles.animAsteroids = false;
            }
        }

        if (CheatToggles.animEmptyGarbage)
        {
            if (map is MapNames.Skeld or MapNames.Dleks)
            {
                Utils.ForcePlayAnimation((byte)TaskTypes.EmptyGarbage);
            }

            CheatToggles.animEmptyGarbage = false;
        }

        if (map is not (MapNames.MiraHQ or MapNames.Fungle))
        {
            if (CheatToggles.animCamsInUse && !_isCamsAnimActive)
            {
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Security, 1);
                _isCamsAnimActive = true;
            }
            else if (!CheatToggles.animCamsInUse && _isCamsAnimActive)
            {
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Security, 0);
                _isCamsAnimActive = false;
            }
        }
        else
        {
            CheatToggles.animCamsInUse = false;
        }
    }

    public static void StopShipAnimCheats()
    {
        CheatToggles.animShields = false;
        CheatToggles.animAsteroids = false;
        CheatToggles.animEmptyGarbage = false;
        CheatToggles.animMedScan = false;
        CheatToggles.animCamsInUse = false;

        _isCamsAnimActive = false;
        _isScanAnimActive = false;
    }

    public static void InvisibilityCheat()
    {
        if (!Utils.isPlayer || PlayerControl.LocalPlayer == null) return;

        try
        {
            if (CheatToggles.invisibility)
            {
                if (!_isInvisibleActive)
                {
                    _invisibilityOriginalPosition = PlayerControl.LocalPlayer.GetTruePosition();
                    PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(new Vector2(999f, 999f));
                    
                    List<PlayerControl> alivePlayersExceptLocal = new();
                    foreach (var player in PlayerControl.AllPlayerControls)
                    {
                        if (!player.AmOwner && !player.Data.IsDead)
                        {
                            alivePlayersExceptLocal.Add(player);
                        }
                    }

                    if (alivePlayersExceptLocal.Count > 0)
                    {
                        PlayerControl randomPlayer = alivePlayersExceptLocal[UnityEngine.Random.Range(0, alivePlayersExceptLocal.Count)];
                        Camera.main.gameObject.GetComponent<FollowerCamera>().SetTarget(randomPlayer);
                        PlayerControl.LocalPlayer.moveable = false;
                    }

                    _isInvisibleActive = true;
                }
            }
            else if (_isInvisibleActive)
            {
                PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(_invisibilityOriginalPosition);
                Camera.main.gameObject.GetComponent<FollowerCamera>().SetTarget(PlayerControl.LocalPlayer);
                PlayerControl.LocalPlayer.moveable = true;
                
                _isInvisibleActive = false;
            }
        }
        catch { }
    }
}
