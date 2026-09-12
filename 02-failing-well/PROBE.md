<!-- Generated from the canonical exercise source (docs/02-failing-well/PROBE.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 2 — Failing Well #

**40 minutes.** Deck reference: Day 1 §4.4 *When the Handler Fails — Ack and Nack*, *Not Acking
— Requeue or Reject*, *Invalid Message Channel*, *Requeue with Delay*, *Dead Letter Channel*,
*What Your Broker Actually Gives You*.

---

## What you have ##

**Exercise 1's answer.** If you did not finish exercise 1, you start level — this pump has all
of it, and it is worth two minutes to see what changed:

- there is a **Message Mapper**, and the Translate stage goes through it
- `PlaceOrderHandler.Handle` takes a `PlaceOrder`. `Model.csproj` has no broker reference
- the acknowledgement happens **after** the handler returns
- a failure no longer kills the loop

So it does not crash and it does not lose messages. **It is still wrong, and this time it will
not show up in your logs as a crash.**

```
cd 02-failing-well
dotnet run --project Receiver  # terminal 1
dotnet run --project Sender    # terminal 2
```

**Management console open at <http://localhost:15672>, and this time there are four queues.**
Filter the Queues list on `failing-well`. Two of them should always be empty; when they are
not, that is the alert. Find out which two.

---

## READ — 5 minutes, no agent ##

| file | what to look at |
|---|---|
| `SimpleMessaging/Channel.cs` | the topology, drawn in the comment. Read the comment |
| `SimpleMessaging/DataTypeChannelConsumer.cs` | the five things the pump can do with a message |
| `SimpleMessaging/MessagePump.cs` | the `catch` block. All of it |

1. **There are four queues and three of them are new.** What is each one *for*? One of them has
   no consumer at all and that is deliberate — which, and why does that work?
2. **The consumer exposes five verbs:** `Acknowledge`, `Requeue`, `RejectForRetry`,
   `SendToInvalidMessageQueue`, `SendToDeadLetter`, and a `RetriesSoFar`. **The pump uses two of
   them.** Which three does it never call?
3. **The `catch` block catches `Exception`.** Name two failures that both land in it and should
   not be treated the same way.

▎ *Dead Letter: we could not deliver it. Invalid Message: we delivered it and could not read it.* The pump has one `catch`. It therefore has one answer to two questions.

---

## PROBE A — how many times is "put it back" — 5 minutes ##

**Predict first.** You are about to send one message that can never be mapped.

```
1. How many times will the pump try to map it before it stops?           ______

2. Where will the message be in one minute?                              ______
```

Now run it, and capture the receiver's output so you can count what happens.

```
dotnet run --project Receiver | tee /tmp/probe-a.log  # terminal 1
dotnet run --project Sender -- unmappable             # terminal 2
```

**Watch terminal 1 for about ten seconds** — you will see why we are capturing it — then stop
the receiver with Ctrl-C and count:

```
grep -c FAILED /tmp/probe-a.log
```

- Attempts in about ten seconds: `____________`

For reference, on a laptop against a local broker that number comes out around **fifteen
thousand**. There is no delay, no limit, and no exit.

▎ The pump never crashed, never lost the message, and logged every single attempt. **A monitor watching for errors would be perfectly happy.** This is the failure mode that gets found by the person who notices the disk filling up.

---

## PROBE B — set *n* and count — 10 minutes ##

**This is the exercise.** It is a question about arithmetic and almost everyone gets it wrong
the first time.

You are going to fix the pump so it retries a failing handler at most **3** times. Not yet —
first, commit to the answer:

```
3. With a limit of n = 3 retries, how many times does the handler
   actually run before the message is given up on?                      ______

4. How long does that take, if each retry waits 5 seconds?              ______

5. When it is finally given up on, which queue is it in?                ______
```

**Write those down before you read another word.** Then do the FIX below, come back, and run:

```
dotnet run --project Sender -- poison  # NOPE-404: never in the catalogue, ever
```

Count the handler invocations in the log, and then **go and find the message in the console**.
Click it. Click *Get Message*. Look at the headers.

- Handler invocations: `____`   (the answer is not 3)
- Total elapsed: `____`
- `x-death` shows: `count` = `____`, `reason` = `____________`, `queue` = `____________`

▎ `x-death` is RabbitMQ telling you a message's entire history for free, and almost nobody knows it is there. It is how the pump knows how many attempts it has had — the pump does not count anything itself.

---

## PROBE C — the same failure, twice, differently — 5 minutes ##

`FLAKY-1` is in the catalogue. The lookup fails the first two times it is asked and works on
the third — a service that was restarting while you happened to call it.

```
dotnet run --project Sender -- flaky
```

```
6. Does this order ever get placed?                                     YES / NO

7. Does it reach the dead letter queue?                                 YES / NO

8. NOPE-404 and FLAKY-1 throw from the same line of the same method.
   How does the pump tell them apart?                                   ______
```

Run it after the fix.

▎ **It cannot tell them apart, and it does not need to.** The policy is *retry a few times and then give up*, and that policy is correct for both — it just happens to end differently for each. Retrying is how you find out which one you had.

---

## FIX — 15 minutes ##

**One file: `SimpleMessaging/MessagePump.cs`.** The topology is already declared and correct;
the gateway already has every verb you need. What is missing is the **policy**, and a policy is
a judgement, which is why it is yours and not your agent's.

**Split the one `catch` into two, because you have two kinds of failure:**

**A failure to *understand*** — the mapper threw `UnmappableMessageException`. The bytes are
not going to change. There is no number of retries that helps, and dropping it silently is not
allowed either, because a misconfigured producer may have sent a perfectly good message to the
wrong channel. **Put it on the invalid message queue and finish with the delivery.**

**A failure to *process*** — the handler threw. The message was fine; the work failed, and it
may simply have been unlucky. **Retry it, with a delay, up to a limit. Past the limit, treat it
as unrecoverable and dead-letter it.**

- `RetriesSoFar(delivery)` tells you how many attempts this message has already had.
- Set the limit to **3** so Probe B's arithmetic is checkable.
- The retry itself is one call, and the five-second delay is already built into the topology.

### Two things to get right, and both of them bite ###

**1. Every branch must finish with the delivery.** A message you have republished somewhere
else is still outstanding on the original delivery until you say otherwise. Leave it that way
and it comes back when the connection drops — and then you have two of it. One branch is the
exception: work out which, and why it does not need an explicit ack.

**2. There is a trap in this exercise and it is the realistic bug.** If you reject a message
for retry and *do not* check the count, you have not fixed the infinite loop from Probe A — you
have given it a five-second period, which makes it a hundred times harder to notice and no less
infinite. **`grep -c` your log for the poison message after two minutes.** If the number is
still climbing, you built the slow version of the bug you came here to fix.

### Verify from the console ###

| send | attempts | ends up in |
|---|---|---|
| `unmappable` | 1 | `invalid.failing-well.Model.PlaceOrder` |
| `poison` | 4 | `dead.failing-well.Model.PlaceOrder` |
| `flaky` | 3 | placed successfully, nowhere |
| nothing | — | `retry.…` **empty**, and it should be empty nearly all the time |

And with all of that going on, `dotnet run --project Sender` must still place a good order
immediately. **A queue that is failing should not be a queue that is slow.**

---

## Argue about this before you leave ##

Ten minutes, out loud, no keyboard. **It is on the slide and it is a genuine disagreement.**

> A body you cannot read, and work you tried four times and gave up on. **Should those be two
> queues, or one?**

For one queue: it is one alert, one runbook, one dashboard, and an operator looking at a failed
message does not much care *why* it failed until they open it.

For two: they need completely different responses. The invalid message queue filling up means
somebody changed a schema and you can probably fix it by fixing the producer. The dead letter
queue filling up means your own service is broken, or a dependency is. **Averaging those two
into one number loses the only information that tells you who to wake up.**

There is a right answer for your system and it is not the same as ours.

### If you have time ###

- **Where should the retry delay come from?** Five seconds, flat, for every failure. What would
  you rather have, and what does RabbitMQ make easy? (Look up how `x-death` count and a set of
  retry queues with different TTLs go together.)
- **Start two receivers**, then send a poison message. Which one gets the retries? Does it
  matter? Now make `FLAKY-1`'s counter per-process and think about what that did.

---

## Carry into exercise 3 ##

You have the full consumer-side picture: per-message acknowledgement, a rejection that routes,
a delayed retry with a limit, and two terminal destinations. **Every one of those is something
the broker did for you.** You wrote policy; RabbitMQ did the work.

Exercise 3 puts the same problem on a Kafka stream, where **none of it exists.** Not the ack,
not the requeue, not the delay, not the dead letter queue. Bring your notes.
