#if UNITY_EDITOR
using System.Linq;
using LLMUnity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>一次性生成独立聊天场景，也可从 Tools/Create Simple NPC Chat Scene 手动重建。</summary>
public static class SimpleChatSceneCreator
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/SimpleChatScene.unity";

    [InitializeOnLoadMethod]
    private static void CreateWhenScriptsReload()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= CreateAfterPlayMode;
            EditorApplication.playModeStateChanged += CreateAfterPlayMode;
        }
        else
        {
            EditorApplication.delayCall += Create;
        }
    }

    private static void CreateAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= CreateAfterPlayMode;
        EditorApplication.delayCall += Create;
    }

    [MenuItem("Tools/Create Simple NPC Chat Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene))
        {
            Debug.LogError($"找不到源场景：{SourceScene}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene)) AssetDatabase.DeleteAsset(TargetScene);
        AssetDatabase.CopyAsset(SourceScene, TargetScene);
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Additive);

        // 复制源场景是为了完整保留已经验证可用的 LLM 模型配置；其余对象全部重建。
        LLM model = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LLM candidate = root.GetComponentInChildren<LLM>(true);
            if (candidate != null && model == null)
            {
                model = candidate;
                candidate.transform.SetParent(null);
                candidate.gameObject.name = "LLM";
                continue;
            }
            Object.DestroyImmediate(root);
        }
        if (model == null)
        {
            Debug.LogError("SampleScene 中没有可复制的 LLM 配置。");
            EditorSceneManager.CloseScene(scene, true);
            return;
        }

        SceneManager.MoveGameObjectToScene(model.gameObject, scene);
        CreateCamera(scene);
        CreateNPC(scene);
        CreateChatUI(scene, model);
        EditorSceneManager.SaveScene(scene, TargetScene);
        EditorSceneManager.CloseScene(scene, true);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"已创建聊天场景：{TargetScene}");
    }

    private static void CreateCamera(Scene scene)
    {
        GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        Camera camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
        go.transform.position = new Vector3(0, 0, -10);
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private static void CreateNPC(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/NPC.prefab");
        GameObject npc = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene) : new GameObject("NPC");
        npc.name = "Chat NPC";
        npc.transform.position = new Vector3(0, 1.25f, 0);
        // 这个场景只保留 Transform 与 SpriteRenderer 外观，彻底移除追杀玩法、
        // 自动决策和预制体 LLMAgent，而不只是暂时禁用它们。
        foreach (MonoBehaviour behaviour in npc.GetComponents<MonoBehaviour>())
            Object.DestroyImmediate(behaviour, true);
    }

    private static void CreateChatUI(Scene scene, LLM model)
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, scene);

        GameObject canvasObject = new GameObject("Chat Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        TMP_Text reply = CreateText(canvas.transform, "AI Reply", "你好，我是场景中的 AI NPC。", 28,
            new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.48f));
        reply.alignment = TextAlignmentOptions.TopLeft;

        TMP_InputField input = CreateInput(canvas.transform);
        Button button = CreateButton(canvas.transform);

        GameObject runtime = new GameObject("Chat Runtime", typeof(LLMAgent), typeof(SimpleNPCChat));
        SceneManager.MoveGameObjectToScene(runtime, scene);
        LLMAgent agent = runtime.GetComponent<LLMAgent>();
        agent.llm = model;
        agent.slot = -1;
        agent.numPredict = 256;
        agent.systemPrompt = "你是游戏中的友好NPC。请使用简洁自然的中文回答玩家，不要输出JSON或Markdown。";

        SerializedObject serialized = new SerializedObject(runtime.GetComponent<SimpleNPCChat>());
        serialized.FindProperty("agent").objectReferenceValue = agent;
        serialized.FindProperty("inputField").objectReferenceValue = input;
        serialized.FindProperty("replyText").objectReferenceValue = reply;
        serialized.FindProperty("sendButton").objectReferenceValue = button;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size; text.color = Color.white; text.enableWordWrapping = true;
        return text;
    }

    private static TMP_InputField CreateInput(Transform parent)
    {
        GameObject root = new GameObject("Message Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.08f, 0.07f); rect.anchorMax = new Vector2(0.76f, 0.16f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(1, 1, 1, 0.12f);
        TMP_Text text = CreateText(root.transform, "Text", "", 24, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(18, 8); text.rectTransform.offsetMax = new Vector2(-18, -8);
        TMP_Text placeholder = CreateText(root.transform, "Placeholder", "输入内容并按回车……", 24, Vector2.zero, Vector2.one);
        placeholder.color = new Color(1, 1, 1, 0.45f);
        placeholder.rectTransform.offsetMin = new Vector2(18, 8); placeholder.rectTransform.offsetMax = new Vector2(-18, -8);
        TMP_InputField field = root.GetComponent<TMP_InputField>();
        field.textComponent = text; field.placeholder = placeholder; field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    private static Button CreateButton(Transform parent)
    {
        GameObject root = new GameObject("Send Button", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.78f, 0.07f); rect.anchorMax = new Vector2(0.92f, 0.16f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.16f, 0.48f, 0.92f);
        TMP_Text label = CreateText(root.transform, "Label", "发送", 24, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        return root.GetComponent<Button>();
    }

    private static void AddToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(item => item.path == TargetScene)) return;
        EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(TargetScene, true) }).ToArray();
    }
}
#endif
