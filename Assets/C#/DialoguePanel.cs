using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话框 UI（底部剧情对话）。
///
/// 版式（对应 Assets/Art/UI/mmexport1792214838154.png）：
/// ┌──────────────────────────────────────────────────────────┐
/// │ ┌────────┐ │  名字（比较大）                              │
/// │ │ 头像框 │ │  对话内容（名字字号 - 3）                    │
/// │ └────────┘ │                                              │
/// └──────────────────────────────────────────────────────────┘
/// - 左边：头像位（留空，之后塞图）
/// - 右边：上=名字，下=对话内容
/// - 名字比对话内容大 3 号
///
/// 用法：
///   DialoguePanel.Create()                      // 运行时自动创建一个
///   DialoguePanel.Instance.Show("青阳", "你好");  // 显示一句
///   DialoguePanel.Instance.Hide();              // 收起
///   DialoguePanel.Instance.Next();              // 下一句（多句队列）
///
/// 所有内容都可在 Inspector 里改，也可以通过 SetSpeaker/SetContent 单独设置。
/// </summary>
[DisallowMultipleComponent]
public class DialoguePanel : MonoBehaviour
{
    [Header("尺寸（画布单位，参考 1920x1080）")]
    [Tooltip("对话框总宽度")]
    [SerializeField] private float panelWidth = 1200f;
    [Tooltip("对话框总高度（宽度按 4:1 左右比较顺眼）")]
    [SerializeField] private float panelHeight = 300f;
    [Tooltip("距离屏幕底部的留白（越小越靠下）")]
    [SerializeField] private float bottomMargin = 20f;

    [Header("头像区")]
    // 头像区宽度不单独配置：它严格跟随底图左块的宽度（原图 x=8..183，共 176/1024），
    // 这样换底图或改 panelWidth 时，头像区永远和浅色块严丝合缝。
    [Tooltip("头像图（留空则是个空白块）")]
    [SerializeField] private Sprite avatarSprite;

    [Header("字号（名字比对话内容大 3）")]
    [SerializeField] private int contentFontSize = 32;
    [SerializeField] private int nameFontOffset = 3;

    [Header("颜色")]
    [SerializeField] private Color nameColor = new Color(0.10f, 0.16f, 0.28f, 1f);
    [SerializeField] private Color contentColor = new Color(0.13f, 0.15f, 0.20f, 1f);

    [Header("字体（留空自动用 heitang.ttf）")]
    [SerializeField] private Font font;

    [Header("资源")]
    [Tooltip("对话框底图 Sprite。留空则运行时自动从 Assets/Art/UI 里找 mmexport1792214838154")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("自动查找字体时用的文件名（不含扩展名）")]
    [SerializeField] private string panelFontName = "heitang";

    [Header("打字机效果")]
    [Tooltip("每秒显示多少个字，0 表示不使用打字机直接整句显示")]
    [SerializeField] private float charsPerSecond = 30f;
    [Tooltip("显示完一句后等待玩家点击/按键继续")]
    [SerializeField] private bool waitForAdvance = true;

    [Header("初始显示（方便在编辑器里直接看版式）")]
    [Tooltip("勾选：进场就显示下面这段占位文字，方便查看对话框效果\n取消勾选：进场即隐藏，等剧情调用 Show()")]
    [SerializeField] private bool startVisible = true;
    [SerializeField] private string placeholderName = "角色名";
    [Tooltip("占位对话内容，填 \n 可换行")]
    [SerializeField] private string placeholderContent = "这里是对话内容。";

    private Canvas canvas;
    private RectTransform panelRect;
    private Image panelImage;
    private RectTransform avatarRect;
    private Image avatarImage;
    private Text nameText;
    private Text contentText;

    private float panelW, panelH;
    private float border, avatarW, gapW, textAreaX, textAreaW;
    private bool typing;
    private Coroutine typingCo;

    private string currentName = "";
    private string currentContent = "";
    private string[] queue;
    private int queueIndex;

    public static DialoguePanel Instance { get; private set; }

    /// <summary>对话框当前是否可见</summary>
    public bool IsVisible { get { return gameObject.activeSelf; } }

    // ---------------------------------------------------------------- 构建

    /// <summary>运行时创建一个对话框（已存在则直接返回）</summary>
    public static DialoguePanel Create()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("DialoguePanel");
        var panel = go.AddComponent<DialoguePanel>();
        return panel;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildIfNeeded();

        // 按 Inspector 的开关决定进场时是否显示：
        // 勾上就保留场景里的激活状态（方便在编辑器里直接看到版式），
        // 取消勾选则进场即隐藏，等剧情调用 Show()。
        if (!startVisible)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);

        if (startVisible)
            Show(placeholderName, placeholderContent);
    }

    private void BuildIfNeeded()
    {
        if (panelRect != null)
            return;

        // --- 优先收编场景里已经摆好的子节点（Background/AvatarBlock/NameText/ContentText）---
        // 场景里没有时才走运行时自建的兜底流程
        Transform bgT = transform.Find("Background");
        Transform avT = transform.Find("AvatarBlock");
        Transform nmT = transform.Find("NameText");
        Transform ctT = transform.Find("ContentText");

        bool hasSceneNodes = bgT != null && nmT != null && ctT != null;
        if (hasSceneNodes)
        {
            panelRect = bgT.GetComponent<RectTransform>();
            panelImage = bgT.GetComponent<Image>();
            avatarRect = avT != null ? avT.GetComponent<RectTransform>() : null;
            avatarImage = avT != null ? avT.GetComponent<Image>() : null;
            nameText = nmT.GetComponent<Text>();
            contentText = ctT.GetComponent<Text>();

            // 字体兜底：场景里没配或配置丢失时自动补
            if (font == null)
                font = ResolveFont(panelFontName);
            if (nameText != null && nameText.font == null)
                nameText.font = font;
            if (contentText != null && contentText.font == null)
                contentText.font = font;

            // 底图兜底
            if (panelSprite == null && panelImage != null && panelImage.sprite == null)
                panelSprite = ResolveBackgroundSprite("mmexport1792214838154");
            if (panelImage != null && panelImage.sprite == null && panelSprite != null)
                panelImage.sprite = panelSprite;

            canvas = GetComponentInParent<Canvas>();
            return;
        }

        // --- 兜底：运行时自建（场景里没摆好的情况）---
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("DialogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = cgo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 1f;
        }

        if (transform.parent == null)
            transform.SetParent(canvas.transform, false);

        // --- 字体 ---
        if (font == null)
            font = ResolveFont(panelFontName);

        // --- 背景底图 ---
        if (panelSprite == null)
            panelSprite = ResolveBackgroundSprite("mmexport1792214838154");

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        panelRect = bg.GetComponent<RectTransform>();
        panelImage = bg.GetComponent<Image>();
        panelImage.sprite = panelSprite;
        if (panelSprite != null)
        {
            // 原图四边各有 8px 黑框，用九宫格拉伸，边框不会变形
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 1f;
        }
        else
        {
            // 找不到底图时用纯色兜底，至少能看到框
            panelImage.color = new Color(0.85f, 0.88f, 0.92f, 0.95f);
        }
        panelImage.raycastTarget = false;

        // --- 头像区 ---
        var av = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        av.transform.SetParent(transform, false);
        avatarRect = av.GetComponent<RectTransform>();
        avatarImage = av.GetComponent<Image>();
        avatarImage.sprite = avatarSprite;
        avatarImage.color = avatarSprite != null ? Color.white : new Color(1, 1, 1, 0); // 没有头像就透明
        avatarImage.raycastTarget = false;
        avatarImage.preserveAspect = true;

        // --- 名字 ---
        var nm = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        nm.transform.SetParent(transform, false);
        nameText = nm.GetComponent<Text>();
        nameText.font = font;
        nameText.fontSize = contentFontSize + nameFontOffset;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = nameColor;
        nameText.alignment = TextAnchor.UpperLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.raycastTarget = false;

        // --- 对话内容 ---
        var ct = new GameObject("ContentText", typeof(RectTransform), typeof(Text));
        ct.transform.SetParent(transform, false);
        contentText = ct.GetComponent<Text>();
        contentText.font = font;
        contentText.fontSize = contentFontSize;
        contentText.color = contentColor;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Truncate;
        contentText.lineSpacing = 1.15f;
        contentText.raycastTarget = false;

        ApplyLayout();
    }

    // 这些是可以在 Inspector 里指定的资源（已上移到字段区）

    /// <summary>
    /// 找字体。优先用 Inspector 里指定的；其次从 Resources 里找同名资源；
    /// 最后退回 Unity 内置字体。编辑器下额外支持按名字在 Assets/Art/fonts 里找。
    /// 本项目的其它 UI（如调试按钮）也会复用这个方法解析中文字体。
    /// </summary>
    public static Font ResolveFont(string hint)
    {
        // 1) Resources 里找（打包后也有效）
        if (!string.IsNullOrEmpty(hint))
        {
            Font r = Resources.Load<Font>(hint);
            if (r != null)
                return r;
            // 也试一下子目录
            foreach (string sub in new[] { "Fonts/", "fonts/", "Art/fonts/" })
            {
                r = Resources.Load<Font>(sub + hint);
                if (r != null)
                    return r;
            }
        }

#if UNITY_EDITOR
        // 2) 编辑器下直接按资源名找（不用把字体挪进 Resources）
        if (!string.IsNullOrEmpty(hint))
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Font " + hint,
                new[] { "Assets/Art/fonts", "Assets" });
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Font f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(path);
                if (f != null)
                    return f;
            }
        }
#endif

        // 3) 兜底
        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallback == null)
            fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return fallback;
    }

    /// <summary>
    /// 按文件名找底图 Sprite。
    /// 编辑器下从 Assets/Art/UI 自动找；打包后依赖 Inspector 里预先拖好的 panelSprite
    /// （或把它放进 Resources 目录后用 Resources.Load）。
    /// </summary>
    private static Sprite ResolveBackgroundSprite(string assetName)
    {
        // 1) Resources 优先（打包后可用的正路）
        Sprite r = Resources.Load<Sprite>(assetName);
        if (r != null)
            return r;

#if UNITY_EDITOR
        // 2) 编辑器下按资源名自动找
        if (!string.IsNullOrEmpty(assetName))
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(assetName + " t:Texture2D",
                new[] { "Assets/Art/UI", "Assets" });
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null)
                    return s;
            }
        }
#endif
        return null;
    }


    private void ApplyLayout()
    {
        if (panelRect == null)
            return;

        panelW = panelWidth;
        panelH = panelHeight;

        // 底图实测（1024x256，黑框与中缝已做成透明）：
        //   左块 x=8..183 (w176) | 中缝 x=184..199 (w16, 透明) | 右块 x=200..1015 (w816)
        // 九宫格 spriteBorder L=200 把"左块+中缝"整体固定，只有右块弹性拉伸，
        // 所以这里的比例也按原图像素来换算。
        const float SRC_W = 1024f;
        const float SRC_H = 256f;

        border = panelW * (8f / SRC_W);            // 左侧留白，对应原图 x=0..7 的透明边
        avatarW = panelW * (176f / SRC_W);         // 左块宽
        gapW = panelW * (16f / SRC_W);             // 中缝宽（透明，但布局要留出来）

        // 面板：底部居中
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(panelW, panelH);
        panelRect.anchoredPosition = new Vector2(0f, bottomMargin);

        // 头像框：强制正方形 —— 边长取左块的宽度，并在左块内垂直居中。
        // 说明：底图左块是 176x240 的竖矩形（宽高比 0.733），装不下同宽的正方形，
        // 所以正方形上下各留一点空白，视觉上仍然是标准正方形且居中。
        float avatarSize = avatarW;
        float avatarTop = (panelH - avatarSize) * 0.5f;   // 垂直居中

        avatarRect.anchorMin = new Vector2(0f, 0.5f);
        avatarRect.anchorMax = new Vector2(0f, 0.5f);
        avatarRect.pivot = new Vector2(0f, 0.5f);
        avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);
        avatarRect.anchoredPosition = new Vector2(border, 0f);

        // 文字区起点 = 左边留白 + 左块 + 中缝，即原图 x=200 处
        textAreaX = border + avatarW + gapW;
        textAreaW = panelW - textAreaX - border;

        // 上下留白按原图比例
        float padV = panelH * (24f / SRC_H);

        // 名字：右上区域，靠上
        float nameH = (contentFontSize + nameFontOffset) * 2.2f;
        RectTransform nameRt = nameText.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.offsetMin = new Vector2(textAreaX, -(padV + nameH));
        nameRt.offsetMax = new Vector2(-border, -padV);

        // 内容：名字下方到底边
        RectTransform ctRt = contentText.rectTransform;
        ctRt.anchorMin = new Vector2(0f, 0f);
        ctRt.anchorMax = new Vector2(1f, 1f);
        ctRt.pivot = new Vector2(0f, 1f);
        ctRt.offsetMin = new Vector2(textAreaX, padV);
        ctRt.offsetMax = new Vector2(-border, -(padV + nameH + 6f));
    }

    // ---------------------------------------------------------------- 显示

    /// <summary>显示一句对话</summary>
    public void Show(string speaker, string content)
    {
        gameObject.SetActive(true);
        BuildIfNeeded();
        ApplyLayout();

        currentName = speaker ?? "";
        currentContent = content ?? "";
        nameText.text = currentName;

        if (typingCo != null)
            StopCoroutine(typingCo);

        if (charsPerSecond > 0f && !string.IsNullOrEmpty(currentContent))
            typingCo = StartCoroutine(TypeRoutine(currentContent));
        else
        {
            contentText.text = currentContent;
            typing = false;
        }
    }

    /// <summary>显示一串对话，每次 Next() 推进一句</summary>
    public void ShowSequence(string[] lines)
    {
        queue = lines;
        queueIndex = 0;
        if (queue != null && queue.Length > 0)
            ParseAndShow(queue[0]);
    }

    // 每句格式："名字|内容"
    private void ParseAndShow(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            Show("", "");
            return;
        }
        int bar = line.IndexOf('|');
        if (bar >= 0)
            Show(line.Substring(0, bar), line.Substring(bar + 1));
        else
            Show(currentName, line);
    }

    private IEnumerator TypeRoutine(string full)
    {
        typing = true;
        contentText.text = "";
        int shown = 0;
        float acc = 0f;
        while (shown < full.Length)
        {
            acc += Time.deltaTime * charsPerSecond;
            int add = Mathf.FloorToInt(acc);
            if (add > 0)
            {
                acc -= add;
                shown = Mathf.Min(full.Length, shown + add);
                contentText.text = full.Substring(0, shown);
            }
            yield return null;
        }
        contentText.text = full;
        typing = false;
        typingCo = null;
    }

    /// <summary>下一句：打字中则立刻显示完整，否则推进队列</summary>
    public void Next()
    {
        if (typing)
        {
            if (typingCo != null)
                StopCoroutine(typingCo);
            typingCo = null;
            typing = false;
            contentText.text = currentContent;
            return;
        }

        if (queue != null && queueIndex + 1 < queue.Length)
        {
            queueIndex++;
            ParseAndShow(queue[queueIndex]);
            return;
        }

        Hide();
    }

    public void Hide()
    {
        if (typingCo != null)
        {
            StopCoroutine(typingCo);
            typingCo = null;
        }
        typing = false;
        queue = null;
        queueIndex = 0;
        gameObject.SetActive(false);
    }

    /// <summary>是否正在逐字显示中</summary>
    public bool IsTyping { get { return typing; } }

    /// <summary>立刻把当前这句完整显示出来（跳过打字机）</summary>
    public void CompleteTyping()
    {
        if (typingCo != null)
        {
            StopCoroutine(typingCo);
            typingCo = null;
        }
        typing = false;
        if (contentText != null)
            contentText.text = currentContent;
    }

    /// <summary>
    /// 是否由面板自己处理点击翻页。
    /// 外部剧情系统接管推进时传 false，面板就只管显示。
    /// </summary>
    public void SetWaitForAdvance(bool value)
    {
        waitForAdvance = value;
    }

    public void SetSpeaker(string s)
    {
        currentName = s ?? "";
        if (nameText != null)
            nameText.text = currentName;
    }

    public void SetContent(string c)
    {
        currentContent = c ?? "";
        if (contentText != null)
            contentText.text = currentContent;
    }

    /// <summary>设置头像图（之后你有了头像直接传进来）</summary>
    public void SetAvatar(Sprite s)
    {
        avatarSprite = s;
        if (avatarImage != null)
        {
            avatarImage.sprite = s;
            avatarImage.color = s != null ? Color.white : new Color(1, 1, 1, 0);
        }
    }

    /// <summary>运行时改字号：名字始终 = 内容 + 3</summary>
    public void SetFontSize(int contentSize)
    {
        contentFontSize = Mathf.Max(1, contentSize);
        if (contentText != null) contentText.fontSize = contentFontSize;
        if (nameText != null) nameText.fontSize = contentFontSize + nameFontOffset;
        ApplyLayout();
    }

    private void Update()
    {
        if (!IsVisible || !waitForAdvance)
            return;

        // 键盘：空格 / 回车 / ESC 之外都不管事
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            Next();
            return;
        }

        // 鼠标/触屏：点屏幕任意位置都推进（包括点在对话框自身上）
        if (Input.GetMouseButtonDown(0))
        {
            // 唯一例外：点在真正的按钮上（设置按钮等）不推进，避免误触开关面板
            if (IsPointerOverButton())
                return;
            Next();
            return;
        }

        // 触屏兜底：部分设备触摸不走 GetMouseButtonDown
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (IsPointerOverButton())
                return;
            Next();
        }
    }

    /// <summary>
    /// 指针是否落在「按钮」类控件上（静态版，供外部剧情播放器复用）。
    /// 注意：对话框自己的 Image/Text 不算按钮，点在上面应该照常推进剧情。
    /// </summary>
    public static bool IsPointerOverButtonStatic()
    {
        return IsPointerOverButton();
    }

    /// <summary>
    /// 指针是否落在「按钮」类控件上。
    /// 注意：对话框自己的 Image/Text 不算按钮，点在上面应该照常推进剧情。
    /// </summary>
    private static bool IsPointerOverButton()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
            return false;

        // 触屏优先用 touch 判定，鼠标用普通判定
        GameObject hit = null;
        if (Input.touchCount > 0)
            hit = es.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                ? GetTopUIUnderPointer()
                : null;
        else if (es.IsPointerOverGameObject())
            hit = GetTopUIUnderPointer();

        if (hit == null)
            return false;

        // 按钮 = 挂了 Selectable（Button/Toggle/Slider）或实现了点击接口的组件
        return hit.GetComponentInParent<UnityEngine.UI.Selectable>() != null;
    }

    /// <summary>取鼠标位置下最上层的 UI 对象</summary>
    private static GameObject GetTopUIUnderPointer()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
            return null;
        var data = new UnityEngine.EventSystems.PointerEventData(es);
        data.position = Input.mousePosition;
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(data, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器预览：不运行游戏时也在 Scene 视图里画出对话框的占位框，
    /// 方便你直接看到它在屏幕上的位置和大小。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
            return;

        // 找到场景里的 Canvas，把画布坐标换算到世界坐标
        Canvas c = GetComponentInParent<Canvas>();
        if (c == null)
            c = FindObjectOfType<Canvas>();
        if (c == null)
            return;

        float scale = 1f;
        float canvasW = panelWidth;
        float canvasH = panelHeight;

        // Canvas 若是铺满屏幕的（默认），用它的 RectTransform 尺寸判断缩放
        RectTransform crt = c.transform as RectTransform;
        if (crt != null && crt.rect.width > 1f)
        {
            // 参考分辨率 1920 宽 -> 屏幕实际宽度
            float screenW = crt.rect.width;
            scale = screenW / 1920f;
            canvasW = panelWidth * scale;
            canvasH = panelHeight * scale;
        }

        Vector3 center = c.transform.position;
        // 面板：底部居中
        float cy = center.y - canvasH * 0.5f;
        float cx = center.x;

        Gizmos.color = new Color(0.25f, 0.55f, 1f, 0.9f);
        Vector3 p0 = new Vector3(cx - canvasW * 0.5f, cy, 0f);
        Vector3 p1 = new Vector3(cx + canvasW * 0.5f, cy + canvasH, 0f);
        Gizmos.DrawWireCube((p0 + p1) * 0.5f, new Vector3(canvasW, canvasH, 0.01f));

        // 头像块大致位置
        float aw = panelWidth * (176f / 1024f) * scale;
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Vector3 a0 = new Vector3(p0.x + canvasW * (8f / 1024f), cy + canvasH * (24f / 256f), 0f);
        Gizmos.DrawWireCube(a0 + new Vector3(aw * 0.5f, canvasH * (208f / 256f) * 0.5f, 0f),
                            new Vector3(aw, canvasH * (208f / 256f), 0.01f));
    }
#endif
}
