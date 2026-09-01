using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在场景加载后整理对话 UI，并创建退出按钮和 AI 思考状态提示。
/// 采用运行时初始化，不需要在 Inspector 中额外绑定引用。
/// </summary>
public class GameUIController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.92f);
    private static readonly Color InputColor = new Color(0.08f, 0.11f, 0.16f, 0.96f);
    private static readonly Color AccentColor = new Color(0.24f, 0.67f, 0.95f, 1f);
    private static readonly Color MutedColor = new Color(0.68f, 0.76f, 0.86f, 1f);

    private NPCAgent agent;
    private TMP_InputField inputField;
    private TMP_Text thinkingText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null || canvas.GetComponent<GameUIController>() != null) return;
        canvas.gameObject.AddComponent<GameUIController>();
    }

    private void Awake()
    {
        agent = FindObjectOfType<NPCAgent>();
        inputField = FindObjectOfType<TMP_InputField>();
        TMP_Text dialogueText = FindDialogueText();

        StyleInputField();
        CreateDialoguePanel(dialogueText);
        CreateExitButton();

        if (agent != null)
        {
            agent.ThinkingChanged += SetThinking;
            SetThinking(agent.IsThinking);
        }
        else
        {
            SetThinking(false);
        }
    }

    /// <summary>找到用于显示 NPC 回复的文本，排除输入框自身的文字与占位符。</summary>
    private TMP_Text FindDialogueText()
    {
        foreach (TMP_Text candidate in GetComponentsInChildren<TMP_Text>(true))
        {
            if (candidate.name == "NPCText") return candidate;
        }
        return null;
    }

    private void CreateDialoguePanel(TMP_Text dialogueText)
    {
        GameObject panel = CreateUiObject("DialoguePanel", transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -34f);
        panelRect.sizeDelta = new Vector2(900f, 190f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = PanelColor;

        if (dialogueText != null)
        {
            dialogueText.transform.SetParent(panel.transform, false);
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(30f, 24f);
            textRect.offsetMax = new Vector2(-30f, -48f);
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.fontSize = 30f;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = 20f;
            dialogueText.fontSizeMax = 34f;
            dialogueText.color = Color.white;
            dialogueText.raycastTarget = false;
        }

        thinkingText = CreateText("ThinkingText", panel.transform, dialogueText, 22f);
        RectTransform statusRect = thinkingText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 14f);
        statusRect.sizeDelta = new Vector2(-60f, 28f);
        thinkingText.alignment = TextAlignmentOptions.Left;
        thinkingText.color = AccentColor;
    }

    private void StyleInputField()
    {
        if (inputField == null) return;

        RectTransform rect = inputField.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 32f);
        rect.sizeDelta = new Vector2(900f, 72f);

        Image background = inputField.GetComponent<Image>();
        if (background != null) background.color = InputColor;

        if (inputField.textComponent != null)
        {
            inputField.textComponent.color = Color.white;
            inputField.textComponent.fontSize = 25f;
        }

        if (inputField.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "输入你想对 NPC 说的话，按 Enter 发送...";
            placeholder.color = MutedColor;
            placeholder.fontSize = 23f;
        }
    }

    private void CreateExitButton()
    {
        GameObject buttonObject = CreateUiObject("ExitButton", transform);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-28f, -28f);
        rect.sizeDelta = new Vector2(150f, 56f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.72f, 0.18f, 0.22f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(QuitGame);

        TMP_Text label = CreateText("Label", buttonObject.transform, thinkingText, 24f);
        label.text = "退出游戏";
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
    }

    private void SetThinking(bool isThinking)
    {
        if (thinkingText != null)
            thinkingText.text = isThinking ? "AI 思考中..." : "AI 已就绪";

        if (inputField != null)
            inputField.interactable = !isThinking;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, TMP_Text fontSource, float fontSize)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (fontSource != null) text.font = fontSource.font;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (agent != null) agent.ThinkingChanged -= SetThinking;
    }
}
