using UnityEngine;

/// <summary>一局中玩家的隐藏/规则身份。</summary>
public enum PlayerRole { Civilian, Killer }
/// <summary>游戏结束时的获胜阵营。</summary>
public enum GameWinner { Civilians, Killer }

/// <summary>完成角色创建和身份抽取后发布。</summary>
public readonly struct GameStartedEvent
{
    public readonly int PlayerCount;
    public readonly int KillerId;
    public GameStartedEvent(int playerCount, int killerId) { PlayerCount = playerCount; KillerId = killerId; }
}

/// <summary>杀手成功击杀最近平民后发布，可供 UI、音效和统计订阅。</summary>
public readonly struct PlayerKilledEvent
{
    public readonly int KillerId;
    public readonly int VictimId;
    public readonly Vector2 Position;
    public PlayerKilledEvent(int killerId, int victimId, Vector2 position)
    { KillerId = killerId; VictimId = victimId; Position = position; }
}

/// <summary>任一阵营满足胜利条件后发布。</summary>
public readonly struct GameEndedEvent
{
    public readonly GameWinner Winner;
    public readonly float Duration;
    public GameEndedEvent(GameWinner winner, float duration) { Winner = winner; Duration = duration; }
}
