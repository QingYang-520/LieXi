using UnityEngine;

/// <summary>
/// 小怪 AI：索敌（玩家进入范围自动锁定）→ 追击（遇墙沿墙滑行绕道，持续卡死自动脱困）→
/// 进入攻击范围后停下攻击，真实扣除玩家血量。头顶血条实时跟随血量变化，死亡后整体隐藏。
/// 动画：走路 6 帧、攻击 5 帧，按朝向自动切换。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class MonsterAI : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 140f;
    [Tooltip("索敌范围：玩家进入该距离自动锁定")]
    [SerializeField] private float detectRange = 450f;
    [Tooltip("前方有墙且距离小于该值时开始绕行")]
    [SerializeField] private float avoidDistance = 90f;

    [Header("攻击")]
    [SerializeField] private float attackRange = 110f;
    [SerializeField] private float attackDamage = 5f;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private float attackVerticalTolerance = 70f;

    [Header("动画帧（按播放顺序）")]
    [SerializeField] private Sprite[] walkRight;
    [SerializeField] private Sprite[] walkLeft;
    [SerializeField] private Sprite[] attackRight;
    [SerializeField] private Sprite[] attackLeft;
    [SerializeField] private float framesPerSecond = 10f;

    [Header("头顶血条")]
    [SerializeField] private Transform hpBarBack;
    [SerializeField] private Transform hpBarFill;
    [SerializeField] private float barWidth = 80f;
    [SerializeField] private float barHeight = 7f;
    [SerializeField] private float barOffsetY = 45f;

    private Rigidbody2D rb;
    private Health health;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer backRenderer;
    private Transform playerTransform;
    private Health playerHealth;

    private float attackCooldown;
    private float animTimer;
    private int animFrame;
    private bool attacking;
    private int attackAnimFrame;
    private float attackAnimTimer;
    private bool facingLeft = true;

    private Vector2 detourDir;
    private float detourTimer;
    private Vector2 lastPosition;
    private float stuckTimer;

    // 休眠：剧情期间"存在但不参与游戏"——隐藏、不动、不索敌、不攻击
    private bool dormant;
    private bool cachedParts;
    private SpriteRenderer[] allRenderers;
    private Collider2D[] allColliders;
    private RigidbodyType2D originalBodyType;

    public bool IsChasing { get; private set; }

    /// <summary>是否处于休眠状态（剧情尚未放怪）</summary>
    public bool IsDormant { get { return dormant; } }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (hpBarFill != null)
            fillRenderer = hpBarFill.GetComponent<SpriteRenderer>();
        if (hpBarBack != null)
            backRenderer = hpBarBack.GetComponent<SpriteRenderer>();
        health.Died += OnDied;
        originalBodyType = rb.bodyType;
    }

    /// <summary>
    /// 休眠 / 苏醒。
    /// dormant=true：隐藏渲染、关闭碰撞、冻结刚体、停止索敌与攻击。玩家贴脸站着也不会被打。
    /// dormant=false：恢复原状，照常索敌追击攻击。
    /// </summary>
    public void SetDormant(bool value)
    {
        if (dormant == value)
            return;

        dormant = value;
        CacheParts();

        // 渲染：本体 + 头顶血条一起隐藏
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
                allRenderers[i].enabled = !value;
        }

        // 碰撞：休眠时关掉，不挡路、也不会被玩家挥刀命中
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null)
                allColliders[i].enabled = !value;
        }

        if (rb != null)
        {
            // 休眠时切成 Kinematic：物理系统不会再推动它，也不会被别的物体撞飞
            rb.velocity = Vector2.zero;
            rb.bodyType = value ? RigidbodyType2D.Kinematic : originalBodyType;
        }

        attacking = false;
        IsChasing = false;
        detourTimer = 0f;
        stuckTimer = 0f;
        attackCooldown = 0f;
        lastPosition = transform.position;
    }

    private void CacheParts()
    {
        if (cachedParts)
            return;
        cachedParts = true;
        // 连子物体一起收集，血条是 Monster 的子节点
        allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        allColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void Start()
    {
        PlayerMovement p = FindObjectOfType<PlayerMovement>();
        if (p != null)
        {
            playerTransform = p.transform;
            playerHealth = p.GetComponent<Health>();
            facingLeft = playerTransform.position.x < transform.position.x;
        }
        lastPosition = transform.position;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Died -= OnDied;
    }

    private void Update()
    {
        // 休眠：不跑动画、不刷血条，彻底不参与游戏
        if (dormant)
            return;

        if (attackCooldown > 0f)
            attackCooldown -= Time.deltaTime;

        TickAnimation();
        UpdateHpBar();
    }

    private void FixedUpdate()
    {
        // 休眠：绝不动、绝不索敌、绝不攻击
        if (dormant)
        {
            if (rb != null)
                rb.velocity = Vector2.zero;
            IsChasing = false;
            return;
        }

        if (health.IsDead || playerTransform == null || playerHealth == null)
        {
            if (rb != null)
                rb.velocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        // 索敌：玩家进入范围且活着才追
        IsChasing = dist <= detectRange && !playerHealth.IsDead;
        if (!IsChasing)
        {
            rb.velocity = Vector2.zero;
            lastPosition = transform.position;
            return;
        }

        // 攻击距离内：停下打人（真实扣玩家血）
        if (dist <= attackRange && Mathf.Abs(toPlayer.y) <= attackVerticalTolerance)
        {
            rb.velocity = Vector2.zero;
            lastPosition = transform.position;
            if (Mathf.Abs(toPlayer.x) > 2f)
                facingLeft = toPlayer.x < 0f;

            if (attackCooldown <= 0f && !playerHealth.IsDead)
            {
                attackCooldown = attackInterval;
                StartAttack();
                playerHealth.TakeDamage(attackDamage);
            }
            return;
        }

        // 追击（含绕墙）
        Vector2 dir = AvoidWalls(toPlayer.normalized, toPlayer);
        rb.velocity = dir * moveSpeed;
        if (Mathf.Abs(toPlayer.x) > 2f)
            facingLeft = toPlayer.x < 0f;

        // 卡死检测：想走但几乎没挪动
        float moved = ((Vector2)transform.position - lastPosition).magnitude;
        float expected = moveSpeed * Time.fixedDeltaTime;
        if (moved < expected * 0.25f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;
        lastPosition = transform.position;
    }

    // 遇墙绕行：沿墙面滑向玩家所在的一侧；持续卡死时先垂直挪一步找路
    private Vector2 AvoidWalls(Vector2 dir, Vector2 toPlayer)
    {
        if (detourTimer > 0f)
        {
            detourTimer -= Time.fixedDeltaTime;
            return detourDir;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, avoidDistance);
        if (hit.collider != null && hit.distance > 1f && hit.collider.name.StartsWith("Wall"))
        {
            Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);
            if (Vector2.Dot(tangent, toPlayer) < 0f)
                tangent = -tangent;
            detourDir = tangent;
            detourTimer = 0.25f;
            return tangent;
        }

        if (stuckTimer > 0.5f)
        {
            stuckTimer = 0f;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            if (Random.value < 0.5f)
                perp = -perp;
            detourDir = perp;
            detourTimer = 0.5f;
            return perp;
        }

        return dir;
    }

    private void StartAttack()
    {
        attacking = true;
        attackAnimFrame = 0;
        attackAnimTimer = 0f;
    }

    private void TickAnimation()
    {
        if (attacking)
        {
            Sprite[] attackSet = facingLeft ? attackLeft : attackRight;
            if (attackSet == null || attackSet.Length == 0)
            {
                attacking = false;
            }
            else
            {
                attackAnimTimer += Time.deltaTime * framesPerSecond;
                while (attackAnimTimer >= 1f)
                {
                    attackAnimTimer -= 1f;
                    attackAnimFrame++;
                }
                if (attackAnimFrame >= attackSet.Length)
                {
                    attacking = false;
                }
                else
                {
                    spriteRenderer.sprite = attackSet[attackAnimFrame];
                }
            }
        }

        if (attacking)
            return;

        Sprite[] walk = facingLeft ? walkLeft : walkRight;
        if (walk == null || walk.Length == 0)
            return;

        bool moving = IsChasing && rb.velocity.sqrMagnitude > 1f;
        if (!moving)
        {
            animFrame = 0;
            animTimer = 0f;
            spriteRenderer.sprite = walk[0];
            return;
        }

        animTimer += Time.deltaTime * framesPerSecond;
        while (animTimer >= 1f)
        {
            animTimer -= 1f;
            animFrame = (animFrame + 1) % walk.Length;
        }
        spriteRenderer.sprite = walk[animFrame];
    }

    private void UpdateHpBar()
    {
        float frac = health.MaxHP > 0f ? health.CurrentHP / health.MaxHP : 0f;
        float s = Mathf.Abs(transform.lossyScale.x);
        if (s < 0.0001f)
            s = 1f;

        // 缩放要除以血条贴图自身的尺寸（White.png 是 8x8 像素），
        // 否则算出来的血条只有几个单位大，肉眼看不见
        if (hpBarBack != null && backRenderer != null && backRenderer.sprite != null)
        {
            Vector2 bs = backRenderer.sprite.bounds.size;
            hpBarBack.localScale = new Vector3(barWidth / (bs.x * s), barHeight / (bs.y * s), 1f);
            hpBarBack.localPosition = new Vector3(0f, barOffsetY / s, 0f);
        }
        if (hpBarFill != null && fillRenderer != null && fillRenderer.sprite != null)
        {
            Vector2 fs = fillRenderer.sprite.bounds.size;
            float w = barWidth * frac;
            hpBarFill.localScale = new Vector3(w / (fs.x * s), (barHeight * 0.6f) / (fs.y * s), 1f);
            hpBarFill.localPosition = new Vector3((-barWidth + w) * 0.5f / s, barOffsetY / s, 0f);
            fillRenderer.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.25f, 0.85f, 0.3f), frac);
        }
    }

    private void OnDied(Health h)
    {
        gameObject.SetActive(false);
    }
}
