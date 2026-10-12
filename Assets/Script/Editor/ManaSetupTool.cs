using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 열린 씬에 마나 시스템을 한 번에 설치하는 에디터 도구 (여러 번 실행해도 중복 생성 없음).
/// - Player(PlayerStats가 있는 오브젝트)에 PlayerMana 추가 + PlayerStats/PlayerAttack 참조 연결
/// - HUD Canvas(PlayerHPBar가 있는 Canvas) 좌하단에 PlayerManaBar 생성 + ManaUIController 연결
/// - fireball.asset의 manaCost를 5로 설정
/// </summary>
public static class ManaSetupTool
{
    private const string FireballPath = "Assets/Magic/fireball.asset";
    private const int FireballManaCost = 5;
    private const string BarName = "PlayerManaBar";
    private const float ScreenPadding = 20f;
    private const float FillInset = 3f;
    private static readonly Vector2 BarSize = new Vector2(300f, 40f);
    private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
    private static readonly Color FillColor = new Color(0.2f, 0.45f, 1f, 1f);

    [MenuItem("Tools/Magiduck/Setup Mana (UI + Components)")]
    private static void Setup()
    {
        PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include);
        if (stats == null)
        {
            Debug.LogError("[ManaSetup] 씬에서 PlayerStats를 찾지 못했습니다. Player가 있는 씬(my.unity)을 열고 다시 실행하세요.");
            return;
        }

        GameObject player = stats.gameObject;
        PlayerMana mana = player.GetComponent<PlayerMana>();
        if (mana == null)
            mana = Undo.AddComponent<PlayerMana>(player);

        SetReference(stats, "mana", mana);

        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        if (attack == null)
            attack = Object.FindAnyObjectByType<PlayerAttack>(FindObjectsInactive.Include);
        if (attack != null)
            SetReference(attack, "playerMana", mana);
        else
            Debug.LogWarning("[ManaSetup] PlayerAttack을 찾지 못해 마나 비용 연결을 건너뜁니다.");

        Canvas canvas = FindHudCanvas();
        if (canvas == null)
        {
            Debug.LogError("[ManaSetup] HUD Canvas(PlayerHPBar의 Canvas)를 찾지 못했습니다.");
            return;
        }

        ManaUIController ui = BuildOrReuseBar(canvas, mana);
        SetFireballCost();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = ui.gameObject;
        Debug.Log($"[ManaSetup] 완료: '{player.name}'에 PlayerMana, '{canvas.name}'에 {BarName} 설치. 씬을 저장하세요 (Ctrl+S).", ui);
    }

    private static Canvas FindHudCanvas()
    {
        PlayerHPBarUI hpBar = Object.FindAnyObjectByType<PlayerHPBarUI>(FindObjectsInactive.Include);
        if (hpBar != null)
        {
            Canvas canvas = hpBar.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                return canvas.rootCanvas;
        }

        PlayerRoot root = Object.FindAnyObjectByType<PlayerRoot>(FindObjectsInactive.Include);
        return root != null ? root.GetComponentInChildren<Canvas>(true) : null;
    }

    private static ManaUIController BuildOrReuseBar(Canvas canvas, PlayerMana mana)
    {
        Transform existing = canvas.transform.Find(BarName);
        if (existing != null && existing.TryGetComponent(out ManaUIController existingUi))
        {
            SetReference(existingUi, "mana", mana);
            return existingUi;
        }

        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = new GameObject(BarName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Mana Bar");
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = new Vector2(ScreenPadding, ScreenPadding);
        rootRect.sizeDelta = BarSize;

        Image background = CreateChild<Image>(root.transform, "Background", 0f);
        background.sprite = sprite;
        background.type = Image.Type.Sliced;
        background.color = BackgroundColor;
        background.raycastTarget = false;

        Image fill = CreateChild<Image>(root.transform, "Fill", FillInset);
        fill.sprite = sprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.color = FillColor;
        fill.raycastTarget = false;

        TextMeshProUGUI text = CreateChild<TextMeshProUGUI>(root.transform, "ManaText", 0f);
        text.text = "100/100";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 22f;
        text.color = Color.white;
        text.raycastTarget = false;

        ManaUIController ui = Undo.AddComponent<ManaUIController>(root);
        SetReference(ui, "mana", mana);
        SetReference(ui, "fillImage", fill);
        SetReference(ui, "manaText", text);
        return ui;
    }

    private static T CreateChild<T>(Transform parent, string name, float inset) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Mana Bar");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);

        return go.AddComponent<T>();
    }

    private static void SetFireballCost()
    {
        MagicData fireball = AssetDatabase.LoadAssetAtPath<MagicData>(FireballPath);
        if (fireball == null)
        {
            Debug.LogWarning($"[ManaSetup] {FireballPath}를 찾지 못해 manaCost 설정을 건너뜁니다.");
            return;
        }

        var so = new SerializedObject(fireball);
        so.FindProperty("manaCost").intValue = FireballManaCost;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(fireball);

        foreach (string guid in AssetDatabase.FindAssets("t:MagicData"))
        {
            MagicData magic = AssetDatabase.LoadAssetAtPath<MagicData>(AssetDatabase.GUIDToAssetPath(guid));
            if (magic != null && magic.ManaCost == 0)
                Debug.Log($"[ManaSetup] manaCost = 0 (무료): {AssetDatabase.GUIDToAssetPath(guid)}", magic);
        }
    }

    private static void SetReference(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"[ManaSetup] {target.GetType().Name}에 '{fieldName}' 필드가 없습니다.", target);
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
