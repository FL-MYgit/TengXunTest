using UnityEngine;

/// <summary>
/// NPC 的技能与规则执行层。
/// AI 只能调用 MoveTo 和 TryKill，真正的坐标边界、身份、范围、冷却和目标选择
/// 都由这里或 AIGameManager 校验，避免模型生成错误内容破坏游戏规则。
/// </summary>
public class NPCController : MonoBehaviour
{
    [SerializeField] private float civilianMoveSpeed = 1.8f;
    [SerializeField] private float killerMoveSpeed = 1.8f;
    [SerializeField] private float killRadius = 1.25f;
    [SerializeField] private float killCooldown = 10f;

    private AIGameManager gameManager;
    private Vector2 destination;
    private float nextKillTime;

    public int PlayerId { get; private set; }
    public PlayerRole Role { get; private set; }
    public bool IsAlive { get; private set; }
    public float KillCooldownRemaining => Mathf.Max(0f, nextKillTime - Time.time);
    public Vector2 Position => transform.position;

    /// <summary>每局开始时重置身份、出生位置、存活状态和击杀冷却。</summary>
    public void Configure(int playerId, PlayerRole role, AIGameManager manager, Vector2 spawnPosition)
    {
        PlayerId = playerId;
        Role = role;
        gameManager = manager;
        IsAlive = true;
        transform.position = spawnPosition;
        destination = spawnPosition;
        nextKillTime = 0f;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.color = role == PlayerRole.Killer ? new Color(0.95f, 0.2f, 0.2f) : new Color(0.2f, 0.75f, 1f);
    }

    /// <summary>使用角色对应速度平滑移向目标；杀手速度与平民相等。</summary>
    private void Update()
    {
        if (!IsAlive || gameManager == null || !gameManager.IsRunning) return;
        float speed = Role == PlayerRole.Killer ? killerMoveSpeed : civilianMoveSpeed;
        transform.position = Vector2.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }

    /// <summary>
    /// 设置移动目标。目标会被限制在方形场地内；平民明显靠近杀手的目标会被拒绝。
    /// </summary>
    public void MoveTo(Vector2 targetPosition)
    {
        if (!IsAlive || gameManager == null || !gameManager.IsRunning) return;
        // 平民不能执行会主动靠近杀手的模型指令。
        if (Role == PlayerRole.Civilian && !gameManager.IsSafeCivilianTarget(this, targetPosition)) return;
        destination = gameManager.ClampToArena(targetPosition);
    }

    /// <summary>
    /// 尝试击杀。只有存活杀手、冷却结束且范围内存在平民时才成功。
    /// 最近目标的选择由 GameManager 统一完成。
    /// </summary>
    public bool TryKill()
    {
        if (!IsAlive || Role != PlayerRole.Killer || gameManager == null || !gameManager.IsRunning) return false;
        if (Time.time < nextKillTime) return false;
        if (!gameManager.TryKillNearest(this, killRadius)) return false;
        nextKillTime = Time.time + killCooldown;
        return true;
    }

    /// <summary>将角色标记死亡、停止移动，并用灰色半透明表现尸体。</summary>
    public void Die()
    {
        if (!IsAlive) return;
        IsAlive = false;
        destination = transform.position;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = new Color(0.25f, 0.25f, 0.25f, 0.65f);
    }
}
