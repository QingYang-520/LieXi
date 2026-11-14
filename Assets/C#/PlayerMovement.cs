using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public GameObject mobileUI;

    private Vector2 moveDirection;
    private bool isMobile;
    private Rigidbody2D rb;
    private Health playerHealth;
    private Vector2 spawnPosition;
    private bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // 用“运行时是否真机”判断，而不是编译期平台宏：
        // 编辑器里即使构建目标是安卓，也保持键盘可用，方便测试；真机上才走虚拟按键
        isMobile = Application.isMobilePlatform;

        playerHealth = GetComponent<Health>();
        if (playerHealth != null)
            playerHealth.Died += OnPlayerDied;

        if (mobileUI != null)
            mobileUI.SetActive(isMobile);
    }

    void Start()
    {
        spawnPosition = transform.position;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.Died -= OnPlayerDied;
    }

    void Update()
    {
        if (dead)
            return;

        // 所有平台（含安卓外接键盘）都支持键盘；
        // 虚拟方向键按住期间由按键控制方向，键盘输入不覆盖
        if (MobileControlButton.HeldCount > 0)
            return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector2(h, v).normalized;
    }

    void FixedUpdate()
    {
        if (dead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 用刚体速度移动：撞到边界/墙会被物理系统挡住，还能沿墙面滑动
        rb.velocity = moveDirection * moveSpeed;
    }

    // 死亡：隐身、停止控制，2.5 秒后在出生点满血复活
    private void OnPlayerDied(Health h)
    {
        dead = true;
        moveDirection = Vector2.zero;
        rb.velocity = Vector2.zero;
        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<BoxCollider2D>().enabled = false;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        transform.position = spawnPosition;
        if (playerHealth != null)
            playerHealth.RestoreFull();
        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<BoxCollider2D>().enabled = true;
        dead = false;
    }

    // 当前移动方向（供动画等组件读取；为零向量表示静止）
    public Vector2 MoveDirection
    {
        get { return moveDirection; }
    }

    public void MoveUp() { moveDirection = Vector2.up; }
    public void MoveDown() { moveDirection = Vector2.down; }
    public void MoveLeft() { moveDirection = Vector2.left; }
    public void MoveRight() { moveDirection = Vector2.right; }
    public void StopMoving() { moveDirection = Vector2.zero; }
}
