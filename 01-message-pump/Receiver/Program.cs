using Model;
using SimpleMessaging;

// The consumer. Runs a Message Pump until you stop it.
//
//   dotnet run
//
// Ctrl-C stops it *between* messages, which is the polite ending.
// Several probes want the rude ending instead -- a process that dies with a message in its
// hands. Use the PID printed below, from another terminal:
//
//   kill -9 <pid>

// Flush every line. Several probes end with this process being killed, and a buffered
// line you never see is a probe you cannot read.
Console.Out.Flush();
var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
Console.SetOut(stdout);

Console.WriteLine($"Receiver starting. PID {Environment.ProcessId}");
Console.WriteLine("Ctrl-C to stop between messages; 'kill -9 " + Environment.ProcessId + "' to stop mid-message.");
Console.WriteLine();

using var stopping = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // do not tear the process down; let the pump notice
    Console.WriteLine("\nStopping after the current message...");
    stopping.Cancel();
};

var pump = new MessagePump<PlaceOrder>(new PlaceOrderHandler(new Catalogue()));

try
{
    await pump.Run(stopping.Token);
}
catch (OperationCanceledException)
{
    // Ctrl-C. The expected ending.
}

Console.WriteLine("Receiver stopped.");
