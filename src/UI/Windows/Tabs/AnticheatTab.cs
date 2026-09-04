using UnityEngine;

namespace TenkaiMenu;

public class AnticheatTab : ITab
{
    public string name => "Anti-Cheat";
    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));
        GUILayout.Label("Anti-Cheat Control", GUIStylePreset.TabSubtitle); Toggle("Enable Anti-Cheat", ref Anticheat.IsEnabled);
        GUILayout.Space(8); GUILayout.Label("Response", GUIStylePreset.TabSubtitle);
        if (DrawGreenButton("Mode: " + Anticheat.DetectionResponseMode, GUILayout.Width(150f), GUILayout.Height(24))) Anticheat.DetectionResponseMode = (Anticheat.ResponseMode)(((int)Anticheat.DetectionResponseMode + 1) % 3);
        GUILayout.Space(8); GUILayout.Label("Identity & Lists", GUIStylePreset.TabSubtitle); Toggle("Detect Invalid Friend Codes", ref Anticheat.DetectInvalidFriendCodes); Toggle("Use Player Ban List", ref Anticheat.UsePlayerBanList); Toggle("Use Name Ban List", ref Anticheat.UseNameBanList); Toggle("Use Word Ban List", ref Anticheat.UseWordBanList); Toggle("Lobby Only", ref Anticheat.BanWordsLobbyOnly);
        GUILayout.Space(8); GUILayout.Label("Validation", GUIStylePreset.TabSubtitle); Toggle("Block Invalid Sabotages", ref Anticheat.BlockInvalidSabotages); Toggle("Hide Kick Reason", ref Anticheat.HideKickReason); Toggle("Detect Player Levels", ref Anticheat.DetectPlayerLevels); Slider("Max Level Threshold", ref Anticheat.MaxLevelThreshold, 100, 10000, "Lv"); Toggle("Auto Kick Levels", ref Anticheat.AutoKickLevels); Slider("Min Level Threshold", ref Anticheat.MinLevelThreshold, 0, 100, "Lv"); Toggle("Block RPCs", ref Anticheat.DetectInvalidRpcs); Toggle("Limit RPC Rate", ref Anticheat.LimitRpcRate); Step("Packet Rate Limit", ref Anticheat.PacketRateLimit, 25, 1000, 1, "PS");
        GUILayout.EndVertical();
    }
    private static void Toggle(string text, ref bool value) { GUILayout.BeginHorizontal(); GUILayout.Label(text); GUILayout.FlexibleSpace(); value = DrawPillToggle(value, text); GUILayout.EndHorizontal(); }
    private static void Step(string text, ref float value, float min, float max, float step, string suffix) { GUILayout.BeginHorizontal(); GUILayout.Label($"{text}: {value:0.##}{suffix}"); if (DrawGreenButton("-", GUILayout.Width(28))) value = Mathf.Clamp(value - step, min, max); if (DrawGreenButton("+", GUILayout.Width(28))) value = Mathf.Clamp(value + step, min, max); GUILayout.EndHorizontal(); }
    private static void Step(string text, ref int value, int min, int max, int step, string suffix) { GUILayout.BeginHorizontal(); GUILayout.Label($"{text}: {value}{suffix}"); if (DrawGreenButton("-", GUILayout.Width(28))) value = Mathf.Clamp(value - step, min, max); if (DrawGreenButton("+", GUILayout.Width(28))) value = Mathf.Clamp(value + step, min, max); GUILayout.EndHorizontal(); }
    private static void Slider(string text, ref int value, int min, int max, string suffix) { GUILayout.BeginHorizontal(); GUILayout.Label($"{text}: {value}{suffix}", GUILayout.Width(190f)); GUILayout.FlexibleSpace(); value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(100f))); GUILayout.EndHorizontal(); }
    private static bool DrawPillToggle(bool value, string text) { Color oldColor = GUI.backgroundColor; GUI.backgroundColor = value ? new Color(1f, 0f, 0.5f, 1f) : new Color(0f, 0.45f, 0.9f, 1f); bool clicked = GUILayout.Button(value ? "ON" : "OFF", GUILayout.Width(55), GUILayout.Height(20)); GUI.backgroundColor = oldColor; return clicked ? !value : value; }
    private static bool DrawGreenButton(string text, params GUILayoutOption[] options) { Color oldColor = GUI.backgroundColor; GUI.backgroundColor = new Color(0.1f, 0.75f, 0.2f, 1f); bool clicked = GUILayout.Button(text, options); GUI.backgroundColor = oldColor; return clicked; }
}