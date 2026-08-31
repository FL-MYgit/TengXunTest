using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 进程内的轻量级强类型事件总线。
/// 发布者只需要知道事件数据类型，不需要持有接收者引用，适合解耦游戏规则、
/// NPC 表现、UI、音效和统计系统。
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

    /// <summary>订阅 T 类型事件。组件应在 OnDisable 中调用 Unsubscribe。</summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        Handlers.TryGetValue(typeof(T), out Delegate current);
        Handlers[typeof(T)] = Delegate.Combine(current, handler);
    }

    /// <summary>取消指定监听器；不存在时安全返回。</summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (!Handlers.TryGetValue(typeof(T), out Delegate current)) return;
        Delegate remaining = Delegate.Remove(current, handler);
        if (remaining == null) Handlers.Remove(typeof(T));
        else Handlers[typeof(T)] = remaining;
    }

    /// <summary>
    /// 同步发布事件。逐个保护监听器，某个 UI 监听器抛异常不会中断游戏规则。
    /// </summary>
    public static void Publish<T>(T message)
    {
        if (!Handlers.TryGetValue(typeof(T), out Delegate current)) return;
        foreach (Delegate handler in current.GetInvocationList())
        {
            try { ((Action<T>)handler).Invoke(message); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // 关闭 Domain Reload 时静态字段不会自动清空，因此每次进入 Play Mode 主动重置。
    private static void Reset() => Handlers.Clear();
}
