using Model;
using SimpleMessaging;

// The consumer. Runs a Message Pump until you stop it.
//
//   dotnet run
//
// Ctrl-C stops it between messages. 'kill -9 <pid>' stops it mid-message.

Console.Out.Flush();
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

Console.WriteLine($"Receiver starting. PID {Environment.ProcessId}");
Console.WriteLine();

using var stopping = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nStopping after the current message...");
    stopping.Cancel();
};

var pump = new MessagePump<PlaceOrder>(
    new PlaceOrderMapper(),
    new PlaceOrderHandler(new Catalogue()));

try
{
    await pump.Run(stopping.Token);
}
catch (OperationCanceledException)
{
    // Ctrl-C. The expected ending.
}

Console.WriteLine("Receiver stopped.");
