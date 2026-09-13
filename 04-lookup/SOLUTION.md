<!-- Generated from the canonical exercise source (docs/04-lookup/SOLUTION.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 4 — the answers #

**Exercise 4 asks you to build something, so this is a specification review rather than a fix.**
It is what a good answer contains, what the probes show, and which of the decisions in
`README.md` were the ones that mattered.

**The worked answer is in this directory**, and it is one answer rather than the answer — there
is nothing planted in it and nothing left broken. Read this first if you built your own; the
interesting comparison is the decisions, not the code.

---

## The four decisions, and only two of them are hard ##

| decision | the easy answer | why it is the one |
|---|---|---|
| what the event carries | **a snapshot**: the SKU and its new price | it makes applying it twice harmless, which Probe D then cashes in |
| what the record is keyed by | **the SKU** | it is the only key that keeps two changes to one price in order. Key by event id and you can apply an old price on top of a new one |
| where the copy lives | **a file on local disk** | so that it survives the process, which is the whole of Probes C and D |
| who knows it is SQLite | **one project, and not the domain** | exercise 1's fix, a second time |

The first two look like schema questions and are actually failure-handling questions. That is
worth saying out loud, because it is the shape of most schema questions: **you are not choosing
what to write down, you are choosing what a repeat delivery costs you.**

### Delta or snapshot, said properly ###

A delta — *"WIDGET-1 went up by 2.00"* — is smaller, it is the honest record of what happened, and
it is what a domain expert will describe to you. It is also **not idempotent**, and the moment it
is not idempotent, every duplicate in the system becomes a correctness bug rather than a wasted
write.

A snapshot — *"WIDGET-1 is now 11.99"* — throws away the history and cannot tell you why. Apply it
twice and nothing happens. Apply them out of order and you need the timestamp to notice, which is
why it carries one.

▎ **Neither is right. The snapshot is right *here*, because you are maintaining a copy rather than an audit trail** — and a copy only ever needs to know the current answer. Build the audit trail and you need both, which is why real systems publish both and people find that surprising.

### The interface is the answer to the other two ###

`Catalogue` asks `IPriceStore` for a price. `IPriceStore` is declared by
`Model`, implemented by `LocalCopy/`, and wired together in `Receiver/`.
That is three files and it is the difference between "we use SQLite" being a **decision** and it
being a **fact about the domain**.

The test is the same one exercise 1 gave you: **what has to change if you are wrong?** Move the
copy to Redis and one implementation changes. Reach for SQLite from inside `Model/` instead
and the answer is "everything that imports the domain, plus every test of it".

▎ You did not build this seam for SQLite. You built it in the first half hour, for RabbitMQ, and it paid a second time against a problem nobody had described yet. **That is what a seam is for, and it is the only argument for one that survives contact with a deadline.**

## Probe A — how stale is it? ##

**Tens of milliseconds, on a laptop, with one hop of Kafka in between** — and in one of the five
languages the same probe against the same broker reads over half a second, every time, because its
client takes that long to connect and the seeder is a fresh process on every `set`. The first record
after a start is the slow one, and that is a connection rather than a broker.

▎ **Which is the finding, and it is not the number.** Two of those measurements differ by a factor
of fifty with identical brokers, identical topics and identical code shape. If you had taken either
one as *"what Kafka costs"* you would have been wrong, and nothing in the number itself would have
told you. The fact that staleness is *measurable at all* is what you came for; knowing which part of
what you measured belongs to the broker is what makes the measurement worth having.

The important part is the second measurement, the one the probe asks you to make deliberately
wrong. Time it to the moment the order is priced and you get about a second, because you waited a
second before placing the order. **You measured your own hand on the keyboard and called it broker
latency**, and that is a mistake people ship — in a dashboard, in a capacity plan, in an SLA.

**Publish-to-applied is the only number that is about the system.** Everything downstream of the
apply is about whoever asked.

And the worst case is not the average. It is: the consumer is behind, or rebalancing, or was
restarted; the record is at the back of a partition that a slow record is holding up. **Staleness
is bounded by consumer lag, not by broker latency**, and lag is the thing you already know how to
watch — `lag.sh`, from exercise 3.

## Probe B — the consumer is dead and everything is fine ##

**The orders succeed. They are priced from the last copy. Nothing reports anything.**

This is the trade, stated as plainly as it can be stated: you swapped a **loud** failure for a
**quiet** one. On-demand lookup fails by timing out, which backs up your queue, which shows up on
a graph somebody is already looking at. ECST fails by being confidently wrong, indefinitely, in a
process whose logs say nothing because nothing went wrong in it.

**How long could it go on? Until a human notices a price is wrong** — which in practice means
until a customer is charged the wrong amount, because nothing else in the system is checking.

So the answer to *"how would you detect it?"* is the whole of the operational work ECST costs you,
and there are three answers, in increasing order of how much they are worth:

| | |
|---|---|
| **watch the consumer** — is the process up? | cheapest, and it catches `kill -9`. It does not catch a consumer that is running and stuck, which is exercise 3's Probe B |
| **watch the lag** — is the offset moving? | catches stuck as well as dead, and it is the same `lag.sh` number as staleness. This is the one to build |
| **watch the copy** — how old is the newest row in it? | the only one that is about the thing you actually care about, and it needs the timestamp you put on the event in step 1 |

▎ **The third row is why the event carries a timestamp**, and it is the argument for carrying one even when nothing reads it yet. A copy that cannot say how old it is cannot be monitored, and a copy that cannot be monitored is a copy you will find out about from a customer.

## Probe C — the empty copy, and the old one ##

**A lookup that returns nothing cannot tell you *why* it returned nothing**, and that is the
first thing to get right. "I have no copy yet" and "that SKU does not exist" arrive as the same
`null`, and they are opposite facts: one is about us and fixes itself, the other is about the
order and never will. **Your `IPriceStore` interface is where you decide whether that is
knowable at all** — *"have you a price for this?"* and *"have you any prices at all?"* are two
questions, and an interface that only offers the first has thrown the distinction away before
anybody could use it.

The answer here asks both, and `Catalogue` throws two different exceptions. **And then
nothing downstream cares**, which is the part worth the probe:

| | what the domain says | what the pump does |
|---|---|---|
| `WIDGET-1`, copy empty | `the local copy has no prices in it yet` | 3 retries, then dead-lettered |
| `NOPE-404`, copy full | `'NOPE-404' is not in the catalogue` | 3 retries, then dead-lettered |

Measured, and identical to the second. **Exercise 2's pump has two answers and needs three.** A
body it cannot read is never retried; everything else is retried *n* times and dead-lettered; and
there is no third door for *"this is fine, we are not ready, ask again in a minute"*.

So the honest answer to blank 10 is **both, differently**:

- **The unknown SKU should never be retried.** It is as permanent as an unreadable body, and it
  is sitting in a retry queue for fifteen seconds for no reason. It wants the invalid-message
  door, or one beside it — the failure is in the *message*, not in the work.
- **The empty copy should be retried far longer than twenty seconds**, because "has the price
  consumer started yet" is a question whose answer arrives in minutes, and dead-lettering a
  perfectly good order because your own infrastructure was slow is the kind of loss that gets
  discovered in a reconciliation.

▎ **None of that is a change to the domain**, and that is the lesson to carry out. `Catalogue` cannot know what a good response to being unready is — that depends on the channel, the retry budget, and what the business does about a lost order, none of which are its business. **It can only tell the truth precisely enough that somebody else can decide.** Getting the exception types right is not pedantry; it is the difference between a policy that *can* be written and one that cannot.

**The second half is what a durable copy adds, and it is the state nobody designs for.** Restart
everything and the orders keep pricing — from a file, with no consumer running, with no
indication anywhere that the numbers came from whenever you last ran the seeder.

So there are three states, not two:

- **no copy** — obvious, and it fails immediately
- **a current copy** — obvious, and it works
- **a copy of unknown age** — indistinguishable from the second one, from the inside

**Only the third one can hurt you**, and the only defence is the timestamp: if every row records
when it was written, the third state becomes visible and can be alarmed on. If it does not, you
have built a system whose correctness you cannot check.

Measured: stop every process, start only the receiver, and it announces `holding 2 prices --
newest change 2m old` and prices the order.

**It told you.** `IPriceStore` can date the copy, the receiver prints it, and that is
already better than most systems manage. It is also **not enough, and the reason is the shape of
the failure rather than the shape of the code**: staleness is a thing that develops while a
process runs, and a startup line is a measurement taken once, at the only moment it was certainly
fine. Four days later the same process is still pricing orders and the only evidence is a log
line that scrolled away.

▎ **A number you print at startup is documentation. A number you check while running is monitoring.** They look identical in a code review and they are not the same artefact at all.

▎ An in-process map has only the first two states, which is exactly why it could not teach you this. **The cheapest store that survives a restart is also the first one that can lie to you.**

## Probe D — the dual write, in code you wrote ##

The price consumer does two things per record: it **applies** the price to the local copy, and it
**commits** the offset. There is no transaction across them, because one of them is in SQLite and
the other is in Kafka.

**Apply first, then commit, then die** → the record is read again on restart and applied again.
The price is set to 77.77 twice. **Nothing happens**, because a snapshot applied twice is the same
snapshot — which is the step-1 decision paying out. Measured: the partition sits behind the log
end until the consumer comes back, and then the copy holds the price it already held.

**Commit first, then apply, then die** → the offset is past a record whose price was never
written. Kafka will not offer it again, because as far as the group is concerned it is done.
**The price is gone until somebody republishes it**, and nobody will, because the catalogue
service has no idea anything went wrong.

▎ **And here is the part that should change how you read a dashboard.** Measured on the reversed ordering: after the kill, `lag.sh` reports **zero lag on every partition**. The group is fully caught up, because it is — it committed past a record it never processed. *The monitoring is not merely silent about the loss. It is actively reporting health, and it is telling the truth about the only thing it can see.*

▎ This is exercise 3's Probe A with the names changed, and that is the point of putting it here. The first time, it was in code we wrote and you agreed it was unfixable in place. **The second time it is in code you wrote, in a loop that looks like one step.**

### What actually fixes it ###

**Not an Outbox.** The Outbox works in exercise 3 because both writes can be made to land in one
database — the order and the outbox row. Here one of the two writes is *Kafka's offset*, which
does not live in your database and cannot be enrolled in your transaction.

Three answers, and they are the same three every stream processor is built out of:

| | |
|---|---|
| **Make the apply idempotent** and accept at-least-once | you already did, by choosing a snapshot. **Free, and it is why the choice mattered** |
| **Store the offset in the same store as the copy**, and commit both in one SQLite transaction | genuinely exactly-once for this consumer. You stop using Kafka's offset commit and take over the bookkeeping, which is more code than it sounds |
| **Make the record carry a version** and ignore anything not newer | idempotence for deltas, bought with a comparison. It is also how you survive out-of-order delivery across a rekey |

**The second row is what a stream-processing library does for you**, and it is worth knowing that
this is what you are paying it for. If your framework advertises exactly-once on Kafka, go and
find out which of these three it built — and what it does when the store it chose is unavailable.

## The one-sentence version ##

**On demand: always right, sometimes unavailable. In advance: always available, sometimes
wrong — and the work you save on the call, you spend on knowing how wrong.**
