/// <summary>
/// Server-side handler interface. Implementations process a specific message type
/// and return zero or more response messages to send back to the client.
/// </summary>
public interface IMessageHandler
{
    MessageBase[] Handle(ClientSession sender, MessageBase message);
}

/// <summary>
/// Typed convenience base class. Handles the downcast so concrete handlers
/// only deal with their specific message type.
/// </summary>
public abstract class MessageHandler<T> : IMessageHandler where T : MessageBase
{
    public MessageBase[] Handle(ClientSession sender, MessageBase message)
    {
        return HandleMessage(sender, (T)message);
    }

    protected abstract MessageBase[] HandleMessage(ClientSession sender, T message);
}
