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
3. **A consumer** that follows the topic and maintains a local copy.
4. **`Catalogue` reads the local copy** instead of its dictionary. `PriceOf` no longer throws
   `CatalogueUnavailableException`, because there is nothing left to be unavailable.

The `PlaceOrderHandler` must not change. If it does, the lookup has leaked into your domain the
way `BasicGetResult` did in exercise 1.

**Your agent can write all four of those.** The specification above is about as much as it needs.
What it cannot do is answer the questions below, and those are the exercise.

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

**Exercise 3's Probe A is also waiting for you here.** The price consumer writes to a local
store. The order handler reads it and writes an order. Those are two writes, and you have met
that problem before.
