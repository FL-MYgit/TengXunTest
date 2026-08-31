using LLMUnity;
using System;
using UnityEngine;

/// <summary>
/// NPC 的 AI 决策适配器。
///
/// 职责只有三个：
/// 1. 从 AIGameManager 获取当前世界状态；
/// 2. 将状态发送给 LLM，并解析一个完整的 JSON 回复；
/// 3. 把合法动作转交给 NPCController（技能层）执行。
///
/// 本类不直接改变坐标或击杀玩家，因此模型无法绕过速度、边界、身份、
/// 击杀范围和冷却时间等游戏规则。
/// </summary>
public class NPCAgent : MonoBehaviour
{
    [Tooltip("同一对象上的 LLMUnity LLMAgent 组件。")]
    [SerializeField] private LLMAgent agent;

    [Tooltip("同一对象上的 AI 技能组件。")]
    [SerializeField] private NPCController skills;

    private AIGameManager gameManager;
    private bool configured;
    private bool shuttingDown;
    private bool ownsActiveRequest;

    // LlamaLib 在同一服务上同时进行多个首次构造/推理时可能发生原生崩溃。
    // Unity 主线程访问此标记，因此无需额外的线程锁。
    private static bool globalRequestInFlight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        globalRequestInFlight = false;
    }

    private void Awake()
    {
        agent = GetComponent<LLMAgent>();
        skills = GetComponent<NPCController>();
    }

    /// <summary>
    /// 由 AIGameManager 在每局开始时调用，注入本角色的技能组件和游戏状态源。
    /// 所有角色共享服务的 slot 0，因为模型请求由 GameManager 串行调度。
    /// </summary>
    public void Configure(NPCController npcSkills, AIGameManager manager, LLMAgent sharedModelAgent)
    {
        skills = npcSkills;
        gameManager = manager;
        agent = sharedModelAgent;
        configured = true;
        shuttingDown = false;

        if (agent == null) return;
        // -1 让 LLMUnity 自动分配唯一可用槽位。所有角色共享本组件，且请求串行。
        agent.slot = -1;
        agent.systemPrompt =
            "你是方形场地生存游戏中的AI玩家。根据全部玩家位置选择动作。" +
            "只输出一个JSON对象，不要Markdown，不要解释。";
        // 决策 JSON 很短，限制生成长度可以显著降低下一次决策的等待时间。
        agent.numPredict = 32;
    }

    /// <summary>
    /// 尝试发起一次模型决策。
    /// 若另一个 NPC 正在使用模型，本次调度直接跳过，不排队、不堆积任务。
    /// </summary>
    public async void RequestDecision()
    {
        if (!CanRequest() || globalRequestInFlight) return;

        globalRequestInFlight = true;
        ownsActiveRequest = true;
        try
        {
            string prompt = gameManager.BuildWorldState(skills);
            // callback=null：关闭流式处理。
            // addToHistory=false：实时坐标不会持续填满对话上下文。
            string reply = await agent.Chat(prompt, null, null, false);

            // await 返回时可能已经退出 Play Mode，必须再次检查生命周期。
            if (!shuttingDown && this != null && isActiveAndEnabled &&
                gameManager != null && gameManager.IsRunning && skills != null && skills.IsAlive)
            {
                ExecuteDecision(reply);
            }
        }
        catch (Exception exception)
        {
            // CancelRequests 在退出 Play Mode 时引发的异常属于正常取消，不输出错误。
            if (!shuttingDown && this != null)
                Debug.LogWarning($"玩家 {skills?.PlayerId} AI 决策失败：{exception.Message}", this);
        }
        finally
        {
            ownsActiveRequest = false;
            globalRequestInFlight = false;
        }
    }

    private bool CanRequest()
    {
        // LLMAgent.Start 是 async void；模型服务已存在并不代表底层 llmAgent 已构造。
        // 等待该对象非空可避免在初始化窗口内调用原生 Chat 导致访问违规。
        return configured && !shuttingDown && isActiveAndEnabled && agent != null && agent.llmAgent != null &&
               skills != null && skills.IsAlive && gameManager != null && gameManager.IsRunning;
    }

    /// <summary>解析模型动作，并且只调用技能层公开的安全接口。</summary>
    private void ExecuteDecision(string reply)
    {
        if (!TryExtractJson(reply, out string json)) return;
        try
        {
            ReturnMes decision = JsonUtility.FromJson<ReturnMes>(json);
            if (decision == null || string.IsNullOrWhiteSpace(decision.action)) return;

            switch (decision.action.Trim().ToLowerInvariant())
            {
                case "move_to":
                    skills.MoveTo(new Vector2(decision.x, decision.y));
                    break;
                case "kill":
                case "attack":
                    skills.TryKill();
                    break;
            }
        }
        catch (Exception exception)
        {
            if (!shuttingDown)
                Debug.LogWarning($"玩家 {skills.PlayerId} 返回了无效 JSON：{exception.Message}", this);
        }
    }

    /// <summary>
    /// 容忍模型偶尔添加的前后文本，只截取最外层 JSON 对象。
    /// 找不到完整花括号时直接忽略，不发起递归重试。
    /// </summary>
    private static bool TryExtractJson(string reply, out string json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(reply)) return false;
        int start = reply.IndexOf('{');
        int end = reply.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        json = reply.Substring(start, end - start + 1);
        return true;
    }

    /// <summary>
    /// 停止角色 AI。退出 Play Mode 和管理器销毁时都会调用。
    /// 只有真正持有请求的角色才取消原生请求，避免其他 NPC 相互干扰。
    /// </summary>
    public void Shutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        configured = false;
        if (ownsActiveRequest && agent != null)
        {
            try { agent.CancelRequests(); }
            catch (Exception) { /* 原生服务可能已经开始销毁，安全忽略。 */ }
        }
    }

    private void OnDisable() => Shutdown();
    private void OnDestroy() => Shutdown();

    /// <summary>保留旧 UI 的手动测试入口；仍然经过全局串行保护。</summary>
    public void SendMessageToAI(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) RequestDecision();
    }
}
