/// <summary>
/// Abstraction over the network transport layer. Implemented by GameClient,
/// consumed by the EventBus. Keeps the event system decoupled from TCP
/// specifics and makes it easy to swap in a mock for testing.
/// </summary>
public interface ITransport
{
    void Send(MessageBase message);
}
