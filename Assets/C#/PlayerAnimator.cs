using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 2D 帧动画组件：把各动作的序列帧拖进对应数组即可完成绑定（无需 Animator）。
/// 根据 PlayerMovement 的移动方向自动选择 向左/向右 行走帧，静止时回到待机帧。
/// 攻击（挥刀）：移动端点攻击按钮或调用 PlayAttack()；电脑端鼠标左键默认触发。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("走路（向右）帧序列，按播放顺序")]
    [SerializeField] private Sprite[] walkRight;
    [Header("走路（向左）帧序列，按播放顺序")]
    [SerializeField] private Sprite[] walkLeft;
    [Header("挥刀（向右）帧序列，按播放顺序")]
    [SerializeField] private Sprite[] slashRight;
    [Header("挥刀（向左）帧序列，按播放顺序")]
    [SerializeField] private Sprite[] slashLeft;
    [Tooltip("走路动画每秒播放的帧数")]
    [SerializeField] private float framesPerSecond = 12f;
    [Tooltip("挥刀动画每秒播放的帧数")]
    [SerializeField] private float slashFramesPerSecond = 14f;
    [Tooltip("两次攻击之间的最小间隔（秒）")]
    [SerializeField] private float attackInterval = 0.6f;
    [Header("攻击判定")]
    [Tooltip("攻击可命中的前方距离（约到挥刀特效最前端）")]
    [SerializeField] private float attackReach = 115f;
    [Tooltip("可命中的纵向容差（约为角色身高一半）")]
    [SerializeField] private float attackVerticalTolerance = 80f;
    [Tooltip("每次攻击造成的伤害")]
    [SerializeField] private float attackDamage = 10f;
    [Tooltip("挥刀第几帧判定命中")]
    [SerializeField] private int attackHitFrame = 2;
    [Tooltip("静止时显示的帧；留空则用当前朝向的第一帧")]
    [SerializeField] private Sprite idleFrame;

    private SpriteRenderer spriteRenderer;
    private PlayerMovement player;
    private float timer;
    private int frame;
    private bool facingLeft;

    private bool slashing;
    private int slashFrame;
    private float slashTimer;
    private float attackCooldownTimer;
    private bool slashHitDone;

    private Sprite[] CurrentWalkSet
    {
        get { return facingLeft ? walkLeft : walkRight; }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        player = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // 鼠标左键攻击：电脑端直接支持，安卓上外接鼠标同样有效。
        // 手指触摸屏幕会模拟鼠标左键，所以用 touchCount==0 把触摸排除掉；
        // 点在 UI 按钮上时不触发，避免按移动键误出刀
        if (Input.GetMouseButtonDown(0) && Input.touchCount == 0)
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                PlayAttack();
        }
    }

    /// <summary>
    /// 触发一次挥刀动画（按当前朝向播放对应的一组帧，播完自动回到移动状态）
    /// </summary>
    public void PlayAttack()
    {
        // 攻击间隔冷却中，忽略本次触发
        if (attackCooldownTimer > 0f)
            return;

        Sprite[] set = facingLeft ? slashLeft : slashRight;
        if (set == null || set.Length == 0)
            return;

        slashing = true;
        slashFrame = 0;
        slashTimer = 0f;
        attackCooldownTimer = attackInterval;
        slashHitDone = false;
    }

    void LateUpdate()
    {
        if (slashing)
        {
            Sprite[] slashSet = facingLeft ? slashLeft : slashRight;
            if (slashSet == null || slashSet.Length == 0)
            {
                slashing = false;
            }
            else
            {
                slashTimer += Time.deltaTime * slashFramesPerSecond;
                while (slashTimer >= 1f)
                {
                    slashTimer -= 1f;
                    slashFrame++;
                }
                if (!slashHitDone && slashFrame >= attackHitFrame)
                {
                    slashHitDone = true;
                    ApplySlashHit();
                }
                if (slashFrame >= slashSet.Length)
                {
                    slashing = false;
                }
                else
                {
                    spriteRenderer.sprite = slashSet[slashFrame];
                    return;
                }
            }
        }

        Vector2 dir = player != null ? player.MoveDirection : Vector2.zero;
        bool moving = dir.sqrMagnitude > 0.0001f;
        bool wasFacingLeft = facingLeft;
        if (moving && Mathf.Abs(dir.x) > 0.0001f)
            facingLeft = dir.x < 0f;

        Sprite[] set = CurrentWalkSet;
        if (set == null || set.Length == 0)
            return;

        if (!moving)
        {
            frame = 0;
            timer = 0f;
            spriteRenderer.sprite = idleFrame != null ? idleFrame : set[0];
            return;
        }

        // 朝向切换时从头播放另一组帧
        if (facingLeft != wasFacingLeft)
        {
            frame = 0;
            timer = 0f;
        }

        timer += Time.deltaTime * framesPerSecond;
        while (timer >= 1f)
        {
            timer -= 1f;
            frame = (frame + 1) % set.Length;
        }
        spriteRenderer.sprite = set[frame];
    }

    // 攻击判定：怪物在面朝方向、距玩家中心 attackReach 内（约到挥刀特效最前端），
    // 且纵向偏差不超过角色身高一半（高于一半、低于一半都打不到）时受击
    private void ApplySlashHit()
    {
        Vector2 pos = transform.position;
        Health[] targets = FindObjectsOfType<Health>();
        foreach (Health h in targets)
        {
            if (h.Side != Health.Faction.Enemy || h.IsDead)
                continue;

            Vector2 d = (Vector2)h.transform.position - pos;
            float forward = facingLeft ? -d.x : d.x;
            if (forward < -20f || forward > attackReach)
                continue;
            if (Mathf.Abs(d.y) > attackVerticalTolerance)
                continue;

            h.TakeDamage(attackDamage);
        }
    }
}
