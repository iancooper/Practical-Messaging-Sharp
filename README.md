<!-- Generated from the canonical exercise source (docs/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Practical Messaging — C# exercises #

Three exercises, about two hours, on your own laptop. **Day 1, §4.3 to §4.5.**

---

## Before the course ##

**Do this at home, on a network you trust.** It pulls two broker images and proves they start,
which is the twenty minutes of the day there is no reason to spend on a venue wifi connection.

```
git clone <this repo>
cd Practical-Messaging-Sharp/00-setup
./prereqs.sh
```

You need **Docker** and **the .NET 10 SDK**. That is all. No cloud account, no API key, and
nothing to sign up for.

If `prereqs.sh` reports a failure, bring its output with you and we will sort it out in the
first coffee break rather than in the exercise slot.

> **Already running RabbitMQ or Kafka on this machine?** The compose file wants ports 5672,
> 15672 and 9092, and **the answer that works is to stop your own containers for the day.**
> Moving the exercises off those ports is not the one-line edit it looks like — see
> *If something is already on those ports* in [`00-setup/README.md`](00-setup/README.md), and
> do it at home rather than in the slot.

## On the day ##

| | | |
|---|---|---|
| after §4.3 *The Message Pump* | [`01-message-pump/`](01-message-pump/PROBE.md) | 35 min |
| after §4.4 *Guaranteed Delivery* | [`02-failing-well/`](02-failing-well/PROBE.md) | 40 min |
| after §4.5 *Queues and Streams* | [`03-streams/`](03-streams/PROBE.md) | 30 min |
| take home, optional | [`04-lookup/`](04-lookup/README.md) | — |

**Each exercise is self-contained and each one starts from the previous one's correct answer.**
So if you do not finish — and the timings are tight on purpose — you start the next one level
with everyone else. Nothing is cumulative except what you have learned.

---

## How these exercises work, and why they changed ##

**The code you are given already runs.** There are no blanks to fill in.

That is a deliberate change, and the reason is simple: an exercise whose difficulty was *typing
the code* is not an exercise any more. An agent fills in a marked-out method in seconds, and
correctly, and you learn nothing. So we took the typing out and kept the part that is still
hard.

> **READ** it → **PREDICT** what will happen → **BREAK** it and watch → **FIX** it.

Three things survive contact with an agent, and they are the exercises:

1. **Specification.** You cannot ask for *"dead letter support in the pump"* well without
   already knowing what a dead letter queue is for, when a message should reach one, and what
   *n* retries actually buys you. **The skill is knowing what to ask for, and recognising when
   you did not get it.**
2. **Prediction.** An agent will hand you code that works. It cannot tell you what your queue
   depth will be in ten seconds, because that is a fact about a running system. Every exercise
   asks you to write down what you think will happen *before* you run it.
3. **Observation.** The RabbitMQ management console is agent-proof. Unacked counts, the
   `x-death` header, a partition whose offset stopped moving — these are things you look at.

### Use an agent. Or don't. ###

**Both paths work and neither is the fallback.** The predictions and the probes are identical
either way, and they are the graded part — the part you will remember on Monday.

- **With an agent:** you get to practise writing the specification and reviewing the answer,
  which is the job now.
- **Without one:** you go straight from the probes to `SOLUTION.md`, which is prose rather than
  code, and type the fix. You lose the authoring practice and nothing else.
- **Either way:** pair up if you can. Arguing about a prediction with another human is better
  than making one alone, and it is free.

▎ **The answer is not the point. The prediction is.** `SOLUTION.md` exists in every exercise and you are welcome to read it — but read it after you have written down what you expected, because that comparison is the only part of this that teaches you anything.

---

## What is in each exercise ##

| | |
|---|---|
| `PROBE.md` | **the exercise.** Read this first |
| `SOLUTION.md` | what the fix is, in prose, with the reasoning. Never code — and in exercise 3, where there is nothing to fix, what the probes show |
| `SimpleMessaging/` | the **messaging gateway** — the only code that knows which broker this is |
| `SimpleEventing/` | the same thing for Kafka, in exercise 3 |
| `Model/` | the domain: an order, a catalogue, a handler |
| `Sender/`, `Receiver/` | two console apps, plus `StreamConsumer` in exercise 3 |

**These are not production code.** They omit most of the error handling production code would
need, and they trade maintainability for focus. Where a file is *deliberately* wrong it says so
at the top, in capitals. Do not copy those into anything.

## Watching what the brokers think ##

This is the half an agent cannot do for you, so it is worth knowing your way around.

```
http://localhost:15672          RabbitMQ management console -- guest / guest
00-setup/queues.sh              the same numbers, on the command line
00-setup/lag.sh                 Kafka: offset and lag, per partition
00-setup/reset.sh               back to empty, between probes
```

**There is no web console for Kafka here.** That is not an oversight — RabbitMQ will show you an
individual message and Kafka will show you a subtraction, and that difference is most of what
operating the two feels like. Exercise 3 makes something of it.

## The patterns these exercises cover ##

Most of the patterns in Day 1 have a slide and a video and **no exercise**, which is new. These
four are exercised because they are the ones where the failure is the lesson:

| pattern | where |
|---|---|
| Message Pump, Message Mapper, Handler Registry, Service Activator | exercise 1 |
| Messaging Gateway, Datatype Channel, Polling Consumer | exercise 1 |
| Invalid Message Channel, Dead Letter Channel, Requeue with Delay | exercise 2 |
| Guaranteed Delivery, the Dual-Write Problem, Outbox, Inbox | exercise 3 |
| Competing Consumers, Consumer Groups, Archive and Replay | exercise 3 |
| Reference Data, ECST, Content Enricher | exercise 4 |

The rest — Point-to-Point, Publish-Subscribe, Message Endpoint, Pipes and Filters,
Request-Reply, Routing Slip — keep their slides and their videos. **The deck did not shrink;
the code did.**
