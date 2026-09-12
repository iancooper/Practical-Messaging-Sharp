<!-- Generated from the canonical exercise source (docs/03-streams/SOLUTION.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 3 — the answers #

**There is no code to write in exercise 3, so this is not a fix.** It is what the probes show,
and what the room should be able to say afterwards.

---

## Probe A — the dual write ##

**Nothing in the code is wrong.** That is the finding, and it is why this exercise is worth
thirty minutes rather than an assertion on a slide.

The pump is exercise 2's answer and it is correct. The handler is clean — a domain type in, no
broker in its signature, the event published through an interface. The ordering is the sensible
one: do the work, tell the world, then acknowledge. Every individual decision is the one you
would defend in review.

And yet: **kill the process between the Kafka write and the RabbitMQ ack, and one order becomes
two events.** RabbitMQ did exactly what exercise 2 asked of it — held the unacked message and
redelivered it — and that correct behaviour is what produced the duplicate.

Reverse the two lines and you get the other failure: the ack lands, the process dies, and the
event never happens. The order is gone from the queue and absent from the stream, and nothing
anywhere reports an error.

**Two writes to two brokers, no shared transaction.** That is the process boundary, which is
where Day 1 started. There is no ordering of those two lines that fixes it, which is why
*which line goes first* is the wrong question — and the reason the probe asks you to try both
is so that the conclusion is yours rather than ours.

### What does fix it ###

**An Outbox.** The service writes the order and a record of the event-to-be-published in **one
transaction against its own database** — one store, so one transaction is available. A separate
relay reads the outbox and publishes to Kafka.

That converts an impossible problem into a possible one, and it does not make the duplicate go
away: the relay can publish and die before marking the row sent, so it publishes again. **The
Outbox buys at-least-once, reliably**, which is a genuine improvement on "at-least-once or
at-most-once depending on where the process died".

**An Inbox** is the other half, and it belongs to the consumer. It records the message ids it
has processed and ignores a repeat. Note where the work lands: **the producer's reliability
created the consumer's obligation**, in a different service, probably owned by a different team.

**Or make the event idempotent** and let the duplicate be harmless. Sometimes genuinely
available — a snapshot event keyed by the order, applied with last-writer-wins, really is safe
to deliver twice. Often a wish: "placing an order" is not naturally idempotent, and neither is
anything that sends an email.

▎ Exactly-once *delivery* does not exist. Exactly-once *processing* is what the Outbox and the Inbox buy together, and it is two pieces of work in two services.

### Why this exercise exists at all ###

Because the room has just built the thing §4.4 warned about. **A handler that consumes a command
from a queue and produces an event to a stream is the dual-write problem**, and it is the most
common shape in service architecture. Naming it on a slide and building it in an exercise are
not the same experience, and the difference is that one of them stays.

## Probe B — no nack, no requeue, no dead letter ##

The consumer is doing **retry in place**, which is the default your framework almost certainly
takes for you. It winds the offset back and reads the same record again, for ever, and one
partition of three stops permanently.

**The important observation is not that it is stuck. It is that everything else is fine.** Two
partitions caught up, good events flowing, no errors in any dashboard that counts errors. The
only symptom is one partition's offset not increasing — which looks identical to a partition
under load until you check it twice.

Compare exercise 2: RabbitMQ noticed the rejection, moved the message, counted the attempts in
a header, and gave you a queue with a name that told you what kind of failure it was. **You
wrote about twenty lines of policy and the broker did all the work.**

Here there is no ack to withhold, because an offset is a bookmark and not a lock. Which removes,
in one go: requeue, requeue-with-delay, reject, dead letter, and redelivery count. The three
things you can actually do:

| | what you lose |
|---|---|
| **Ignore and continue** — commit and move on | **the record.** Load shedding, and it may be the right answer for telemetry. It is not the right answer for money |
| **Retry in place** — what it does now | **throughput on that partition**, and eventually all of it. One bad record is a partition-wide outage |
| **Copy to another topic** and commit | **the ordering.** And you must build the retry topic, its consumer, the scheduler that decides when to re-feed, and somewhere to keep the attempt count |

**The third one is the closest thing to requeue-with-delay, and the cost is the one people
miss:** the record comes back at the *end* of the log. You de-ordered the stream to get a
retry — and ordering within a partition was the thing partitioning bought you in the first
place.

▎ On a queue the broker holds the message for you. On a stream, whatever holds it is code you wrote.

## Probe C — archive and replay ##

A new consumer group reads the whole log from the beginning, because nothing was ever deleted.
Reading a stream does not consume it; the offset belongs to the reader.

There is no version of this on a queue. A queue holds work to be done, and work that is done is
gone — so "replay" means asking the producer to send it again, which means the producer kept it,
which means the producer built a stream.

**This is the row where the stream wins, and it is worth being precise about why:** it is the
same property that costs you everywhere else. There is no lock because there is nothing to
lock — the record was never removed, and a thing that is never removed cannot be held aside for
one consumer.

## The table ##

| | RabbitMQ (queue) | Kafka (stream) |
|---|---|---|
| per-message ack | **broker** — ack / nack | **you.** Offsets only |
| requeue | **broker** — nack, requeue: true | **you.** Seek, or re-publish |
| requeue with delay | **broker** — TTL + DLX, no code | **you.** A retry topic and a scheduler |
| dead letter destination | **broker** — dead-letter exchange | **you.** Another topic, and a consumer for it |
| redelivery count | **broker** — the `x-death` header | **you.** A header you maintain, or a table |
| ordering | **broker** — per queue | **broker** — per partition |
| replay the past | **nobody.** It is gone | **broker** — reset the offset |

Six of seven rows the broker filled in on the queue side. One on the stream side, and it is the
one the queue cannot do at all.

▎ **This is not "queues are better".** It is that the two models are answers to different questions, and every framework that hides the difference is spending your reliability budget without telling you the price. If your library offers you a dead letter queue on Kafka, it built one — go and read what it does when *it* fails.
