<!-- Generated from the canonical exercise source (docs/02-failing-well/SOLUTION.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 2 — what the fix is #

**Prose, on purpose. No code.** The compiling version is in `../03-streams/`, which is where
exercise 3 starts from.

---

## The defect, stated precisely ##

The pump had one `catch (Exception)` and one response to it: `Requeue`, which is
`basic.nack` with `requeue: true`.

That response is wrong in three separate ways and it is worth separating them, because each one
is a different lesson:

1. **It has no limit.** Nothing counts attempts, so nothing can ever stop. Probe A measured
   this at thousands of attempts a second.
2. **It has no delay.** The message goes back and is picked up again immediately, which is why
   the number is tens of thousands rather than fifteen. A transient failure gets no time to
   become non-transient.
3. **It does not distinguish the two failures.** A body that cannot be mapped and a handler that
   threw arrive at the same `catch` and leave by the same door — and one of them can never
   succeed, so retrying it is the definition of a poison pill.

▎ *A message that is only ever nacked blocks the queue forever. Requeue and reject both have to end somewhere.*

## The fix ##

**Two catches, because there are two failures.**

### A failure to understand: `UnmappableMessageException` ###

Publish it to the invalid message queue and acknowledge the original delivery.

**Retries are not merely unhelpful here, they are the bug.** The bytes on the wire will not
change between attempt one and attempt ten thousand. And the two shortcuts are both wrong:
acking it to make it go away is silent data loss — a misconfigured producer may have sent a
perfectly good message to the wrong channel, and nobody will ever know it happened — and
rejecting it into the retry cycle is Probe A again with a timer on it.

### A failure to process: anything else ###

Ask `RetriesSoFar(delivery)`. Under the limit, reject it for retry — one call, and the topology
supplies the five-second delay. At or over the limit, publish it to the dead letter queue and
acknowledge.

**Note that nothing in the pump counts anything.** The count comes from the `x-death` header,
which RabbitMQ maintains, and the pump reads it. That is the difference between using a broker
capability and reimplementing one.

## The arithmetic, and why the answer is 4 ##

A limit of `n = 3` retries runs the handler **four** times: the original attempt plus three
retries. At roughly five seconds a retry that is about fifteen seconds from first failure to
dead letter.

The reason people answer 3 is that "3 retries" sounds like a total. It is not: the first
attempt was not a retry. **This matters beyond the arithmetic** — if you configure `n` from a
downstream service's recovery time, you are specifying `n × delay` of patience, and getting the
fencepost wrong by one attempt is the difference between riding out a rolling restart and
dead-lettering an afternoon's orders.

## The two things that bite ##

### Acknowledge every branch ###

A delivery you have republished elsewhere is **still outstanding** until you ack it. Leave it
and the broker returns it to the queue the moment your connection drops — and the copy you
published is already sitting in the retry or dead letter queue. You have duplicated the message
while trying to be careful with it.

The exception is the **retry** branch, and it is an exception for a good reason: rejecting *is*
finishing with the delivery. `basic.nack` with `requeue: false` hands the message back to the
broker, which routes it to the work queue's dead-letter target. Ack it as well and you will get
an error for acknowledging a delivery tag you no longer hold.

▎ Every branch ends the delivery exactly once. Two of them do it with an ack; one of them does it with the reject itself.

### Which direction the dead-letter cycle runs ###

The work queue's `x-dead-letter-routing-key` points at the **retry** queue, not at the dead
letter queue — which looks backwards until you know why.

RabbitMQ maintains the `x-death` count for dead-letter hops **it performs itself**. Work →
retry → work is such a cycle: a reject takes it one way, a TTL expiry brings it back, and RMQ
increments the count on each lap. Publish to the retry queue by hand instead and the count
stops incrementing — because as far as the broker is concerned, that is a brand new message
someone published, which happens to carry an `x-death` header.

**That is not a footnote; it is the whole reason the topology is shaped this way.** The two
terminal queues can be published to by hand precisely because nothing about them needs
counting. If you built this and your retry loop never ended, check which hops the broker owns.

## What you have, and what it cost ##

Per-message acknowledgement. A reject that routes rather than deletes. A delayed retry with a
limit, built from a TTL and a dead-letter exchange. Two terminal destinations that mean
different things.

**You wrote a policy — about twenty lines of `catch` — and RabbitMQ did all the work.** No
scheduler, no retry table, no plugin, no queue of your own.

**Which is exactly the point exercise 3 makes, by taking all of it away.** On a Kafka stream
there is no per-message ack, so there is no reject, no requeue, no delay and no dead letter
queue. Everything above becomes code you write and state you keep. The deck's table says so in
one row; exercise 3 is that row, felt.
