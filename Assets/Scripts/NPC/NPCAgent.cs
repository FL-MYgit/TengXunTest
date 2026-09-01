using LLMUnity;
using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>单 NPC 的非流式模型交互脚本。</summary>
public class NPCAgent : MonoBehaviour
{
    [SerializeField] private LLMAgent agent; //LLMAgent引用
    [SerializeField] private NPCController controller; //NPCControlleryinyong
    [Min(0)] public int maxResendTime = 3; //最大重传次数
    public string CurrentMessage; //当前传输的信息
    private bool requestInProgress; //AI当前是否正在思考
    private bool shuttingDown; //该对象是否被禁用或销毁

    private void Awake()
    {
        if (agent == null) agent = GetComponent<LLMAgent>();
        if (controller == null) controller = GetComponent<NPCController>();
    }

    /// <summary>
    /// 向AI发送信息（外部调用）
    /// </summary>
    /// <param name="message"></param>
    public async void SendMessageToAI(string message)
    {
        if (requestInProgress || shuttingDown || string.IsNullOrWhiteSpace(message)) return;

        if (agent == null) { Debug.LogError("NPCAgent 没有绑定 LLMAgent。", this); return; }

        requestInProgress = true;
        CurrentMessage = message.Trim();

        try 
        { 
            await SendWithLimitedRetries(CurrentMessage); 
        }catch (Exception exception)
        {
            if (!shuttingDown) Debug.LogWarning($"AI 请求失败：{exception.Message}", this);
        }
        finally { requestInProgress = false; }
    }

    /// <summary>
    /// 尝试向AI发送消息
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task SendWithLimitedRetries(string message)
    {
        // 第一次不算重传，总请求次数最多为 maxResendTime + 1。
        int maximumAttempts = Mathf.Max(0, maxResendTime) + 1;

        // GameWorldMes 会把 ScriptableObject 列表转换为带 items 根节点的有效 JSON。
        string worldJson = GameWorldMes.Instance != null
            ? GameWorldMes.Instance.ToJson()
            : "{\"items\":[]}";

        string fullMessage =
            "当前场景物品信息：\n" + worldJson +
            "\n玩家输入：\n" + message;

        // 在真正发起请求前输出，方便确认模型收到的完整内容。
        Debug.Log($"发送信息：{fullMessage}", this);

        for (int attempt = 0; attempt < maximumAttempts && !shuttingDown; attempt++)
        {
            // 关闭流式输出，只接收最终完整回复。
            string reply = await agent.Chat(fullMessage, null, null, false);

            CurrentMessage = reply;

            Debug.Log($"AI 回复：{reply}", this);

            if (TryExecuteReply(reply)) return;

            if (attempt + 1 < maximumAttempts)
                Debug.LogWarning($"AI 回复解析失败，准备第 {attempt + 1} 次重传。", this);
        }
        if (!shuttingDown)
            Debug.LogWarning($"AI 回复解析失败，已达到最大重传次数 {maxResendTime}。", this);
    }

    /// <summary>
    /// 尝试解析AI回复
    /// </summary>
    /// <param name="reply"></param>
    /// <returns></returns>
    private bool TryExecuteReply(string reply)
    {
        //返回值判空
        if (string.IsNullOrWhiteSpace(reply)) return false;

        try
        {
            ReturnMes result = JsonUtility.FromJson<ReturnMes>(reply);
            if (result == null || string.IsNullOrWhiteSpace(result.action)) return false;

            switch (result.action.Trim().ToLowerInvariant())
            {
                case "move_to": 
                    controller?.MoveTo(new Vector2(result.x, result.y));
                    controller?.Say(result.message);
                    break;
                case "say": 
                    controller?.Say(result.message); 
                    break;
                case "idle": 
                    break;
                default: 
                    return false;
            }
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// 当对象被禁用时
    /// </summary>
    private void OnDisable()
    {
        shuttingDown = true;

        if (!requestInProgress || agent == null) return;

        try { 
            //取消发送与返回
            agent.CancelRequests();
        }catch (Exception e) {
            Debug.LogError($"错误：{e},取消发送/返回失败");
        }
    }
}
