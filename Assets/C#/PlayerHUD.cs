using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条 HUD：左侧文字显示剩余血量，右侧进度条显示血量比例。
/// 挂在 Canvas 的 HUD 面板上，自动查找场景中玩家的 Health，血量变化时才刷新。
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private Text hpText;
    [SerializeField] private Image hpFill;

    private Health playerHealth;
    private int lastShown = -1;

    private void Start()
    {
        PlayerMovement p = FindObjectOfType<PlayerMovement>();
        if (p != null)
            playerHealth = p.GetComponent<Health>();
    }

    private void LateUpdate()
    {
        if (playerHealth == null || hpText == null || hpFill == null)
            return;

        int hp = Mathf.CeilToInt(playerHealth.CurrentHP);
        if (hp == lastShown)
            return;
        lastShown = hp;

        hpText.text = "剩余血量：" + hp + "/" + Mathf.CeilToInt(playerHealth.MaxHP);
        hpFill.fillAmount = playerHealth.MaxHP > 0f ? playerHealth.CurrentHP / playerHealth.MaxHP : 0f;
    }
}
