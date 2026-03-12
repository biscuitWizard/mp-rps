using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central event bus that acts as middleware between game systems and the
/// network layer. Supports three interaction patterns:
///
/// 1. Local request/response — Subscribe + Publish with a synchronous handler.
/// 2. Network request/response — Publish with a callback; the message goes
///    over the wire and the callback fires when the nonce-matched reply arrives.
/// 3. General subscription — SubscribeResponse listens for any inbound message
///    of a given type regardless of nonce (server-pushed events, broadcasts, etc).
///
/// The nonce on each message ties a specific request to its response, similar
/// to how axios correlates HTTP requests. When no nonce match exists the message
/// is still broadcast to general subscribers, keeping the system extensible.
/// </summary>
public class EventBus : MonoBehaviour
{
    private static EventBus _instance;

    public static EventBus Instance
    {
        get
        {
            if (_instance == null)
            {
                var obj = new GameObject("EventBus");
                _instance = obj.AddComponent<EventBus>();
            }
            return _instance;
        }
    }

    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Dictionary<Type, List<Delegate>> _responseSubscribers = new();
    private readonly Dictionary<string, Delegate> _pendingCallbacks = new();
    private ITransport _transport;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Wire up the network transport. Called once by NetworkManager during startup.
    /// </summary>
    public void SetTransport(ITransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Register a local handler that can synchronously produce a response
    /// for a given request type. Useful for in-process mocking or local-only logic.
    /// </summary>
    public void Subscribe<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        where TRequest : MessageBase
        where TResponse : MessageBase
    {
        var type = typeof(TRequest);

        if (!_subscribers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _subscribers[type] = list;
        }

        list.Add(handler);
    }

    /// <summary>
    /// Listen for any inbound message of a given type, regardless of nonce.
    /// This is the "general subscription" pattern — fire-and-forget server
    /// pushes like RoundResultMessage land here.
    /// </summary>
    public void SubscribeResponse<TResponse>(Action<TResponse> handler)
        where TResponse : MessageBase
    {
        var type = typeof(TResponse);

        if (!_responseSubscribers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _responseSubscribers[type] = list;
        }

        list.Add(handler);
    }

    /// <summary>
    /// Publish a request that expects a typed response. Local handlers are
    /// tried first; if none produce a result and a network transport is
    /// available the message is serialized and sent over the wire. The
    /// callback (if provided) is stored keyed by nonce and invoked when
    /// the matching response arrives from the server.
    /// </summary>
    public void Publish<TRequest, TResponse>(
        TRequest payload,
        Action<TResponse> callback = null,
        float timeout = 0f)
        where TRequest : MessageBase
        where TResponse : MessageBase
    {
        var type = typeof(TRequest);
        bool handled = false;

        if (_subscribers.TryGetValue(type, out var list))
        {
            foreach (var d in list)
            {
                var handler = (Func<TRequest, TResponse>)d;
                var response = handler(payload);
                if (response == null) continue;

                callback?.Invoke(response);
                BroadcastResponse(response);
                handled = true;
            }
        }

        if (!handled && _transport != null)
        {
            if (callback != null)
            {
                _pendingCallbacks[payload.Nonce] = callback;

                if (timeout > 0f)
                    StartCoroutine(TimeoutCoroutine(payload.Nonce, timeout));
            }

            _transport.Send(payload);
        }
    }

    /// <summary>
    /// Fire-and-forget: send a message over the network with no expected
    /// response callback. Responses may still arrive via general subscription.
    /// </summary>
    public void Send<TRequest>(TRequest payload) where TRequest : MessageBase
    {
        if (_transport != null)
            _transport.Send(payload);
        else
            Debug.LogWarning("[EventBus] No transport set; cannot send message");
    }

    /// <summary>
    /// Called by the GameClient when a message arrives from the server.
    /// Checks for a nonce-matched pending callback first (request/response),
    /// then broadcasts to general response subscribers.
    /// </summary>
    public void ReceiveMessage(MessageBase message)
    {
        if (!string.IsNullOrEmpty(message.Nonce) &&
            _pendingCallbacks.TryGetValue(message.Nonce, out var pending))
        {
            _pendingCallbacks.Remove(message.Nonce);
            pending.DynamicInvoke(message);
        }

        BroadcastToSubscribers(message);
    }

    private void BroadcastResponse<TResponse>(TResponse response)
        where TResponse : MessageBase
    {
        var type = typeof(TResponse);

        if (!_responseSubscribers.TryGetValue(type, out var list))
            return;

        foreach (var d in list)
        {
            var handler = (Action<TResponse>)d;
            handler(response);
        }
    }

    private void BroadcastToSubscribers(MessageBase message)
    {
        var type = message.GetType();

        if (!_responseSubscribers.TryGetValue(type, out var list))
            return;

        foreach (var d in list)
            d.DynamicInvoke(message);
    }

    private IEnumerator TimeoutCoroutine(string nonce, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (_pendingCallbacks.Remove(nonce))
            Debug.LogWarning($"[EventBus] Request timed out (nonce: {nonce})");
    }
}
