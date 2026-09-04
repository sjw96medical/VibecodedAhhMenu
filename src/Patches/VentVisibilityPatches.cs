using HarmonyLib;

namespace TenkaiMenu;

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
public static class PlayerPhysics_SeePlayersInVents
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (!CheatToggles.seePlayersInVents) return;

        var player = __instance?.myPlayer;
        if (player == null || player.AmOwner || player.Data == null || player.Data.IsDead) return;
        if (player.inVent && MeetingHud.Instance == null)
        {
            player.Visible = true;
        }
    }
}
