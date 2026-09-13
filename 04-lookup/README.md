<!-- Generated from the canonical exercise source (docs/04-lookup/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Exercise 4 — The Lookup *(optional, take home)* #

**There is no code in this directory, and that is deliberate.** Exercises 1 to 3 gave you a
working system and asked you to predict how it fails. This one gives you a specification and
asks you to build it — which is the other half of the skill, and the half an agent is genuinely
good for.

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

**Start from `03-streams`.** Copy it, and add:

1. **A `PriceChanged` event and a topic for it.** A SKU and a price, on the Kafka stream
   gateway in `SimpleEventing/` that exercise 3 gave you, keyed so that events for
   one SKU stay in order. Think about whether it should carry the new price or just say that
   the price changed — §6.3 *Domain or Delta Event* and *Summary or Snapshot*, and the choice
   matters here more than it looks.
2. **A seeder** that publishes prices onto that topic, and can change one on demand.
3. **A consumer** that follows the topic and maintains a local copy. **Run it in the receiver's
   own process**, on a thread or task of its own, and let the local copy be an in-memory map
   that both it and `Catalogue` can see. That is the simplest thing that works, it is
   what most ECST consumers start as, and it keeps step 4 to a one-line change. A copy in a
   database or a Redis is the honest production answer and a much bigger exercise — do that
   second, if you do it at all, and notice what it does to *Where this goes next* at the bottom
   of this page.
4. **`Catalogue` reads the local copy** instead of its dictionary. `PriceOf` no longer throws
   `CatalogueUnavailableException`, because there is nothing left to be unavailable.

The `PlaceOrderHandler` must not change. If it does, the lookup has leaked into your domain the
way `BasicGetResult` did in exercise 1 — and it does not have to. The handler asks
`Catalogue` for a price; `Catalogue` decides where prices come from. Put an
in-process map behind it, kept current by the consumer in step 3, and the handler's line is the
same line. **That is what the seam was for.** Move the copy out of process instead and you are
making a network call while a message sits unacked — which is `GIZMO-SLOW` wearing a different
hat, and the thing ECST was supposed to buy you out of.

**Your agent can write all four of those**, and step 3 is the one it needs you to have decided
first: *a local copy* is not a specification until somebody says where it lives. What it cannot
do is answer the questions below, and those are the exercise.

### Done is ###

**About two hours to build, and an hour on the probes**, if you let the agent write the code and
spend your own time on the questions. It is longer than any of the three you did in the room;
that is what take-home buys. You are done when:

- a price change published by the seeder shows up in an order placed a moment later
- you have **a number** for publish-to-applied staleness, from Probe A, that you measured rather
  than estimated
- you can say what your system does when the price consumer is dead (Probe B) and when it has
  never run (Probe C), and **which of those two your code currently confuses**
- `PlaceOrderHandler` is untouched

---

## The probes ##

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

Measure it to the moment the *order* is priced instead and you will get about a second — which
is how long you waited before placing the order, not what the broker cost you. That is worth
doing once, deliberately, to see the difference between the two numbers.

▎ *Behind by one broker hop, which is the trade.* You now know what one broker hop costs on your laptop. It is not what it costs in production, but you know how to find out.

### PROBE B — stop the consumer that fills the lookup ###

Kill the price consumer. Leave the order pump running. Publish three price changes. Place
orders throughout.

```
4. Do the orders succeed?                                               YES / NO
5. Are they priced correctly?                                           YES / NO
6. Does anything, anywhere, report a problem?                           ______
7. How long could this go on before someone noticed?                    ______
```

▎ **This is the failure mode ECST buys you, and it is the quiet one.** On-demand lookup fails loudly: the call times out and your queue backs up, which you saw in exercise 1. A stale local copy keeps answering, confidently, with last week's prices. **Decide which of those two failures you would rather have, and then say how you would detect the one you chose.**

### PROBE C — the empty copy ###

Start everything from clean, with the price topic empty, and place an order.

```
8. What does the handler see?                                           ______
9. Is that the same as a SKU that does not exist?                       ______
10. What should it do?                                                  ______
```

▎ "I have no copy yet" and "that SKU is not a thing" are different facts, and a dictionary lookup returns the same answer for both. Exercise 2 taught you that a failure to understand and a failure to process need different destinations. This is the same distinction one layer down.

---

## Where this goes next ##

**Day 2 builds this again**, in the Paper Flow exercise, as the Catalogue Maker — and asks where
a lookup lives in a flow-based design. If you have done this, you will have already met the
answer.

**Exercise 3's Probe A is waiting for you here too, and it is closer than it looks.** The
obvious candidate — the price consumer writes its copy, the order handler writes an order — is
not it: those are two processes and two stores, and nobody expects one transaction across them.

The dual write is **inside the price consumer**. It applies a record to its local copy, and it
commits its offset, and those are two writes with no transaction between them. Commit first and
die, and the copy is missing a price it will never be offered again. Apply first and die, and
you apply it twice — which is harmless here, because a price is a snapshot and last-writer-wins,
and that is not an accident: it is §6.3's *Summary or Snapshot* choice paying for itself one
exercise later. **Choose a delta event in step 1 and you have to solve this properly.**
