using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>保存场景物品资料，并生成可被 JsonUtility 正确序列化的 JSON。</summary>
public class GameWorldMes : MonoBehaviour
{
    private static GameWorldMes instance;
    public static GameWorldMes Instance => instance;

    [Tooltip("当前场景中需要发送给 AI 的物品列表")]
    public List<WorldItem> worldObjPos = new List<WorldItem>();

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(this);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// 顶层 List 和 ScriptableObject 引用不适合直接交给 JsonUtility，
    /// 因此先转换成普通数据对象，再通过包含 items 字段的根对象序列化。
    /// </summary>
    public string ToJson()
    {
        WorldStateData state = new WorldStateData();

        foreach (WorldItem item in worldObjPos)
        {
            if (item == null) continue;

            state.items.Add(new WorldItemData
            {
                name = item.Name,
                description = item.Description,
                x = item.pos.x,
                y = item.pos.y
            });
        }

        return JsonUtility.ToJson(state);
    }
}

/// <summary>JSON 根对象；JsonUtility 要求根节点是对象，不能直接是 List。</summary>
[Serializable]
public class WorldStateData
{
    public List<WorldItemData> items = new List<WorldItemData>();
}

/// <summary>发送给 AI 的单个场景物品快照。</summary>
[Serializable]
public class WorldItemData
{
    public string name;
    public string description;
    public float x;
    public float y;
}
