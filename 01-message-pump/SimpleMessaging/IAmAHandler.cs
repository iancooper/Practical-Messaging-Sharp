using RabbitMQ.Client;

namespace SimpleMessaging;

/// <summary>
/// The contract your application code implements so the pump can dispatch to it.
/// </summary>
public interface IAmAHandler<T> where T : IAmAMessage
{
    Task Handle(BasicGetResult delivery);
}
