using System.Collections;
using UnityEngine;

/// <summary>
/// 剧情播放器（序章 / 任意关卡通用）。
///
/// 职责很单纯：
///   1. 从 Assets/Plot/<plotName>.txt 读取剧情
///   2. 逐句显示到 DialoguePanel（带打字机效果）
///   3. 玩家点击屏幕任意位置 / 点对话框 → 跳到下一句
///   4. 播完最后一句 → 自动收起对话框
///
/// 剧情文本完全不写死在代码里，改 txt 就够了。
/// </summary>
[DisallowMultipleComponent]
public class StoryPlayer : MonoBehaviour
{
    [Header("剧情来源")]
    [Tooltip("Assets/Plot 下的文件名（不含 .txt），比如「序章」")]
    [SerializeField] private string plotName = "序章";

    [Header("播放设置")]
    [Tooltip("进场自动播放")]
    [SerializeField] private bool autoPlay = true;
    [Tooltip("开场前等待几秒（给镜头/淡入留时间）")]
    [SerializeField] private float delayBeforeStart = 0.3f;
    [Tooltip("播完后是否自动收起对话框")]
    [SerializeField] private bool hideWhenDone = true;

    [Header("联动的怪（播完后放出）")]
    [Tooltip("勾选：剧情播完自动唤醒 MonsterSpawnTrigger 放怪")]
    [SerializeField] private bool spawnMonstersAfterStory = false;

    private DialoguePanel panel;
    private bool playing;
    private PlotReader.PlotLine[] lines;
    private int index;

    /// <summary>是否正在播放</summary>
    public bool IsPlaying { get { return playing; } }

    private void Start()
    {
        if (!autoPlay)
            return;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (delayBeforeStart > 0f)
            yield return new WaitForSeconds(delayBeforeStart);
        PlayPlot(plotName);
    }

    /// <summary>播放 Assets/Plot 下的某个剧情文件</summary>
    public void PlayPlot(string name)
    {
        PlotReader.PlotLine[] parsed = PlotReader.Read(name);
        if (parsed == null || parsed.Length == 0)
        {
            Debug.LogWarning("StoryPlayer: 剧情为空或读取失败 -> " + name);
            return;
        }
        Play(parsed);
    }

    /// <summary>播放已解析好的台词列表</summary>
    public void Play(PlotReader.PlotLine[] script)
    {
        if (script == null || script.Length == 0)
            return;

        panel = DialoguePanel.Instance;
        if (panel == null)
            panel = DialoguePanel.Create();
        if (panel == null)
            return;

        // 由播放器接管推进，面板只负责显示
        panel.SetWaitForAdvance(false);

        lines = script;
        index = 0;
        playing = true;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (lines == null || index < 0 || index >= lines.Length)
        {
            Finish();
            return;
        }

        PlotReader.PlotLine line = lines[index];
        panel.Show(line.speaker, line.content);
    }

    private void Update()
    {
        if (!playing || panel == null)
            return;

        bool advance = false;

        if (Input.GetMouseButtonDown(0))
            advance = !DialoguePanel.IsPointerOverButtonStatic();
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            advance = !DialoguePanel.IsPointerOverButtonStatic();
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            advance = true;

        if (advance)
            Advance();
    }

    /// <summary>推进：打字中先显示完整句，否则下一句</summary>
    public void Advance()
    {
        if (!playing || panel == null)
            return;

        if (panel.IsTyping)
        {
            panel.CompleteTyping();
            return;
        }

        index++;
        ShowCurrent();
    }

    private void Finish()
    {
        if (!playing)
            return;
        playing = false;

        if (hideWhenDone && panel != null)
            panel.Hide();

        if (spawnMonstersAfterStory)
        {
            var trigger = FindObjectOfType<MonsterSpawnTrigger>();
            if (trigger != null)
                trigger.Spawn();
            else
                MonsterSpawnTrigger.SpawnAll();
        }
    }
}
