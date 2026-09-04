using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TenkaiMenu;

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
public static class ExtendedLobbyListPatch
{
    private static Scroller scroller;
    private static readonly int extraSlots = 15;

    public static void Prefix(FindAGameManager __instance)
    {
        if (!CheatToggles.extendedLobbyList)
        {
            scroller = null;
            return;
        }

        try
        {
            GameContainer baseContainer = __instance.gameContainers[4];
            GameObject scrollRoot = new("TenkaiExtendedLobbyScroller");
            scrollRoot.transform.SetParent(((Component)baseContainer).transform.parent);

            scroller = scrollRoot.AddComponent<Scroller>();
            scroller.Inner = scrollRoot.transform;
            scroller.MouseMustBeOverToScroll = true;
            scroller.ScrollWheelSpeed = 0.3f;
            scroller.SetYBoundsMin(0f);
            scroller.SetYBoundsMax(3.5f);
            scroller.allowY = true;

            var mask = ((Component)((Component)baseContainer).transform.parent).gameObject.AddComponent<BoxCollider2D>();
            mask.size = new Vector2(100f, 100f);
            scroller.ClickMask = mask;

            var expanded = new List<GameContainer>();
            foreach (var container in __instance.gameContainers)
            {
                ((Component)container).transform.SetParent(scrollRoot.transform);
                var position = ((Component)container).transform.position;
                ((Component)container).transform.position = new Vector3(position.x, position.y, 25f);
                expanded.Add(container);
            }

            for (int index = 0; index < extraSlots; index++)
            {
                var clone = UnityEngine.Object.Instantiate(baseContainer, scrollRoot.transform);
                var position = ((Component)clone).transform.position;
                ((Component)clone).transform.position = new Vector3(position.x, position.y - 0.75f * (index + 1), 25f);
                expanded.Add(clone);
            }

            __instance.gameContainers = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameContainer>(expanded.ToArray());
        }
        catch (Exception exception)
        {
            TenkaiMenu.Log?.LogError($"Extended lobby list failed: {exception.Message}");
            scroller = null;
        }
    }

    [HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
    public static class RefreshListPatch
    {
        public static void Postfix()
        {
            if (!CheatToggles.extendedLobbyList || scroller == null) return;

            try { scroller.ScrollRelative(new Vector2(0f, -100f)); }
            catch { }
        }
    }
}
