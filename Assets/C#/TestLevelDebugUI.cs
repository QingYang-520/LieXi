using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 测试关卡专用调试按钮。
///
/// 右上角放两个手动按钮：
///   [对话] —— 调出对话框，显示当前的默认文本；点屏幕任意位置隐藏。
///   [刷怪] —— 唤醒场景里那两只小怪（可反复点击，重复刷）。
///
/// 这个 UI 只在测试关卡用，正式关卡不要放它。
/// </summary>
[DisallowMultipleComponent]
public class TestLevelDebugUI : MonoBehaviour
{
    [Header("按钮外观")]
    [SerializeField] private Color buttonColor = new Color(0.12f, 0.16f, 0.28f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.94f, 1f, 1f);
    [SerializeField] private int fontSize = 34;

    [Header("对话框默认文本")]
    [SerializeField] private string defaultSpeaker = "角色名";
    [SerializeField] private string defaultContent = "这里是对话内容，先在编辑器里看看版式。";

    private DialoguePanel panel;
    private bool dialogueShown;
    private float shownAt;
    private bool building;

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        // 对话框显示时：点屏幕任意位置（按钮除外）就隐藏
        if (!dialogueShown)
            return;

        // 刚点开的这一帧忽略，避免按钮点击的同一帧把它关掉
        if (Time.unscaledTime - shownAt < 0.05f)
            return;

        bool clicked = Input.GetMouseButtonDown(0) ||
                       (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!clicked)
            return;

        if (IsPointerOverButton())
            return;

        HideDialogue();
    }

    // ------------------------------------------------------------------ 搭建 UI

    private void BuildUI()
    {
        if (building) return;
        building = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("TestLevelDebugUI: 场景里找不到 Canvas");
            return;
        }

        Font font = DialoguePanel.ResolveFont("heitang");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 右上角容器
        var root = new GameObject("DebugButtons", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.sizeDelta = new Vector2(260f, 200f);
        rootRt.anchoredPosition = new Vector2(-24f, -24f);   // 右上角留边

        MakeButton(root.transform, font, "对 话", OnDialogueClicked, 0f);
        MakeButton(root.transform, font, "刷 怪", OnSpawnClicked, 90f);
    }

    private void MakeButton(Transform parent, Font font, string label,
                            UnityEngine.Events.UnityAction onClick, float y)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<CanvasRenderer>();

        var img = go.AddComponent<Image>();
        img.color = buttonColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(220f, 72f);
        rt.anchoredPosition = new Vector2(0f, -y);   // 从顶部往下排

        // 文字
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        txtGo.AddComponent<CanvasRenderer>();
        var txt = txtGo.AddComponent<Text>();
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.raycastTarget = false;
        txt.text = label;

        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ 按钮

    private void OnDialogueClicked()
    {
        if (dialogueShown)
        {
            HideDialogue();
            return;
        }
        ShowDialogue();
    }

    private void ShowDialogue()
    {
        panel = DialoguePanel.Instance;
        if (panel == null)
            panel = DialoguePanel.Create();
        if (panel == null)
            return;

        // 关掉面板自己的「点屏推进」，改由本脚本统一处理点屏隐藏
        panel.SetWaitForAdvance(false);
        panel.Show(defaultSpeaker, defaultContent);

        dialogueShown = true;
        shownAt = Time.unscaledTime;
    }

    private void HideDialogue()
    {
        if (panel != null)
            panel.Hide();
        dialogueShown = false;
    }

    private void OnSpawnClicked()
    {
        var trigger = FindObjectOfType<MonsterSpawnTrigger>();
        if (trigger != null)
        {
            trigger.ResetToDormant();
            trigger.Spawn();
        }
        else
        {
            MonsterSpawnTrigger.SpawnAll();
        }

        // 满血复活：确保重复点击也能重新刷出满血怪
        MonsterAI[] all = Resources.FindObjectsOfTypeAll<MonsterAI>();
        if (all == null) return;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            Health h = all[i].GetComponent<Health>();
            if (h != null) h.RestoreFull();
        }
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>指针是否落在按钮上（避免点按钮时误关对话框）</summary>
    private static bool IsPointerOverButton()
    {
        var es = EventSystem.current;
        if (es == null)
            return false;

        GameObject hit = null;
        if (Input.touchCount > 0)
        {
            if (es.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                hit = GetTopUIUnderPointer();
        }
        else if (es.IsPointerOverGameObject())
        {
            hit = GetTopUIUnderPointer();
        }

        if (hit == null)
            return false;
        return hit.GetComponentInParent<Selectable>() != null;
    }

    private static GameObject GetTopUIUnderPointer()
    {
        var es = EventSystem.current;
        if (es == null) return null;
        var data = new PointerEventData(es);
        data.position = Input.mousePosition;
        var results = new System.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(data, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }
}
