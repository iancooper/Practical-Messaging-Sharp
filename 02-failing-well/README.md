# Exercise 2 — Failing Well #

**Start with [PROBE.md](PROBE.md).** 40 minutes.

```
dotnet run --project Receiver     # the pump
dotnet run --project Sender       # a good order
dotnet run --project Sender -- unmappable | poison | flaky | slow | burst 20
```

Management console: <http://localhost:15672> — filter Queues on `failing-well`. There are four.

| | |
|---|---|
| [`PROBE.md`](PROBE.md) | the exercise |
| [`SOLUTION.md`](SOLUTION.md) | what the fix is, in prose. After the predictions, not before |
| `SimpleMessaging/Channel.cs` | the topology, drawn in a comment. Read it |
| `SimpleMessaging/MessagePump.cs` | **the one file you need to change** |

**This is exercise 1's answer**, so you start level whether or not you finished it. The pump no
longer crashes and no longer loses messages — and it is still wrong.
