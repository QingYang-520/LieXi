using UnityEngine;

/// <summary>
/// 怪物休眠开关：让场景里预先摆好的小怪在剧情播放期间"存在但不参与游戏"。
///
/// 休眠（Dormant）状态下小怪：
/// - 隐藏渲染（SpriteRenderer + 头顶血条全部关闭），玩家看不见
/// - 关闭碰撞体，不挡路、不会被挥刀判定命中
/// - 刚体速度清零并切为 Kinematic，物理系统不会再推动它
/// - MonsterAI 停止索敌、停止追击、绝不攻击玩家（即便玩家贴脸站着）
///
/// 苏醒（Awake）后一切恢复正常，小怪照常索敌追击攻击。
///
/// 用法：把本组件挂在场景里任一物体上（推荐挂在一个空的 "MonsterSpawnTrigger" 节点），
/// 在 Inspector 的 monsters 数组里拖入要控制的小怪。
/// 剧情播放完毕后调用 Spawn()（或 SpawnAll() 静态方法）即可放怪。
///
/// 也可以用 TriggerByPlayerEnter 选项改成"玩家走进范围自动放怪"，
/// 两种方式都会走同一个内部流程，且只放一次。
/// </summary>
[DisallowMultipleComponent]
public class MonsterSpawnTrigger : MonoBehaviour
{
    [Header("要控制的怪物")]
    [Tooltip("剧情期间休眠、触发后苏醒的小怪。留空则运行时自动收集场景里所有 MonsterAI")]
    [SerializeField] private MonsterAI[] monsters;

    [Header("初始状态")]
    [Tooltip("勾选：进入场景时立即休眠（剧情模式，默认）\n取消勾选：进入场景时小怪就是苏醒的（普通关卡）")]
    [SerializeField] private bool startDormant = true;

    [Header("触发方式（可选）")]
    [Tooltip("勾选后，玩家走进 triggerRadius 范围也会自动放怪（和剧情调用二选一，先到先得）")]
    [SerializeField] private bool triggerByPlayerEnter = false;
    [Tooltip("玩家进入该距离（世界单位）自动放怪")]
    [SerializeField] private float triggerRadius = 120f;
    [Tooltip("触发范围检测中心，留空则用本物体位置")]
    [SerializeField] private Transform triggerCenter;

    [Header("调试")]
    [Tooltip("勾选后按 F5 手动放怪，方便在编辑器里测试")]
    [SerializeField] private bool debugHotkey = false;

    private bool spawned;

    private void Awake()
    {
        CollectMonstersIfNeeded();

        if (startDormant)
            SetDormant(true);
    }

    private void Start()
    {
        // Awake 之后再兜底一次：确保即使脚本执行顺序有变化，剧情开始时小怪也一定是睡着的
        if (startDormant && !spawned)
            SetDormant(true);
    }

    private void Update()
    {
        if (spawned)
            return;

        if (debugHotkey && Input.GetKeyDown(KeyCode.F5))
        {
            Spawn();
            return;
        }

        if (!triggerByPlayerEnter)
            return;

        PlayerMovement p = FindObjectOfType<PlayerMovement>();
        if (p == null)
            return;

        Vector2 center = triggerCenter != null ? triggerCenter.position : transform.position;
        if (((Vector2)p.transform.position - center).sqrMagnitude <= triggerRadius * triggerRadius)
            Spawn();
    }

    /// <summary>
    /// 放怪：唤醒所有被本组件管理的小怪。重复调用无副作用（只放一次）。
    /// 剧情播放结束后调用这个方法。
    /// </summary>
    public void Spawn()
    {
        if (spawned)
            return;
        spawned = true;

        CollectMonstersIfNeeded();

        // 先让所有小怪苏醒（激活物体 + 恢复物理）
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null)
                continue;
            monsters[i].gameObject.SetActive(true);
            monsters[i].SetDormant(false);
        }
    }

    /// <summary>把当前所有小怪重新按休眠处理（调试用，比如重播剧情）</summary>
    public void ResetToDormant()
    {
        spawned = false;
        SetDormant(true);
    }

    /// <summary>
    /// 静态便捷方法：让场景里所有小怪立即休眠（剧情开始前调用）。
    /// 场景里已经有 MonsterSpawnTrigger 时不需要用它。
    /// </summary>
    public static void SetAllDormant(bool dormant)
    {
        MonsterAI[] all = FindObjectsOfType<MonsterAI>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            all[i].gameObject.SetActive(true);
            all[i].SetDormant(dormant);
        }
    }

    /// <summary>
    /// 静态便捷方法：唤醒场景里所有休眠中的小怪（剧情结束后调用）。
    /// </summary>
    public static void SpawnAll()
    {
        MonsterAI[] all = FindObjectsOfType<MonsterAI>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            all[i].gameObject.SetActive(true);
            all[i].SetDormant(false);
        }
    }

    private void SetDormant(bool dormant)
    {
        CollectMonstersIfNeeded();
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null)
                continue;
            monsters[i].SetDormant(dormant);
        }
    }

    private void CollectMonstersIfNeeded()
    {
        if (monsters != null && monsters.Length > 0)
            return;
        monsters = FindObjectsOfType<MonsterAI>();
    }

    private void OnDrawGizmosSelected()
    {
        if (!triggerByPlayerEnter)
            return;
        Vector3 center = triggerCenter != null ? triggerCenter.position : transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(center, triggerRadius);
    }
}
