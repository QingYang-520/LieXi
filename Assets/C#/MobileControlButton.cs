using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 移动端方向键按钮：按住持续移动，松开停止。
/// 按钮本身是盖在方向键底图上的透明触摸区；pressVisual 指定按下时变暗的提示图（通常就是整张底图）。
/// 玩家对象可以后加：留空 player 时会在运行时自动查找场景中的 PlayerMovement。
/// </summary>
public class MobileControlButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private enum Direction { Up, Down, Left, Right }

    [SerializeField] private Direction direction = Direction.Up;
    [Tooltip("留空则运行时自动查找场景中的 PlayerMovement")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("按下时变暗的提示图（可留空），通常指定整张方向键底图")]
    [SerializeField] private Graphic pressVisual;

    private static readonly Color s_NormalColor = Color.white;
    private static readonly Color s_PressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    // 多指同时按住时，最后一根手指抬起才停止移动、才恢复底图颜色
    private static int heldCount;
    private static bool warnedNoPlayer;

    // 当前按住中的虚拟方向键数量（供 PlayerMovement 判断是否让位给虚拟按键）
    public static int HeldCount
    {
        get { return heldCount; }
    }

    private bool pressed;

    private PlayerMovement Target
    {
        get
        {
            if (player == null)
                player = FindObjectOfType<PlayerMovement>();
            return player;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        heldCount++;
        ApplyTint();

        PlayerMovement target = Target;
        if (target == null)
        {
            if (!warnedNoPlayer)
            {
                warnedNoPlayer = true;
                Debug.LogWarning("MobileControlButton: 场景中没有 PlayerMovement，移动键暂不生效。", this);
            }
            return;
        }

        switch (direction)
        {
            case Direction.Up: target.MoveUp(); break;
            case Direction.Down: target.MoveDown(); break;
            case Direction.Left: target.MoveLeft(); break;
            case Direction.Right: target.MoveRight(); break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed)
            return;
        pressed = false;
        ReleaseFinger();
    }

    private void OnDisable()
    {
        if (!pressed)
            return;
        pressed = false;
        ReleaseFinger();
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // 兜底：按住时把鼠标拖出游戏窗口再松开，uGUI 收不到抬起事件，
        // “按住中”计数会卡死、导致键盘输入一直被忽略。检测到鼠标已松开就强制复位。
        if (pressed && !Input.GetMouseButton(0))
        {
            pressed = false;
            ReleaseFinger();
        }
#endif
    }

    void OnApplicationPause(bool paused)
    {
        // 手机上切后台/来电时会错过抬起事件，暂停时统一复位
        if (paused && pressed)
        {
            pressed = false;
            ReleaseFinger();
        }
    }

    private void ReleaseFinger()
    {
        heldCount = Mathf.Max(0, heldCount - 1);
        ApplyTint();
        if (heldCount > 0)
            return;

        PlayerMovement target = Target;
        if (target != null)
            target.StopMoving();
    }

    private void ApplyTint()
    {
        if (pressVisual != null)
            pressVisual.color = heldCount > 0 ? s_PressedColor : s_NormalColor;
    }
}
