using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds a ready-wired <see cref="CombatHUD"/> canvas in the open scene. Everything it creates can be rearranged afterwards.</summary>
public static class CombatHUDBuilder
{
    private static readonly Color BarBackground = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.85f);

    [MenuItem("GameObject/Combat/Combat HUD", false, 10)]
    private static void Create()
    {
        var root = new GameObject("CombatHUD", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform t = root.transform;
        var topCenter = new Vector2(0.5f, 1f);
        var bottomLeft = Vector2.zero;

        CombatBar enemyHealth = CreateBar("EnemyHealthBar", t, topCenter, new Vector2(0f, -40f), new Vector2(900f, 32f),
            new Color(0.85f, 0.15f, 0.15f), "ENEMY", true);
        TMP_Text enemyState = CreateText("EnemyStateText", t, topCenter, new Vector2(0f, -4f), new Vector2(900f, 34f),
            28f, TextAlignmentOptions.Bottom);
        enemyState.fontStyle = FontStyles.Bold;
        CombatBar stagger = CreateBar("StaggerBar", t, topCenter, new Vector2(0f, -80f), new Vector2(900f, 10f),
            new Color(0.3f, 1f, 0.3f), null, false);

        TMP_Text telegraph = CreateText("TelegraphLabel", t, topCenter, new Vector2(0f, -110f), new Vector2(1000f, 140f),
            48f, TextAlignmentOptions.Top);
        TMP_Text feedback = CreateText("FeedbackText", t, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 140f),
            80f, TextAlignmentOptions.Center);
        feedback.fontStyle = FontStyles.Bold;

        TMP_Text playerState = CreateText("PlayerStateText", t, bottomLeft, new Vector2(40f, 148f), new Vector2(480f, 44f),
            32f, TextAlignmentOptions.BottomLeft);
        playerState.fontStyle = FontStyles.Bold;
        CombatBar health = CreateBar("HealthBar", t, bottomLeft, new Vector2(40f, 112f), new Vector2(480f, 30f),
            new Color(0.3f, 0.85f, 0.3f), "HP", true);
        CombatBar shield = CreateBar("ShieldBar", t, bottomLeft, new Vector2(40f, 76f), new Vector2(480f, 24f),
            new Color(0.3f, 0.6f, 1f), "SHIELD", true);
        CombatBar stamina = CreateBar("StaminaBar", t, bottomLeft, new Vector2(40f, 40f), new Vector2(480f, 24f),
            new Color(1f, 0.8f, 0.2f), "STAMINA", true);

        var hud = root.AddComponent<CombatHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerCombatController>();
        so.FindProperty("enemy").objectReferenceValue = Object.FindFirstObjectByType<EnemyCombatController>();
        so.FindProperty("healthBar").objectReferenceValue = health;
        so.FindProperty("shieldBar").objectReferenceValue = shield;
        so.FindProperty("staminaBar").objectReferenceValue = stamina;
        so.FindProperty("enemyHealthBar").objectReferenceValue = enemyHealth;
        so.FindProperty("staggerBar").objectReferenceValue = stagger;
        so.FindProperty("feedbackText").objectReferenceValue = feedback;
        so.FindProperty("playerStateText").objectReferenceValue = playerState;
        so.FindProperty("enemyStateText").objectReferenceValue = enemyState;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "Create Combat HUD");

        var telegraphDebug = Object.FindFirstObjectByType<CombatTelegraphDebug>();
        if (telegraphDebug != null)
        {
            var debugSo = new SerializedObject(telegraphDebug);
            debugSo.FindProperty("label").objectReferenceValue = telegraph;
            debugSo.ApplyModifiedProperties();
        }

        Selection.activeGameObject = root;
    }

    private static CombatBar CreateBar(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
        Color fillColor, string label, bool withTrail)
    {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = BarBackground;
        background.raycastTarget = false;

        RectTransform trail = withTrail ? CreateFill("Trail", rect, TrailColor) : null;
        RectTransform fill = CreateFill("Fill", rect, fillColor);

        TMP_Text valueText = null;
        if (label != null)
        {
            float fontSize = size.y * 0.65f;
            CreateInsetText("Label", rect, label, fontSize, TextAlignmentOptions.Left);
            valueText = CreateInsetText("Value", rect, string.Empty, fontSize, TextAlignmentOptions.Right);
        }

        var bar = rect.gameObject.AddComponent<CombatBar>();
        var so = new SerializedObject(bar);
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("trail").objectReferenceValue = trail;
        so.FindProperty("valueText").objectReferenceValue = valueText;
        so.ApplyModifiedPropertiesWithoutUndo();

        return bar;
    }

    private static RectTransform CreateFill(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateInsetText(string name, RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);

        TMP_Text tmp = AddText(go, text, fontSize, alignment);
        tmp.color = Color.white;
        return tmp;
    }

    private static TMP_Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
        float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        return AddText(rect.gameObject, string.Empty, fontSize, alignment);
    }

    private static TMP_Text AddText(GameObject go, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>A rect anchored and pivoted at <paramref name="anchor"/>, so <paramref name="position"/> is an offset from that screen point.</summary>
    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
