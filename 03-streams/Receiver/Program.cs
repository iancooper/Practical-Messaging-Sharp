using Model;
using SimpleEventing;
using SimpleMessaging;

// A command in on a queue, a fact out on a stream. This process is both a RabbitMQ consumer
// and a Kafka producer, which is an extremely common shape and the reason exercise 3 exists.
//
//   dotnet run --project Receiver
//   DUAL_WRITE_WINDOW=10 dotnet run --project Receiver     # for Probe A

Console.Out.Flush();
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

Console.WriteLine($"Receiver starting. PID {Environment.ProcessId}");

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nStopping after the current message...");
    stopping.Cancel();
};

using var publisher = new KafkaEventPublisher<OrderPlaced>(
    await EventStreamProducer<OrderPlaced>.CreateAsync(
        serializer:   OrderPlaced.Serialize,
        partitionKey: @event => @event.OrderId));

var pump = new MessagePump<PlaceOrder>(
    new PlaceOrderMapper(),
    new PlaceOrderHandler(new Catalogue(), publisher));

try
{
    await pump.Run(stopping.Token);
}
catch (OperationCanceledException)
{
    // Ctrl-C. The expected ending.
}

Console.WriteLine("Receiver stopped.");
