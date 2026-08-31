[System.Serializable]
/// <summary>
/// LLM 决策 JSON 的最小数据模型。
/// move_to 使用 x/y；kill 不需要额外字段；message 预留给后续对话玩法。
/// </summary>
public class ReturnMes
{
    public string action;
    public float x;
    public float y;
    public string message;
}
