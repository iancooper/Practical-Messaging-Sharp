<!-- Generated from the canonical exercise source (docs/03-streams/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 3 — The Same Guarantees on a Stream #

**Start with [PROBE.md](PROBE.md).** 30 minutes, and there is nothing to build.

```
dotnet run --project Receiver             # RabbitMQ consumer AND Kafka producer
dotnet run --project StreamConsumer       # Kafka consumer, counts duplicates
dotnet run --project Sender               # one order
dotnet run --project Sender -- bad-event  # an unreadable record, straight onto the stream

DUAL_WRITE_WINDOW=15 dotnet run --project Receiver  # widens the gap so you can aim at it
```

| | |
|---|---|
| [`PROBE.md`](PROBE.md) | the exercise: predict, break, observe, argue |
| [`SOLUTION.md`](SOLUTION.md) | what the probes show. After the predictions, not before |
| `SimpleMessaging/` | exercise 2's answer. **Correct — nothing here needs fixing** |
| `SimpleEventing/` | the Kafka side: one topic, and nothing else |
| `Model/PlaceOrderHandler.cs` | places the order, then publishes the event. Also correct |

Watching the brokers: `../00-setup/queues.sh`, `../00-setup/lag.sh`, `../00-setup/reset.sh`.

**Nothing in this exercise is a bug you can fix by editing a file**, which is the point of it.
