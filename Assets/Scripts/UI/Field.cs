using TMPro;
using UnityEngine;

/// <summary>把输入框结束编辑时的文本交给单 NPC AI。</summary>
public class Field : MonoBehaviour
{
    [SerializeField] private TMP_InputField field;
    [SerializeField] private NPCAgent agent;

    private void Awake()
    {
        if (field == null) field = GetComponent<TMP_InputField>();
        if (field != null) field.onEndEdit.AddListener(SendMessageTo);
    }

    public void SendMessageTo(string message)
    {
        if (agent == null || string.IsNullOrWhiteSpace(message)) return;
        agent.SendMessageToAI(message);
        field.text = string.Empty;
    }

    private void OnDestroy()
    {
        if (field != null) field.onEndEdit.RemoveListener(SendMessageTo);
    }
}
