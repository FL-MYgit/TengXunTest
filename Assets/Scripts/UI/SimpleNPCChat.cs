using LLMUnity;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单 NPC 聊天场景控制器。
/// 用户按回车、输入框失去焦点或点击发送按钮后，把完整输入发送给本地模型；
/// 本类不注册流式回调，只在生成全部完成后一次性更新回复文本。
/// </summary>
public class SimpleNPCChat : MonoBehaviour
{
    [SerializeField] private LLMAgent agent;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text replyText;
    [SerializeField] private Button sendButton;

    private bool requestInFlight;
    private bool shuttingDown;

    private void Awake()
    {
        inputField.onEndEdit.AddListener(OnInputFinished);
        sendButton.onClick.AddListener(SendCurrentInput);
        replyText.text = "你好，我是场景中的 AI NPC。";
    }

    private void OnInputFinished(string value)
    {
        // onEndEdit 也会在点击按钮导致输入框失焦时触发；忙碌标记可避免重复发送。
        if (!string.IsNullOrWhiteSpace(value)) _ = SendAsync(value);
    }

    public void SendCurrentInput()
    {
        if (!string.IsNullOrWhiteSpace(inputField.text)) _ = SendAsync(inputField.text);
    }

    private async Task SendAsync(string message)
    {
        if (requestInFlight || shuttingDown || agent == null) return;
        requestInFlight = true;
        sendButton.interactable = false;
        inputField.interactable = false;
        replyText.text = "AI 正在思考……";

        try
        {
            // LLMAgent.Start 是异步初始化；最多等待 60 秒，避免初始化窗口内进入原生 Chat。
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!shuttingDown && agent.llmAgent == null && Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            if (shuttingDown) return;
            if (agent.llmAgent == null) throw new TimeoutException("LLM 初始化超时");

            // callback=null 关闭流式输出；完整回复生成后才更新 UI。
            string answer = await agent.Chat(message.Trim(), null, null, true);
            if (!shuttingDown && this != null)
                replyText.text = string.IsNullOrWhiteSpace(answer) ? "AI 没有返回内容。" : answer.Trim();
        }
        catch (Exception exception)
        {
            if (!shuttingDown && this != null) replyText.text = $"发送失败：{exception.Message}";
        }
        finally
        {
            if (!shuttingDown && this != null)
            {
                inputField.text = string.Empty;
                inputField.interactable = true;
                sendButton.interactable = true;
                inputField.ActivateInputField();
            }
            requestInFlight = false;
        }
    }

    private void OnDisable()
    {
        shuttingDown = true;
        inputField?.onEndEdit.RemoveListener(OnInputFinished);
        sendButton?.onClick.RemoveListener(SendCurrentInput);
        if (requestInFlight && agent != null)
        {
            try { agent.CancelRequests(); }
            catch (Exception) { }
        }
    }
}
