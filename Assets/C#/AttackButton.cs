using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 攻击按钮：按下触发 PlayerAnimator 的挥刀动画。
/// playerAnimator 留空则运行时自动查找场景中的 PlayerAnimator。
/// </summary>
public class AttackButton : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("留空则运行时自动查找场景中的 PlayerAnimator")]
    [SerializeField] private PlayerAnimator playerAnimator;

    private static bool warnedNoAnimator;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (playerAnimator == null)
            playerAnimator = FindObjectOfType<PlayerAnimator>();

        if (playerAnimator != null)
        {
            playerAnimator.PlayAttack();
        }
        else if (!warnedNoAnimator)
        {
            warnedNoAnimator = true;
            Debug.LogWarning("AttackButton: 场景中没有 PlayerAnimator，攻击键暂不生效。", this);
        }
    }
}
