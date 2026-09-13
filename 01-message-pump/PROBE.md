<!-- Generated from the canonical exercise source (docs/01-message-pump/PROBE.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 1 — The Message Pump #

**35 minutes.** Deck reference: Day 1 §4.3 *The Message Pump*, *Translate and Dispatch*,
*Polling Consumer*, *Service Activator*.

---

## What you have ##

A producer and a consumer that work. Two terminals:

```
cd 01-message-pump
dotnet run --project Receiver  # terminal 1: the pump
dotnet run --project Sender    # terminal 2: sends one order
```

An order goes on a queue, comes off it, and gets priced. There is nothing to make work.

**And a third window, which is the one that matters: <http://localhost:15672>, `guest` / `guest`.**
Find *Queues* → `message-pump.Model.PlaceOrder`. Keep it open. Your agent cannot see it,
cannot read it, and cannot predict what it will say. That is the point.

If you would rather have the numbers in a terminal, `../00-setup/queues.sh` prints the same
ready, unacked and consumers counts, and `../00-setup/reset.sh` empties the queues between
probes. You will want the second one: these probes deliberately leave messages behind.

> **The *Consumers* column will read 0 even while your receiver is running, and that is
> correct.** This pump is a *Polling Consumer* — it asks the broker for one message at a time
> with `basic.get` rather than subscribing — so there is no consumer registered for the broker
> to count. Worth remembering when you get to exercise 3.

## The shape of every exercise today ##

> **READ** it → **PREDICT** what will happen → **BREAK** it and watch → **FIX** it.

**Write the prediction down before you run anything.** Not because we will collect it, but
because a prediction you did not write down becomes "yes, that's what I expected" the moment
you see the answer, and then you have learned nothing. There are blanks below. Use them.

**Your agent is welcome here and it will not help you with the middle two steps.** It can
read the code faster than you and it can write the fix faster than you. It cannot tell you
what the queue depth will be in ten seconds, because that is a fact about a running system,
not about the code.

---

## READ — 5 minutes, no agent ##

Open these three files, in this order, and answer the three questions. In your head is fine.

| file | |
|---|---|
| `SimpleMessaging/MessagePump.cs` | the pump: **Get → Translate → Dispatch → Handle** |
| `Model/PlaceOrderHandler.cs` | your application code |
| `Model/Model.csproj` | the domain project's dependencies |

1. **Find the four stages of the pump in `MessagePump.cs`.** Get is labelled. Where are the
   other three?
2. **`PlaceOrderHandler.Handle` takes a `BasicGetResult`.** What is a `BasicGetResult`, which
   package does it come from, and what is it doing in a method about placing orders?
3. **`Model.csproj` has a `PackageReference` to `RabbitMQ.Client`.** `Model` is the domain.
   Why does the domain need the broker's client library? Should it?

▎ Everything in this sub-topic except the handler is the **messaging gateway**. The handler is your code. Where exactly is the line, in this repo, today?

---

## PROBE A — a message that will not map — 8 minutes ##

```
dotnet run --project Sender -- unmappable
```

That publishes valid JSON of the wrong shape: `{"Id":"c0ffee","ProductCode":"WIDGET-1","Qty":1}`.
`PlaceOrder` requires `Id`, `Sku`, `Quantity`, `CustomerId` and permits nothing else, so this
body is never going to become a `PlaceOrder`. Not now, and not on the hundredth attempt.

**Predict first.** Three questions, and the third is the one people get wrong:

```
1. Is the receiver process still running 10 seconds later?      YES / NO   ______

2. How many messages are on the queue afterwards?                          ______

3. Where can I go and look at the message that broke it?                   ______
```

**Now run it.** Receiver in one terminal, sender in another, console open.

**Then answer, from the console rather than from memory:**

- Queue depth afterwards: `ready` = `____`, `unacked` = `____`
- The message is: still queued / dead-lettered / **gone** (circle one)

▎ A single malformed body, from a producer you do not control, stopped your service and destroyed the evidence.

---

## PROBE B — a handler that throws — 5 minutes ##

```
dotnet run --project Sender -- poison
```

That order is **perfectly well formed**. It maps. `NOPE-404` is simply not in the catalogue,
so `Catalogue.PriceOf` throws — the way a real lookup throws when a real service is having a
real problem.

```
4. Is this the same failure as Probe A, or a different one?               ______

5. Does the pump need to tell the two apart? Why?                         ______
```

Run it. Then, **without restarting the receiver**, send a good order:

```
dotnet run --project Sender  # a perfectly good WIDGET-1
```

- Does it get priced? `____`
- Queue depth now: `ready` = `____`

▎ One bad order stopped the service, and the good orders behind it are now piling up. That is a *poison message*, and you have just built one.

---

## PROBE C — kill it mid-handle — 5 minutes ##

**Run `../00-setup/reset.sh` first.** Probe B left a good order on the queue on purpose, and
if you leave it there the pump eats it before the slow one and your numbers below are somebody
else's.

This one needs the slow lookup. `GIZMO-SLOW` is in the catalogue, but the lookup takes 30
seconds — a downstream service having a bad afternoon.

```
dotnet run --project Sender -- slow
```

**Predict, before you run it.** While the handler is in the middle of those 30 seconds:

```
6. The console will show  ready = ______   unacked = ______

7. I kill the receiver at second 10 of the lookup. After the kill:
   the order is  (a) back on the queue and will be retried
                 (b) gone
                 (c) still locked, and released when the lock expires        ______
```

Run it. Watch the console **while the lookup is running** — that is the whole probe, and it
lasts 30 seconds, so you have time. Then, from a second terminal, using the PID the receiver
printed:

```
kill -9 <pid>
```

Watch the console for another ten seconds.

**The console's numbers refresh about every five seconds**, so give it a moment after the kill
before you decide your prediction was wrong.

- During the lookup: `ready` = `____`, `unacked` = `____`
- After the kill: `ready` = `____`, `unacked` = `____`

▎ The broker reported an idle queue while thirty seconds of work was in flight, and reported nothing at all when that work was lost. **The console was telling you the truth about what the broker knew. The broker had been misinformed.**

---

## FIX — 12 minutes ##

**Now bring the agent in.** You know what is wrong; the job is to say what right looks like.
Three changes, and the order matters because the second one depends on the first.

**1. Put the Translate stage back where it belongs.** The pump has a `Get` and a `Handle` and
nothing in between. Add a **Message Mapper** — something that turns a body into a `PlaceOrder`
— and call it from the pump, between getting the message and dispatching it.

**2. Change the handler's signature so it cannot know about messaging.**
`Handle(BasicGetResult)` becomes `Handle(PlaceOrder)`. When you are done:

- `Model.csproj` **has no reference to `RabbitMQ.Client`.** Delete it and make the build pass.
  That is your check, and it is a build error rather than a matter of opinion.
- Nothing in `Model/` has a `using RabbitMQ.Client`.

**3. Move the acknowledgement.** The pump acks a message it has not finished with. Decide where
the ack belongs, and what should happen instead when the stage before it throws.

  - For a body that will not map — is there any number of retries that helps?
  - For a handler that threw — is that the same answer?
  - **You do not have to solve this properly yet.** Exercise 2 is the proper answer, and it
    needs a dead-letter exchange you have not set up. For now, make the pump **survive**: it
    must not die, and the next message must still get processed.

### Verify the fix from the console, not from the code ###

Re-run all three probes. The console should now say something different:

| | before | after |
|---|---|---|
| **A** unmappable | pump dead, message gone | pump alive, and you *chose* to drop it rather than had it vanish |
| **B** poison | pump dead, queue backing up | pump alive, good orders still flowing |
| **C** kill mid-handle | `unacked=0` during the lookup; order lost | `unacked=1` during the lookup; order back on the queue after the kill |

**Probe C's "after" is the one to be sure of.** `unacked=1` for thirty seconds means the broker
is holding the message on your behalf for exactly as long as you are working on it. That is what
per-message acknowledgement is *for*, and it is the thing a stream will not do for you in
exercise 3.

### If you have time ###

- The pump dispatches to one hard-coded handler. §4.3 calls for a **Handler Registry** and a
  **Message Mapper Registry** — a lookup from message type to handler and to mapper. Add them,
  and notice what a *Datatype Channel* has to do with it.
- Send `dotnet run --project Sender -- burst 50` and watch the queue drain. Then start a
  **second** receiver and send another 50. That is Competing Consumers, and it costs you nothing.

---

## Carry into exercise 2 ##

You now have a pump that survives failure — but everything it survives, it survives by
**dropping on the floor**. You can say the pump does not die. You cannot say where a bad
message went, because it went nowhere.

Exercise 2 gives the two failures you found in Probes A and B two different destinations,
using RabbitMQ's own dead-letter exchange. **`02-failing-well/` already contains a correct
version of this exercise's fix**, so if you did not finish, you start level.
