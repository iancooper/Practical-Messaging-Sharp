namespace SimpleMessaging;

/// <summary>
/// The Message Pump: take a message off a channel, get it to application code, repeat
/// until cancelled.
///
///     Get -> Translate -> Dispatch -> Handle
///
/// Each of those four stages fails in its own way, which is why a message that is never going
/// to be handled has four different places it can end up.
///
/// ---------------------------------------------------------------------------------------
///  THIS PUMP IS THE EXERCISE.
///
///  It compiles, it runs, and messages flow through it. It is also wrong, in more than one
///  way, and every way it is wrong is something that has shipped to production somewhere.
///
///  Read it before you run it. Then read PROBE.md. Do not copy this file into anything.
/// ---------------------------------------------------------------------------------------
/// </summary>
public sealed class MessagePump<T> where T : IAmAMessage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IAmAHandler<T> _handler;
    private readonly string _hostName;

    public MessagePump(IAmAHandler<T> handler, string hostName = "localhost")
    {
        _handler = handler;
        _hostName = hostName;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        await using var consumer = await DataTypeChannelConsumer<T>.CreateAsync(_hostName);

        Console.WriteLine($"Pump running on {Channel.QueueNameFor<T>()}");

        while (!cancellationToken.IsCancellationRequested)
        {
            // GET
            var delivery = await consumer.Receive();

            if (delivery is null)
            {
                // Nothing there. Yield, so a Polling Consumer does not spin the CPU.
                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            Console.WriteLine($"Got delivery {delivery.DeliveryTag}");

            // We have the message in our hands, so the broker does not need to hold it for us
            // any more. Tell it we are done and let it free the slot.
            await consumer.Acknowledge(delivery.DeliveryTag);

            // TRANSLATE, DISPATCH and HANDLE
            await _handler.Handle(delivery);
        }
    }
}
