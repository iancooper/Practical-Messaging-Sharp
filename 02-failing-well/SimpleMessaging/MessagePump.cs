using System.Text;

namespace SimpleMessaging;

/// <summary>
/// The Message Pump: Get -> Translate -> Dispatch -> Handle, in a loop, until cancelled.
///
/// ---------------------------------------------------------------------------------------
///  THIS IS EXERCISE 1's ANSWER, AND EXERCISE 2's PROBLEM.
///
///  Everything exercise 1 asked for is here and correct:
///    - the Translate stage is back, and it goes through a Message Mapper
///    - the handler takes a domain type; nothing in Model/ knows a broker exists
///    - the acknowledgement happens after the work, not before it
///    - a failure no longer kills the loop
///
///  So it does not lose messages any more, and it does not fall over. It is still wrong, and
///  the way it is wrong is worse than falling over, because it will not show up in your logs
///  as a crash. Read PROBE.md.
/// ---------------------------------------------------------------------------------------
/// </summary>
public sealed class MessagePump<T> where T : IAmAMessage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IAmAMessageMapper<T> _mapper;
    private readonly IAmAHandler<T> _handler;
    private readonly string _hostName;

    public MessagePump(IAmAMessageMapper<T> mapper, IAmAHandler<T> handler, string hostName = "localhost")
    {
        _mapper = mapper;
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
                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            try
            {
                // TRANSLATE
                var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
                var message = _mapper.MapToRequest(body);

                // DISPATCH and HANDLE
                await _handler.Handle(message);

                // Only now are we done with it.
                await consumer.Acknowledge(delivery.DeliveryTag);
            }
            catch (Exception e)
            {
                // Something went wrong, and we must not lose the message. Put it back on the
                // queue so it gets tried again.
                Console.WriteLine($"  FAILED: {e.Message} -- putting it back");
                await consumer.Requeue(delivery.DeliveryTag);
            }
        }
    }
}
