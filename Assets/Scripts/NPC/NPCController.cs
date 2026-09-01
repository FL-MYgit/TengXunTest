using System.Collections;
using NavMeshPlus.Components;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC 的移动与说话控制器。
/// 移动只能通过 NavMeshAgent 完成；绑定失败时角色停在原地，避免穿墙和越界。
/// </summary>
public class NPCController : MonoBehaviour
{
    [SerializeField] private TMP_Text text; //AI输出文本引用
    [SerializeField] private float moveSpeed = 1f; //移动速度
    [SerializeField, Min(0.1f)] private float spawnSampleRadius = 20f; //出生点搜索半径
    [SerializeField, Min(0.1f)] private float destinationSampleRadius = 3f; //移动最大搜索半径

    private NavMeshAgent navMeshAgent;
    private bool navMeshReady;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        // Agent 不能在 NavMesh 注册前启用，否则 Unity 会报告出生点不在 NavMesh 上。
        if (navMeshAgent != null) navMeshAgent.enabled = false;
    }

    private IEnumerator Start()
    {
        if (navMeshAgent == null)
        {
            Debug.LogError("NPC 缺少 NavMeshAgent，无法移动。", this);
            yield break;
        }

        // Physics2D 更新后，NavMeshPlus 才能正确收集 Collider2D。
        yield return new WaitForFixedUpdate();
        // NavMeshPlus 在第 1 帧构建 Physics Collider 数据会给出精度警告；等到下一帧再构建。
        if (Time.frameCount <= 1) yield return null;
        ConfigureAgent();

        // 先使用已有数据；若数据无效，则根据当前 Collider2D 在运行时重建一次。
        if (!TryAttachToNavMesh())
        {
            NavMeshSurface surface = FindObjectOfType<NavMeshSurface>();
            if (surface != null)
            {
                surface.BuildNavMesh();
                yield return null;
                TryAttachToNavMesh();
            }
        }

        if (!navMeshReady)
            Debug.LogError("NPC 无法绑定到 2D NavMesh，已停止移动以防止越界。请检查 NavMeshSurface 和地图碰撞体。", this);
    }

    private void ConfigureAgent()
    {
        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = false;
        navMeshAgent.speed = moveSpeed;
    }

    /// <summary>
    /// 初始化NavMesh寻路，自动贴附附近的NavMesh网格
    /// </summary>
    /// <returns></returns>
    private bool TryAttachToNavMesh()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, spawnSampleRadius, navMeshAgent.areaMask))
            return false;

        transform.position = hit.position;
        navMeshAgent.enabled = true;
        navMeshReady = navMeshAgent.isOnNavMesh;
        if (!navMeshReady) navMeshAgent.enabled = false;
        return navMeshReady;
    }

    /// <summary>
    /// 目标在墙内或地图外时会被投射到最近的可行走点；找不到时忽略指令。
    /// </summary>
    public void MoveTo(Vector2 target)
    {
        if (!navMeshReady || navMeshAgent == null || !navMeshAgent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, destinationSampleRadius, navMeshAgent.areaMask))
            navMeshAgent.SetDestination(hit.position);
        else
            Debug.LogWarning($"目标位置 {target} 不在 NavMesh 上，已忽略本次移动。", this);
    }

    public void Say(string message)
    {
        if (text != null) text.text = message;

        if (!string.IsNullOrWhiteSpace(message))
            Debug.Log($"NPC：{message}", this);
    }
}
