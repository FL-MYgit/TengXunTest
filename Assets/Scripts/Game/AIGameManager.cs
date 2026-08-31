using System.Collections.Generic;
using System.Text;
using LLMUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 单局游戏的总控制器，也是所有角色共享的“世界状态源”。
///
/// 它负责创建五名角色、随机分配身份、维护方形场地与胜负条件，
/// 并以 0.2 秒为周期执行可靠的基础战术和轮流调度 LLM 决策。
/// 基础战术不依赖模型响应速度，因此即使本地模型正在推理，追逐、逃跑、
/// 击杀范围判定和三分钟计时也会继续正常运行。
/// </summary>
[DefaultExecutionOrder(-100)]
public class AIGameManager : MonoBehaviour
{
    // 固定规则参数。AI 只能在这些规则允许的范围内选择动作。
    private const int RequiredPlayers = 5;
    private const float TacticalTickInterval = 0.2f;
    [Tooltip("场地中心到 X/Y 边界的距离；默认形成 10×10 的方形场地。")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(5f, 5f);
    [Tooltip("至少一名平民需要存活的秒数。")]
    [SerializeField] private float civilianSurvivalTime = 180f;
    [Tooltip("角色预制体；未设置时自动读取 Resources/Prefabs/NPC。")]
    [SerializeField] private GameObject npcPrefab;

    private readonly List<NPCController> players = new List<NPCController>();
    private float startedAt;
    private float nextTacticalTick;
    private int nextBrainIndex;
    private LLMAgent sharedAgent;
    public bool IsRunning { get; private set; }
    public IReadOnlyList<NPCController> Players => players;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 五人玩法只允许在指定追杀场景启动。采用白名单可以从根本上避免
        // 聊天场景或以后新增的菜单场景被错误注入竞技场、五名 NPC 和管理器。
        if (SceneManager.GetActiveScene().name != "SampleScene") return;
        EnsureGameLLM();
        // 不要求场景手工挂载管理器，同时避免场景中已有管理器时重复创建。
        if (FindObjectOfType<AIGameManager>() == null)
            new GameObject("AI Game Manager").AddComponent<AIGameManager>();
    }

    /// <summary>
    /// 保证追杀场景启动时一定存在 LLM。正常情况下使用场景中保存的组件；
    /// 如果组件被误删，则在 inactive 对象上完成配置后再激活，避免空模型启动。
    /// </summary>
    private static void EnsureGameLLM()
    {
        LLM existing = FindObjectOfType<LLM>(true);
        if (existing != null)
        {
            existing.dontDestroyOnLoad = false;
            return;
        }

        GameObject modelObject = new GameObject("LLM");
        modelObject.SetActive(false);
        LLM model = modelObject.AddComponent<LLM>();
        model.numThreads = -1;
        model.numGPULayers = 0;
        model.parallelPrompts = 1;
        model.contextSize = 2048;
        model.batchSize = 128;
        model.model = "D:/LLMUnityModels/Qwen3.5-2B-Q4_K_M.gguf";
        model.dontDestroyOnLoad = false;
        modelObject.SetActive(true);
    }

    private void Start()
    {
        npcPrefab = npcPrefab != null ? npcPrefab : Resources.Load<GameObject>("Prefabs/NPC");
        if (npcPrefab == null) { Debug.LogError("找不到 Resources/Prefabs/NPC。", this); return; }
        CreateArenaVisual();
        SetupPlayers();
        StartRound();
    }

    private void Update()
    {
        if (!IsRunning) return;

        // 模型一次推理通常远慢于 0.2 秒。战术层按固定频率持续执行，
        // 保证等待模型时平民仍会逃跑，杀手也会追击和尝试击杀。
        if (Time.time >= nextTacticalTick)
        {
            nextTacticalTick = Time.time + TacticalTickInterval;
            RunTacticalTick();
            RequestNextAIDecision();
        }

        int civiliansAlive = 0;
        foreach (NPCController player in players)
            if (player.IsAlive && player.Role == PlayerRole.Civilian) civiliansAlive++;
        if (civiliansAlive == 0) EndRound(GameWinner.Killer);
        else if (Time.time - startedAt >= civilianSurvivalTime) EndRound(GameWinner.Civilians);
    }

    private void SetupPlayers()
    {
        // 优先复用场景中已有角色，不足五名时再从 Resources 预制体补齐。
        players.Clear();
        foreach (NPCController existing in FindObjectsOfType<NPCController>())
            if (players.Count < RequiredPlayers) players.Add(existing);
        while (players.Count < RequiredPlayers)
        {
            GameObject instance = Instantiate(npcPrefab);
            NPCController skills = instance.GetComponent<NPCController>();
            if (skills == null) { Destroy(instance); break; }
            players.Add(skills);
        }
    }

    private void StartRound()
    {
        if (players.Count != RequiredPlayers) { Debug.LogError("NPC 数量不足 5。", this); return; }

        // NPC 预制体中的 LLMAgent 默认禁用，避免每个克隆在 Awake 阶段抢着查找、
        // 初始化同一个原生服务。这里创建一个不显示角色外观的专用共享 Agent。
        sharedAgent = CreateSharedAgent();

        int killerIndex = Random.Range(0, players.Count);
        for (int index = 0; index < players.Count; index++)
        {
            PlayerRole role = index == killerIndex ? PlayerRole.Killer : PlayerRole.Civilian;
            NPCController skills = players[index];
            skills.gameObject.name = $"AI Player {index} ({role})";
            skills.Configure(index, role, this, RandomSpawn(index));
            NPCAgent brain = skills.GetComponent<NPCAgent>();
            // 场景没有 LLM 时仍运行可靠的基础追逐/逃跑逻辑，只跳过模型高级决策。
            if (brain != null && sharedAgent != null) brain.Configure(skills, this, sharedAgent);
        }
        startedAt = Time.time;
        nextTacticalTick = Time.time;
        nextBrainIndex = 0;
        IsRunning = true;
        EventBus.Publish(new GameStartedEvent(players.Count, killerIndex));
        Debug.Log($"游戏开始：玩家 {killerIndex} 是杀手。", this);
    }

    /// <summary>
    /// 创建全局唯一的模型客户端。先在 inactive 对象上绑定场景 LLM，再激活对象，
    /// 从而确保 LLMAgent.Awake 执行时一定已经拥有有效的 LLM 引用。
    /// </summary>
    private LLMAgent CreateSharedAgent()
    {
        // true 表示也搜索暂时未激活的对象，避免场景加载顺序造成“找不到 LLM”的误报。
        LLM model = FindObjectOfType<LLM>(true);
        if (model == null)
        {
            Debug.LogWarning("场景中没有 LLM 组件：本局仅使用基础战术，不启用模型决策。", this);
            return null;
        }

        GameObject agentObject = new GameObject("Shared NPC LLM Agent");
        agentObject.SetActive(false);
        agentObject.transform.SetParent(transform, false);
        LLMAgent result = agentObject.AddComponent<LLMAgent>();
        result.llm = model;
        result.slot = -1;
        agentObject.SetActive(true);
        return result;
    }

    private Vector2 RandomSpawn(int index)
    {
        // 五个出生点大致均匀分布在圆周上，并加入少量随机扰动。
        float angle = index * Mathf.PI * 2f / RequiredPlayers + Random.Range(-0.2f, 0.2f);
        float radius = Mathf.Min(arenaHalfSize.x, arenaHalfSize.y) * 0.72f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    public Vector2 ClampToArena(Vector2 position)
    {
        // 留出角色半径大小的边距，防止角色中心贴到边界线上。
        const float margin = 0.35f;
        position.x = Mathf.Clamp(position.x, -arenaHalfSize.x + margin, arenaHalfSize.x - margin);
        position.y = Mathf.Clamp(position.y, -arenaHalfSize.y + margin, arenaHalfSize.y - margin);
        return position;
    }

    public bool TryKillNearest(NPCController killer, float radius)
    {
        // 遍历所有存活平民，只选择击杀半径内距离最短的一个。
        NPCController nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (NPCController candidate in players)
        {
            if (!candidate.IsAlive || candidate.Role != PlayerRole.Civilian) continue;
            float distance = Vector2.Distance(killer.Position, candidate.Position);
            if (distance <= radius && distance < nearestDistance)
            { nearest = candidate; nearestDistance = distance; }
        }
        if (nearest == null) return false;
        nearest.Die();
        EventBus.Publish(new PlayerKilledEvent(killer.PlayerId, nearest.PlayerId, nearest.Position));
        Debug.Log($"玩家 {killer.PlayerId} 击杀玩家 {nearest.PlayerId}。", this);
        return true;
    }

    /// <summary>
    /// 执行不依赖模型延迟的基本生存策略。
    /// 平民每 0.2 秒更新逃跑方向；杀手追逐最近平民，并在合法范围内自动击杀。
    /// </summary>
    private void RunTacticalTick()
    {
        NPCController killer = GetKiller();
        if (killer == null || !killer.IsAlive) return;

        NPCController nearestCivilian = GetNearestLivingCivilian(killer.Position);
        if (nearestCivilian != null)
        {
            killer.MoveTo(nearestCivilian.Position);
            killer.TryKill();
        }

        foreach (NPCController player in players)
        {
            if (!player.IsAlive || player.Role != PlayerRole.Civilian) continue;
            Vector2 away = player.Position - killer.Position;
            if (away.sqrMagnitude < 0.001f)
                away = new Vector2(Random.value - 0.5f, Random.value - 0.5f);
            // 目标放在当前位置沿远离杀手方向 4 个单位处，再由场地规则裁剪。
            player.MoveTo(ClampToArena(player.Position + away.normalized * 4f));
        }
    }

    /// <summary>轮流给一个存活角色请求 AI 决策，避免 5 个原生请求同时启动。</summary>
    private void RequestNextAIDecision()
    {
        for (int checkedCount = 0; checkedCount < players.Count; checkedCount++)
        {
            NPCController player = players[nextBrainIndex];
            nextBrainIndex = (nextBrainIndex + 1) % players.Count;
            if (!player.IsAlive) continue;
            NPCAgent brain = player.GetComponent<NPCAgent>();
            if (brain != null) brain.RequestDecision();
            return;
        }
    }

    public NPCController GetKiller()
    {
        foreach (NPCController player in players)
            if (player.IsAlive && player.Role == PlayerRole.Killer) return player;
        return null;
    }

    public NPCController GetNearestLivingCivilian(Vector2 position)
    {
        NPCController nearest = null;
        float bestDistance = float.MaxValue;
        foreach (NPCController player in players)
        {
            if (!player.IsAlive || player.Role != PlayerRole.Civilian) continue;
            float distance = Vector2.SqrMagnitude(player.Position - position);
            if (distance < bestDistance) { bestDistance = distance; nearest = player; }
        }
        return nearest;
    }

    /// <summary>
    /// 平民的 AI 目标只有在不会明显缩短其与杀手距离时才会被技能层接受。
    /// </summary>
    public bool IsSafeCivilianTarget(NPCController civilian, Vector2 target)
    {
        NPCController killer = GetKiller();
        if (killer == null) return true;
        float currentDistance = Vector2.Distance(civilian.Position, killer.Position);
        float targetDistance = Vector2.Distance(ClampToArena(target), killer.Position);
        return targetDistance + 0.1f >= currentDistance;
    }

    public string BuildWorldState(NPCController observer)
    {
        // 每次都从真实技能状态生成快照；LLM 不保存坐标历史，避免陈旧信息。
        StringBuilder text = new StringBuilder(512);
        text.Append("你是玩家").Append(observer.PlayerId).Append("，身份=").Append(observer.Role)
            .Append("，位置=(").Append(observer.Position.x.ToString("F2")).Append(',')
            .Append(observer.Position.y.ToString("F2")).Append(')');
        if (observer.Role == PlayerRole.Killer)
            text.Append("，击杀冷却=").Append(observer.KillCooldownRemaining.ToString("F1")).Append("秒");
        text.Append("。方形场地 x/y 范围均为 -5 到 5。玩家状态：");
        foreach (NPCController player in players)
            text.Append("[id=").Append(player.PlayerId).Append(",role=").Append(player.Role)
                .Append(",alive=").Append(player.IsAlive ? 1 : 0).Append(",x=")
                .Append(player.Position.x.ToString("F2")).Append(",y=")
                .Append(player.Position.y.ToString("F2")).Append(']');
        if (observer.Role == PlayerRole.Killer)
            text.Append("追逐平民，接近后击杀。只返回 {\"action\":\"move_to\",\"x\":数字,\"y\":数字} 或 {\"action\":\"kill\"}");
        else
            text.Append("远离杀手。只返回 {\"action\":\"move_to\",\"x\":数字,\"y\":数字}");
        return text.ToString();
    }

    private void EndRound(GameWinner winner)
    {
        if (!IsRunning) return;
        IsRunning = false;
        float duration = Time.time - startedAt;
        EventBus.Publish(new GameEndedEvent(winner, duration));
        Debug.Log($"游戏结束：{winner} 获胜，用时 {duration:F1} 秒。", this);
    }

    private void OnDisable()
    {
        // 退出 PlayMode 时先阻止新请求，再通知每个 AI 取消自己持有的请求。
        IsRunning = false;
        foreach (NPCController player in players)
        {
            if (player == null) continue;
            NPCAgent brain = player.GetComponent<NPCAgent>();
            if (brain != null) brain.Shutdown();
        }
    }

    private void CreateArenaVisual()
    {
        // LineRenderer 只负责显示边界，真正的越界限制由 ClampToArena 保证。
        LineRenderer line = new GameObject("Square Arena").AddComponent<LineRenderer>();
        line.loop = true; line.positionCount = 4; line.startWidth = line.endWidth = 0.08f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = Color.white;
        line.SetPositions(new[]
        {
            new Vector3(-arenaHalfSize.x, -arenaHalfSize.y),
            new Vector3(-arenaHalfSize.x, arenaHalfSize.y),
            new Vector3(arenaHalfSize.x, arenaHalfSize.y),
            new Vector3(arenaHalfSize.x, -arenaHalfSize.y)
        });
        Camera camera = Camera.main;
        if (camera != null && camera.orthographic) camera.orthographicSize = 6f;
    }
}
