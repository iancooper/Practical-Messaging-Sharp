using System.Text;

namespace SimpleMessaging;

/// <summary>
/// The Message Pump: Get -> Translate -> Dispatch -> Handle, in a loop, until cancelled.
///
/// ---------------------------------------------------------------------------------------
///  THIS IS EXERCISE 2's ANSWER, AND IT IS CORRECT. Nothing here needs fixing.
///
///    - Translate goes through a Message Mapper; the handler takes a domain type
///    - the acknowledgement happens after the work, not before it
///    - a body we cannot read goes to the invalid message queue, and is never retried
///    - work that failed is retried with a delay, up to a limit, then dead-lettered
///    - the retry count comes from RabbitMQ's x-death header; we count nothing ourselves
///
///  Every one of those five is something the broker does for you. Exercise 3 is about what
///  happens to this list when the channel is a stream instead of a queue.
///
///  There is one line in here that is now a problem, and it is not a problem with the pump.
///  Read PROBE.md.
/// ---------------------------------------------------------------------------------------
/// </summary>
public sealed class MessagePump<T> where T : IAmAMessage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>How many times we retry before giving up. This is n.</summary>
    private const int MaxRetries = 3;

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
            catch (UnmappableMessageException e)
            {
                // A failure to UNDERSTAND. The bytes are not going to change, so there is
                // nothing to retry -- retrying this is the definition of a poison pill.
                // Publish it to the invalid message queue, where someone can go and look at
                // it. A reject would send it to the *retry* queue, which is the one thing this
                // message must never go to.
                Console.WriteLine($"  INVALID: {e.Message}");
                Console.WriteLine($"  -> {Channel.InvalidQueueNameFor<T>()}");
                await consumer.SendToInvalidMessageQueue(delivery);
                await consumer.Acknowledge(delivery.DeliveryTag);
            }
            catch (Exception e)
            {
                // A failure to PROCESS. The message was perfectly readable; the work failed.
                // That may have been bad luck, so it is worth trying again -- but not forever.
                var retries = DataTypeChannelConsumer<T>.RetriesSoFar(delivery);

                if (retries < MaxRetries)
                {
                    Console.WriteLine($"  FAILED on attempt {retries + 1}: {e.Message}");
                    Console.WriteLine($"  -> retrying in {Channel.RetryDelay.TotalSeconds:0}s");
                    await consumer.RejectForRetry(delivery.DeliveryTag);
                }
                else
                {
                    Console.WriteLine($"  GIVING UP after {retries + 1} attempts: {e.Message}");
                    Console.WriteLine($"  -> {Channel.DeadLetterQueueNameFor<T>()}");
                    await consumer.SendToDeadLetter(delivery);
                    await consumer.Acknowledge(delivery.DeliveryTag);
                }
            }
        }
    }
}
