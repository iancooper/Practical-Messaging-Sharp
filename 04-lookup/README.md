<!-- Generated from the canonical exercise source (docs/04-lookup/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 4 — The Lookup *(optional, take home)* #

**This one gives you a specification and asks you to build it.** Exercises 1 to 3 gave you a
working system and asked you to predict how it fails. Writing the specification is the other half
of the skill, and it is the half an agent is genuinely good at taking off your hands — *once you
have it*.

**There is a worked answer in this directory.** That is the same bargain the other three exercises
make: nothing is hidden, because the answer is not the point. Build your own first if you want the
exercise; the probes below run against either, and the probes are the part that teaches you
something.

**One thing in it is deliberately the easy answer**, the way exercises 1 and 2 ship a defect, and
Probe C is what finds it. It is marked in capitals in the file it is in, so you will know when you
have got there.

Deck reference: Day 1 §6.2 *Reference Data* — *Get It On Demand* versus *Get It In Advance
(ECST)*, and *Be Honest About the Trade*. Day 2 picks this up again as *FBP — Where Do Lookups
Live?*

---

## The thing you have been ignoring for three exercises ##

`Model/Catalogue.cs` is a `Dictionary<string, decimal>`. It has been standing in for reference
data that **belongs to somebody else** — the catalogue service owns SKUs and prices, and your
order service needs them to price an order.

That is the most common integration in any system, and there are exactly two answers to it.
You have effectively been doing the first one:

| | |
|---|---|
| **Get it on demand** | call the catalogue service when you need a price. Always current. You are now **down when it is down**, and slow when it is slow — which is `GIZMO-SLOW`, and you have already watched what it does to your queue |
| **Get it in advance (ECST)** | the catalogue service publishes its changes; you keep a local copy and read that. Never blocked, never slow — and **always a little bit out of date** |

## Build the second one ##

**Start from `03-streams`.** Copy it somewhere of your own, and add:

1. **A `PriceChanged` event and a topic for it.** A SKU and a price, on the Kafka stream
   gateway in `SimpleEventing/` that exercise 3 gave you, keyed so that events for
   one SKU stay in order. Think about whether it should carry the new price or just say that
   the price changed — §6.3 *Domain or Delta Event* and *Summary or Snapshot*, and the choice
   matters here more than it looks. **Probe D is where it stops being a matter of taste.**
2. **A seeder** that publishes prices onto that topic, and can change one on demand.
3. **A consumer that follows the topic and maintains the local copy — in its own process, and in
   a store that outlives it.** Not a thread inside the receiver, and not a map in memory:
   **SQLite**, which is a file on your own disk. Two reasons, and both of them are probes.
   A separate process is a thing you can `kill -9`, which is Probe B. A store that survives the
   kill is what makes Probes C and D mean anything at all — a map in memory is empty after every
   restart, so *"my copy is stale"* and *"my copy is gone"* look identical and you can never see
   the difference that ECST is actually about.
4. **`Catalogue` reads the local copy** instead of its dictionary. `PriceOf` no longer throws
   `CatalogueUnavailableException`, because there is nothing left to be unavailable.

**SQLite is the cheapest durable store there is**, and that is the whole reason it is the one
specified: `Microsoft.Data.Sqlite` is one package, there is nothing to install on the machine, and
the database is a file you can delete. No container, no port, nothing else to go wrong on a
laptop.

### ⚑ The domain must not gain the SQLite dependency ###

This is the sharpest thing in the exercise, and it is exercise 1's lesson arriving a second time
wearing different clothes.

`Catalogue` now reads from a database. If `Model` picks up the SQLite library to
do that, you have put a *storage* technology in your domain exactly the way exercise 1 had a
*broker* in it, and you will have undone the fix you made in the first half hour.

So: `Catalogue` depends on `IPriceStore`, **an interface `Model` itself
declares** — "given a SKU, what is the price, and do you have one at all?" `LocalCopy/`
implements it, and is the only place that names SQLite. `Receiver/` puts the two together,
because composition is the application's job and not the domain's.

**ECST moves the dependency; it does not remove it.** The seam that kept RabbitMQ out of the
domain keeps SQLite out of it, unchanged, and that is the point worth carrying out of this
exercise: a seam you built for one reason pays for itself against a problem you had not thought
of yet.

**And it is a check rather than a matter of opinion.** `Model.csproj` references
`SimpleMessaging` and nothing else, so naming a SQLite type inside `Model/` does not
compile. Add `Microsoft.Data.Sqlite` to it to make that error go away and you have made the
mistake; the compiler is telling you the truth.

### The `PlaceOrderHandler` must not change either ###

If it does, the lookup has leaked into your domain the way `BasicGetResult` did in exercise 1 —
and it does not have to. The handler asks `Catalogue` for a price; `Catalogue`
decides where prices come from. **That is what the seam was for**, and the handler's line is the
same line it was in exercise 3.

**Notice what did and did not move.** The store is *out of process* in the sense that a different
process fills it — but reading it is a file read on the same disk, measured in microseconds, and
nothing waits on a network while a message sits unacked. Put a Redis or a shared database on the
other side of that interface instead and you are making a network call from inside the handler
again, which is `GIZMO-SLOW` wearing a different hat and the thing ECST was supposed to buy you
out of. **The interface is what lets you be wrong about that later without touching the domain.**

### What goes away, and the going is the lesson ###

`GIZMO-SLOW` and `FLAKY-1` **do not survive this exercise.** They were on-demand failures — a
lookup that hung, a lookup that was briefly unwell — and there is no longer a call to hang or to
be unwell. You did not fix them. You removed the thing that could fail, and bought a different
failure in its place, which is Probe B.

**Your agent can write all four steps**, and step 3 is the one it needs you to have decided
first: *a local copy* is not a specification until somebody says where it lives and what happens
to it when the process dies. What an agent cannot do is answer the questions below, and those are
the exercise.

### Done is ###

**Two to three hours to build, and an hour on the probes**, if you let the agent write the code
and spend your own time on the questions. It is longer than any of the three you did in the room;
that is what take-home buys. You are done when:

- a price change published by the seeder shows up in an order placed a moment later
- you have **a number** for publish-to-applied staleness, from Probe A, that you measured rather
  than estimated
- you can say what your system does when the price consumer is dead (Probe B) and when it has
  never run (Probe C), and **which of those two your code currently confuses**
- you have killed the price consumer inside its own write window (Probe D) and can say what it
  cost you
- `PlaceOrderHandler` is untouched, and `Model/` has no SQLite dependency

---

## The probes ##

These need **four terminals** — the receiver, the price consumer, the stream consumer if you want
to watch the events, and one to run the seeder and the sender from. That is one more than any
previous exercise asked for, and it is because the price consumer is a real process now.

```
dotnet run --project PriceConsumer        # terminal 1, leave it running
dotnet run --project Receiver             # terminal 2, leave it running
dotnet run --project PriceSeeder -- seed  # terminal 3: publish a starting price for every SKU
dotnet run --project Sender               # terminal 3: place an order
```

### PROBE A — how stale is it? ###

```
1. The catalogue publishes a price change. I place an order one second later.
   Which price does the order get?                                       ______

2. How would I *measure* the answer to question 1 rather than guess it?  ______

3. What is the worst case, and what makes it the worst case?             ______
```

**Then measure it.** Put a timestamp on `PriceChanged` when you publish it, and print the
difference when your **consumer applies it to the local copy**. Publish-to-applied is your
staleness, and it is the whole trade.

```
dotnet run --project PriceSeeder -- set WIDGET-1 11.99  # terminal 3, watch terminal 1 print the gap
dotnet run --project Sender                             # then place an order and see which price it got
```

Measure it to the moment the *order* is priced instead and you will get about a second — which
is how long you waited before placing the order, not what the broker cost you. That is worth
doing once, deliberately, to see the difference between the two numbers.

**On the machine these were written on it is 30 to 120 ms**, and the first record after a start is
always the slowest one — which is the connection, not the broker. Your number will differ and that
does not matter; having measured one does.

**One reading will look absurd, and it is honest.** Replay a record that has been sitting in the
log for ten minutes — which is exactly what Probe D makes you do — and publish-to-applied comes out
at ten minutes, because that is how long ago the catalogue said it. It is measuring staleness, not
latency, and on a replay those are the same arithmetic and very different facts.

▎ *Behind by one broker hop, which is the trade.* You now know what one broker hop costs on your laptop. It is not what it costs in production, but you know how to find out.

### PROBE B — stop the consumer that fills the lookup ###

**Kill the price consumer** — a real `kill -9`, not a Ctrl-C, because a clean shutdown is the
case nobody has a problem with. Leave the order pump running. Publish three price changes. Place
orders throughout.

```
kill -9 <the price consumer's PID>  # it prints it on startup
dotnet run --project PriceSeeder -- set WIDGET-1 49.99
dotnet run --project Sender
```

```
4. Do the orders succeed?                                               YES / NO
5. Are they priced correctly?                                           YES / NO
6. Does anything, anywhere, report a problem?                           ______
7. How long could this go on before someone noticed?                    ______
```

**Measured**: the orders succeed, they are priced at whatever the copy last heard, the receiver
logs an ordinary success for every one of them, and no queue, no log and no broker metric moves.
The only thing anywhere that knows is the consumer group's lag, in a process that is no longer
running to report it.

▎ **This is the failure mode ECST buys you, and it is the quiet one.** On-demand lookup fails loudly: the call times out and your queue backs up, which you saw in exercise 1. A stale local copy keeps answering, confidently, with last week's prices. **Decide which of those two failures you would rather have, and then say how you would detect the one you chose.**

### PROBE C — the empty copy, and the old one ###

Start everything from clean — **which now means deleting the database as well as the topic**,
and noticing that you had to:

```
../00-setup/reset.sh
rm -f prices.db
```

Then place an order without running the seeder at all.

```
8. What does the handler see?                                           ______
9. Is that the same as a SKU that does not exist?                       ______
10. What should it do?                                                  ______
```

**Measured, against the answer in this directory**: `'WIDGET-1' is not in the catalogue`, four
attempts about five seconds apart, and then `dead.streams.Model.PlaceOrder`. **A good order, for a
SKU that exists, thrown away because the lookup had not started yet.** Exercise 2's machinery did
exactly what you built it to do; it was told the wrong thing.

▎ "I have no copy yet" and "that SKU is not a thing" are different facts, and a dictionary lookup returns the same answer for both. Exercise 2 taught you that a failure to understand and a failure to process need different destinations. This is the same distinction one layer down.

**Now the half a map in memory could not show you.** Seed the prices, place an order, then stop
every process — the consumer, the receiver, all of it — and start them again *without* seeding.

```
11. Does the order still get priced?                                    YES / NO
12. Where did that price come from, and how old is it?                  ______
13. Which is worse: an empty copy, or one you cannot date?              ______
```

**Measured**: the receiver starts, says `holding 2 prices`, and prices the order. No consumer is
running. No seeder has run. Nothing in the output is different from a healthy system, and the
prices could be from five minutes ago or from March.

▎ **A durable copy adds a third state, and it is the dangerous one.** "No copy", "a current copy" and "a copy from some time I did not record" are three different things, and only two of them are obvious from the outside. If your local copy does not carry the time it was written, you have built something that cannot tell you whether it is right. That is a schema decision, and you make it in step 1.

### PROBE D — the dual write, in your own code ###

Exercise 3's Probe A was the receiver: a Kafka write and a RabbitMQ ack, two brokers, no
transaction. You watched it and agreed it was unfixable in place.

**This one is yours, and it is inside the price consumer.** It applies a record to the local copy,
and it commits its offset, and those are two writes with no transaction between them. The window
widens the gap so you can aim at it, exactly as `DUAL_WRITE_WINDOW` did:

```
PRICE_WRITE_WINDOW=15 dotnet run --project PriceConsumer  # terminal 1
dotnet run --project PriceSeeder -- set WIDGET-1 77.77    # terminal 3
kill -9 <the price consumer's PID>                        # while the window is open
dotnet run --project PriceConsumer                        # start it again and watch
```

**Give the consumer twenty seconds to join before you publish**, or you will sit watching a
terminal that has nothing to say. That is the group-join delay from *Two things to know before you
trust a number* in [`../00-setup/README.md`](../00-setup/README.md), and it is the single most
common reason one of these probes looks broken when it is not.

```
14. The price was applied but the offset was not committed. What happens
    on restart?                                                         ______
15. Is that a problem? Say why, in one sentence.                        ______
```

**Measured**: `lag.sh` shows the partition sitting behind the log end, the restarted consumer reads
the same record again, and the copy ends up with the price it already had. **Nothing was harmed and
nothing had to be clever.**

**Now reverse the two lines in `PriceConsumer/Program.cs`** — commit the offset first, then apply —
and run it again.

```
16. What is missing now, and when will you be offered it again?         ______
17. Which of the two orderings would you ship?                          ______
```

**Measured, and this is the one to sit with**: the copy still holds the *old* price, the restarted
consumer reads nothing at all, and **`lag.sh` reports zero lag on every partition.** The consumer
is caught up. The group is healthy. Every dashboard you have is green, and a price change has been
destroyed. *Nothing will ever offer it to you again.*

▎ **One ordering applies twice. The other loses the price for ever.** There is no third place to put that line — you have now proved it twice, in two different processes, and the second time it was your code. **Whether the duplicate is harmless is not luck: it is the *Summary or Snapshot* choice you made in step 1, paying for itself two probes later.** Choose a delta event — *"the price went up by 2.00"* — and applying it twice is simply wrong, and you have to solve this properly.

**Put the apply back before the commit before you go on**, if you intend to keep the code.

---

## Where this goes next ##

**Day 2 builds this again**, in the Paper Flow exercise, as the Catalogue Maker — and asks where
a lookup lives in a flow-based design. If you have done this, you will have already met the
answer.

**The dual write you just met in Probe D is the one worth arguing about**, because the obvious
candidate is not it. *The price consumer writes its copy, the order handler writes an order* —
those are two processes and two stores, and nobody expects one transaction across them. The one
that catches people is the one inside a single loop that looks like a single step.

**And the fix is the one exercise 3 named.** An Outbox will not help here, because there is no
database the offset lives in — the offset is Kafka's. What does help is making the apply
**idempotent**, which a snapshot price already is, or moving the offset into the same store as
the copy so that one transaction covers both. **That second answer is what a stream-processing
library does for you**, and it is worth knowing that is what you are buying when you adopt one.
