using System.Text.Json;
using Model;
using SimpleEventing;

CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true; // terminate the pump
    cts.Cancel();
};

var messagePump = new DataTypeChannelConsumer<Biography>(
    message => JsonSerializer.Deserialize<Biography>(message.Value),
    biography =>
    {
        Console.WriteLine($"Received biography for {biography.Id}");
        return true;
    });

await messagePump.Receive(cts.Token);