using HarmonyLib;
using UnityEngine;

namespace TenkaiMenu;

[HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
public static class MatchInfoGuide_Open
{
    public static void Prefix(MatchInfoGuide __instance)
    {
        if (__instance.NormalModeSettings.Count > 0 || __instance.HnSModeSettings.Count > 0)
        {
            __instance.ControllerSelectable.Clear();
            __instance.CreatePlayerEntries();
        }
    }
}

[HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.CreatePlayerEntries))]
public static class MatchInfoGuide_CreatePlayerEntries
{
    private static Vector2 _anchoredPosition;

    public static bool Prefix(MatchInfoGuide __instance)
    {
        __instance.PlayerPool.ReclaimAll();
        var stencil = 51;

        foreach (var playerInfo in GameData.Instance.AllPlayers)
        {
            var component = __instance.PlayerPool.Get<PoolableBehavior>().GetComponent<PlayerIdentifierButton>();
            component.transform.localPosition = new Vector3(0f, 0f, -1f);
            component.Populate(playerInfo);
            component.NameText.text = Utils.GetNameTag(playerInfo, playerInfo.PlayerName, false, true);

            if (_anchoredPosition == Vector2.zero)
                _anchoredPosition = component.NameText.rectTransform.anchoredPosition;

            component.NameText.rectTransform.anchoredPosition = new Vector2(_anchoredPosition.x, _anchoredPosition.y + 0.05f);
            __instance.ControllerSelectable.Add(component.Button);
            component.SetTextStencil(stencil++);
        }

        return false;
    }
}