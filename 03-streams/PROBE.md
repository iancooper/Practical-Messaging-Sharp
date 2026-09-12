<!-- Generated from the canonical exercise source (docs/03-streams/PROBE.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 3 — The Same Guarantees on a Stream #

**30 minutes, and there is nothing to build.** Deck reference: Day 1 §4.4 *The Dual-Write
Problem*, *Outbox*, *Inbox*, *Streams — No Requeue or DLQ*, *What Your Broker Actually Gives
You*; §4.5 *Scaling Streams — Consumer Groups*, *Archive and Replay*.

> *Take the reliability you built on a queue and get the same guarantees on a stream.*
> **Nothing you relied on in §4.4 is native here.**

---

## What you have ##

**Exercise 2's answer, plus a stream.** The pump is correct and nothing in it needs fixing. What
is new is one line in the handler: having placed the order, it publishes an `OrderPlaced` event
to a Kafka topic.

A command in on a queue; a fact out on a stream. **You have almost certainly written this.**

```
cd 03-streams
dotnet run --project Receiver        # RabbitMQ consumer AND Kafka producer
dotnet run --project StreamConsumer  # Kafka consumer, counts what it sees
dotnet run --project Sender          # one order
```

Three terminals, and two ways to look at what the brokers think:

```
../00-setup/queues.sh                   # RabbitMQ: ready, unacked, consumers
../00-setup/lag.sh                      # Kafka: offset and lag, per partition
../00-setup/reset.sh                    # back to empty, between probes
```

▎ **There is no management console for Kafka here, and that is not an oversight.** RabbitMQ hands you a web UI that will show you an individual message. Kafka hands you a log, an offset and a subtraction. That difference is most of what operating the two feels like.

---

## READ — 5 minutes, no agent ##

| file | |
|---|---|
| `Model/PlaceOrderHandler.cs` | the handler. **Nothing in it is wrong** |
| `SimpleEventing/EventStreamConsumer.cs` | the stream's version of the pump. Read the comment at the top |
| `SimpleEventing/Stream.cs` | the topology — and compare its length with `SimpleMessaging/Channel.cs` |

1. **`Channel.cs` declares four queues. `Stream.cs` declares one topic.** Exercise 2 used the
   other three for retry, invalid messages and dead letters. Where did they go?
2. **`EventStreamConsumer` commits an offset instead of acknowledging a message.** What can you
   do with an ack that you cannot do with an offset?
3. **The handler publishes to Kafka, then returns, and then the pump acks RabbitMQ.** Those are
   two writes to two brokers. Which transaction are they in?

---

## PROBE A — one order, two events — 12 minutes ##

**This is the probe that matters.** Do it first, and reset before it so the numbers are clean.

```
../00-setup/reset.sh
```

The handler publishes the event and then returns; the pump acks RabbitMQ afterwards. In a real
service the gap between those is microseconds wide. `DUAL_WRITE_WINDOW` widens it so you can
aim at it — it does not create it.

**Predict. All four, in writing, before you run anything.**

```
1. I kill the Receiver AFTER the Kafka write and BEFORE the RabbitMQ ack.
   What does RabbitMQ do with the command?                               ______

2. When I restart the Receiver, how many times will the order be placed? ______

3. How many OrderPlaced events end up on the stream?                     ______

4. Which of the two is wrong -- the queue or the stream?                  ______
```

Now run it. Terminal 1, and leave it running for the whole probe:

```
dotnet run --project StreamConsumer
```

Terminal 2:

```
DUAL_WRITE_WINDOW=15 dotnet run --project Receiver
```

Terminal 3:

```
dotnet run --project Sender
```

Watch terminal 2. It will tell you the event is on the stream, that RabbitMQ has not been
acked, and give you its PID. **You have fifteen seconds.** From terminal 3:

```
kill -9 <pid>
../00-setup/queues.sh            # where is the command?
```

**Wait ten seconds before you believe that number.** The management console refreshes its
statistics on a timer, so straight after a kill it may still be showing you the state from
before — and a correct prediction can look wrong for five seconds. **Kafka has a slower version of
the same problem**, and if terminal 1 has gone quiet that is the first thing to suspect: see
*Two things to know before you trust a number* in `../00-setup/README.md`.

Then restart the Receiver — without the window this time — and watch terminal 1.

```
dotnet run --project Receiver
```

**Record what happened:**

- The command after the kill: `ready` = `____`
- Times the handler placed that order: `____`
- `OrderPlaced` events on the stream for it: `____`
- StreamConsumer said: `________________________________`

▎ **The queue behaved perfectly.** It held the message because you never acked it, and it redelivered it to the next consumer, which is precisely what you asked exercise 2 to make it do. And because of that, you placed one order and told the world twice.

**Now do it the other way round.** Move the acknowledgement in `SimpleMessaging/MessagePump.cs`
so it happens *before* `_handler.Handle(...)` instead of after. Reset, and run the probe again — but
**aim the kill somewhere else this time**, and the reason is worth a moment of its own.

`DUAL_WRITE_WINDOW` holds the process open *after* the event is on the stream. That is the gap
that duplicates, and it is the one you have just been aiming at. With the ack moved, the gap that
*loses* is the one **before** the Kafka write — so that is where the process has to die, and the
window is no use to you. The slow lookup gives you thirty seconds of it instead:

```
dotnet run --project Sender -- slow  # then kill the receiver during the lookup, before the event
```

```
5. Events on the stream now?                                             ______
6. Orders RabbitMQ still has a record of?                                ______
```

▎ **The receiver's own message is a lie now, and nothing broke to make it one.** It still announces that the event is on the stream and RabbitMQ has not been acked. You moved one line, and a log statement that was true became false — which is worth remembering the next time you trust one.

▎ One ordering duplicates. The other loses. **There is no third place to put that line** — and you have just proved it by exhausting the options.

---

## PROBE B — a record you cannot read — 10 minutes ##

On a queue this was exercise 2 and it took twenty lines of policy. Here:

```
../00-setup/reset.sh
dotnet run --project StreamConsumer       # terminal 1, leave it running
dotnet run --project Sender -- bad-event  # appends an unreadable record to the topic
```

```
7. What does the stream consumer do with it?                             ______

8. How do I get it out of the way so the next record can be processed?   ______
```

Now, **with the consumer still stuck**, put good traffic behind it:

```
dotnet run --project Receiver           # terminal 2
dotnet run --project Sender -- burst 6  # terminal 3
../00-setup/lag.sh
```

- Partitions caught up: `____`   Partitions stuck: `____`
- Good events the consumer managed to read: `____`
- The stuck partition's CURRENT offset, checked twice a minute apart: `____` and `____`

▎ **Two thirds of your stream is working perfectly.** One partition will never move again, and its only symptom is a number that stopped going up. There is no queue to look in, no `x-death` header to read, and no message you can click on.

**Now specify the fix — do not write it.** Five minutes, out loud or on paper. Nominate the
approach you would actually take, and say what it costs:

| approach | what it costs you |
|---|---|
| **Ignore and continue** — commit the offset and move on | |
| **Retry in place** — what it does now | |
| **Copy to another topic** and commit | |

For the third one, be specific about what you would have to build, because this is the honest
comparison with exercise 2: who creates the topic, who reads it, who decides when to try again,
where the attempt count lives, and **where in the stream the record comes back**.

---

## PROBE C — the thing a queue cannot do — 5 minutes ##

Everything so far has been a stream doing less than a queue. This is the other direction.

```
9. I want to re-read every OrderPlaced event from the beginning.
   On a queue, how?                                                      ______
   On a stream, how?                                                     ______
```

Change `Stream.ConsumerGroup` to any new name and run the stream consumer again.

- Events read: `____`   (the log was never modified)

▎ A queue holds work to be done, and work that is done is gone — so replaying it means asking the producer to send it again. A stream holds facts, and reading is not consuming. **The offset is yours, not the broker's.**

---

## Argue about this before you leave ##

**Ten minutes, no keyboard.** Two questions, and the first one is the one you will meet at work.

**1. What actually fixes Probe A?**

You cannot put the two writes in one transaction — different brokers, no shared transaction
manager, and that is the definition of the process boundary. So: name the pattern, say which
half of the problem it solves, and say what the other half costs.

- **Outbox** — write the order and the intent-to-publish in one database transaction, and
  relay from there. Who does the relaying, and what happens when the relay runs twice?
- **Inbox** — the consumer records what it has seen and ignores a repeat. Whose problem is
  that, and does it help the *producer* at all?
- **Neither** — make the event idempotent, and let the duplicate be harmless. When is that
  genuinely available to you, and when is it a wish?

▎ Exactly-once *delivery* does not exist. Exactly-once *processing* is what the Outbox and Inbox pair buys, and it is two pieces of work in two services.

**2. Count what the broker did for you.**

For each row, say who supplies it on each side — the broker, or you:

| | RabbitMQ (exercise 2) | Kafka (exercise 3) |
|---|---|---|
| per-message ack | | |
| requeue | | |
| requeue with delay | | |
| dead letter destination | | |
| redelivery count | | |
| ordering | | |
| replay the past | | |

**Then answer the question the deck asks:** if your framework offers you a dead letter queue on
Kafka, who built it — and do you know what it does when it fails?

---

## Take home ##

`../04-lookup/` is exercise 4, and it is optional. The `Catalogue` the handler has been calling
is a dictionary; it is standing in for reference data that belongs to somebody else. Exercise 4
fills it from a stream, and asks the only question that matters about that: **how stale is it,
and how would you know?**
