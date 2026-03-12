using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Routes incoming messages to their registered handlers based on the message
/// type discriminator string. Handlers are registered during Initialize() and
/// invoked on the Unity main thread.
/// </summary>
public class MessageDispatcher
{
    private readonly Dictionary<string, IMessageHandler> _handlers = new();

    /// <summary>
    /// Register all built-in handlers. Called once at server startup.
    /// Add new Register lines here when you create new message types.
    /// </summary>
    public void Initialize()
    {
        Register<LoginMessage>(new LoginHandler());
        Register<PlayCardMessage>(new PlayCardHandler());
    }

    public void Register<T>(IMessageHandler handler) where T : MessageBase
    {
        _handlers[typeof(T).Name] = handler;
    }

    /// <summary>
    /// Deserialize a raw JSON line and route it to the appropriate handler.
    /// Returns the handler's response messages, or null if unhandled.
    /// </summary>
    public MessageBase[] Dispatch(ClientSession sender, string json)
    {
        var message = MessageRegistry.Deserialize(json);
        if (message == null)
        {
            Debug.LogWarning($"[MessageDispatcher] Failed to deserialize: {json}");
            return null;
        }

        if (!_handlers.TryGetValue(message.Type, out var handler))
        {
            Debug.LogWarning($"[MessageDispatcher] No handler for '{message.Type}'");
            return null;
        }

        try
        {
            return handler.Handle(sender, message);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MessageDispatcher] Handler error for '{message.Type}': {e}");
            return new MessageBase[]
            {
                new ErrorMessage { Nonce = message.Nonce, ErrorText = e.Message }
            };
        }
    }
}
