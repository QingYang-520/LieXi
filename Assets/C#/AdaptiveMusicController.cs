using System.Collections;
using UnityEngine;

/// <summary>
/// 动态战斗音乐：根据场上是否还有活着的怪物，在简单版和困难版之间无缝切换。
///
/// 做法（PvZ 式无缝切换）：
///   两条音轨从进场就【同时播放】，简单版出声、困难版静音（volume=0）。
///   切换时【只调音量】，全程不调 Play()/Stop()。
///
/// 这样做的好处：
///   1. 困难版一直在同步播着，淡入时是"接着放"而不是"从头放"。
///   2. 切换瞬间没有 Play() 瞬态，不会窜出那段错乱的快速音。
/// </summary>
[DisallowMultipleComponent]
public class AdaptiveMusicController : MonoBehaviour
{
    [Header("音乐片段")]
    [Tooltip("场上没有怪物时播放（简单版）")]
    [SerializeField] private AudioClip simpleClip;
    [Tooltip("场上有怪物时播放（困难版）")]
    [SerializeField] private AudioClip hardClip;

    [Header("切换设置")]
    [Tooltip("交叉淡化时长（秒）")]
    [SerializeField] private float fadeDuration = 1.2f;
    [Tooltip("检测间隔（秒）")]
    [SerializeField] private float checkInterval = 0.5f;
    [Tooltip("音乐音量（0~1）")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;

    [Header("调试")]
    [Tooltip("在 Console 打印播放状态")]
    [SerializeField] private bool verboseLog = true;

    private AudioSource simpleSource;
    private AudioSource hardSource;

    private bool inHardMode;
    private bool ready;
    private Coroutine fadeCo;
    private float nextCheck;

    private void Awake()
    {
        simpleSource = gameObject.AddComponent<AudioSource>();
        hardSource = gameObject.AddComponent<AudioSource>();
        SetupSource(simpleSource, simpleClip, "简单版");
        SetupSource(hardSource, hardClip, "困难版");

        // mp3 默认 preloadAudioData=0，主动触发加载
        if (simpleClip != null) simpleClip.LoadAudioData();
        if (hardClip != null) hardClip.LoadAudioData();
    }

    private void SetupSource(AudioSource src, AudioClip clip, string label)
    {
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0f;          // 先都静音，等加载完再统一开声
        src.priority = 64;
        if (clip == null)
            Debug.LogWarning("[动态音乐] " + label + " 的 AudioClip 没拖进来，这条不会响！");
    }

    private void Start()
    {
        StartCoroutine(PreloadThenStart());
    }

    private IEnumerator PreloadThenStart()
    {
        const float timeout = 5f;
        float waited = 0f;

        // 等两首都加载到 Loaded（括号包好，避免 && / || 优先级坑）
        while (waited < timeout && !BothLoaded())
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!BothLoaded())
            Debug.LogWarning("[动态音乐] 音频加载超时。simple=" + ClipState(simpleClip) +
                             " hard=" + ClipState(hardClip));

        // 核心：两条音轨【同时起播】，之后只调音量、不再 Play/Stop
        simpleSource.timeSamples = 0;
        hardSource.timeSamples = 0;
        simpleSource.Play();
        hardSource.Play();

        inHardMode = AnyMonsterAlive();
        ApplyVolume(inHardMode);   // 直接设好初始音量，不淡化

        ready = true;

        if (verboseLog)
            Debug.Log("[动态音乐] 双轨同步播放中，当前 -> " + (inHardMode ? "困难版" : "简单版") +
                      "  simple=" + ClipState(simpleClip) + "  hard=" + ClipState(hardClip));
    }

    private static string ClipState(AudioClip c)
    {
        return c == null ? "null" : c.loadState.ToString();
    }

    private bool BothLoaded()
    {
        bool simpleOk = (simpleClip == null) || (simpleClip.loadState == AudioDataLoadState.Loaded);
        bool hardOk = (hardClip == null) || (hardClip.loadState == AudioDataLoadState.Loaded);
        return simpleOk && hardOk;
    }

    private void Update()
    {
        if (!ready)
            return;
        if (Time.time < nextCheck)
            return;
        nextCheck = Time.time + checkInterval;

        bool shouldHard = AnyMonsterAlive();
        if (shouldHard == inHardMode)
            return;

        inHardMode = shouldHard;
        CrossfadeTo(inHardMode);

        if (verboseLog)
            Debug.Log("[动态音乐] 切换 -> " + (inHardMode ? "困难版（有怪）" : "简单版（没怪）"));
    }

    /// <summary>场上是否有真正在场（已苏醒、未死亡）的怪物</summary>
    private bool AnyMonsterAlive()
    {
        MonsterAI[] all = FindObjectsOfType<MonsterAI>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (!all[i].gameObject.activeInHierarchy) continue;
            if (all[i].IsDormant) continue;
            return true;
        }
        return false;
    }

    /// <summary>立即设好音量（进场用，不淡化）</summary>
    private void ApplyVolume(bool hard)
    {
        simpleSource.volume = hard ? 0f : musicVolume;
        hardSource.volume = hard ? musicVolume : 0f;
    }

    /// <summary>交叉淡化切换：只改音量，不碰 Play/Stop</summary>
    private void CrossfadeTo(bool hard)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeRoutine(hard));
    }

    private IEnumerator FadeRoutine(bool toHard)
    {
        AudioSource from = toHard ? simpleSource : hardSource;
        AudioSource to = toHard ? hardSource : simpleSource;

        // 两条都在播，这里只把音量一升一降，无缝过渡
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            to.volume = musicVolume * k;
            from.volume = musicVolume * (1f - k);
            yield return null;
        }

        to.volume = musicVolume;
        from.volume = 0f;      // 只静音，不 Stop，保持同步
    }

    /// <summary>外部手动设置目标状态</summary>
    public void ForceMode(bool hard)
    {
        if (!ready) return;
        if (hard == inHardMode) return;
        inHardMode = hard;
        CrossfadeTo(hard);
    }
}
