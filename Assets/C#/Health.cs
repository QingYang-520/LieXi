using System;
using UnityEngine;

/// <summary>
/// 通用血量组件：玩家和小怪都用它。
/// TakeDamage 扣血，血量归零触发 Died 事件，由各自的逻辑决定死亡表现。
/// </summary>
public class Health : MonoBehaviour
{
    public enum Faction { Player = 0, Enemy = 1 }

    [SerializeField] private Faction faction = Faction.Player;
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP = 100f;

    public Faction Side
    {
        get { return faction; }
    }

    public float MaxHP
    {
        get { return maxHP; }
    }

    public float CurrentHP
    {
        get { return currentHP; }
    }

    public bool IsDead
    {
        get { return currentHP <= 0f; }
    }

    public event Action<Health> Died;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (currentHP <= 0f)
            return;

        currentHP = Mathf.Max(0f, currentHP - amount);
        if (currentHP <= 0f && Died != null)
            Died(this);
    }

    // 满血恢复（玩家复活等场景用）
    public void RestoreFull()
    {
        currentHP = maxHP;
    }
}
