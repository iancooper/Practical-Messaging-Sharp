<!-- Generated from the canonical exercise source (docs/00-setup/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Setup #

```
./prereqs.sh
```

**Run it before the course, at home.** It checks your tools, pulls the two broker images, starts
them, proves you can reach both, and builds all three exercises. It is safe to run repeatedly.

It builds the optional take-home, `04-lookup`, as well — but **reports it as a NOTE rather than
counting it as a failure**, because nobody's setup is broken by an exercise they are not going to
start. The summary says which way it went either way; silence would be worse than a FAIL.

| | |
|---|---|
| `docker-compose.yml` | RabbitMQ and Kafka, one container each |
| `prereqs.sh` | pull, start, and check everything. **Do this at home** |
| `queues.sh` | RabbitMQ: ready, unacked and consumers per queue |
| `peek.sh` | RabbitMQ: one message, its body and its headers — and puts it back |
| `lag.sh` | Kafka: current offset, log end and lag per partition |
| `reset.sh` | delete the exercises' queues, topics and consumer groups. **Stop your consumers first** |

**`reset.sh` deletes those queues rather than emptying them**, which matters because the
consumer is the only thing that declares them. Stop your receiver and your stream consumer,
run it, and start them again — a consumer left polling a queue that has just been deleted is
a confusing five minutes, and none of it is about messaging.

**It does not delete exercise 4's local copy**, and that is deliberate rather than an oversight:
`04-lookup/prices.db` is a file on your disk and not broker state, so "reset the brokers" does not
and should not reach it. The script says so when it finishes. Probe C is the one that wants it
gone, and it wants you to notice that you had to ask.

## Starting and stopping ##

```
docker compose up -d          # or docker-compose up -d
docker compose down           # volumes persist; your messages survive
docker compose down -v        # and now they don't
```

RabbitMQ management console: <http://localhost:15672>, `guest` / `guest`.

## Two things to know before you trust a number ##

Both of these will, at some point, show you a number that makes a correct prediction look wrong.
Neither is a quirk of the exercises.

### 1. RabbitMQ's console refreshes on a timer — about every five seconds ###

So immediately after you kill a consumer, the console may still show the state from before you
killed it. If a probe's number looks wrong, wait ten seconds and look again before you conclude
your prediction was wrong. It usually was not.

This is how you will misread a production dashboard one day. `queues.sh` reads the same API and has
the same delay.

### 2. Kafka keeps a dead consumer's partitions for up to 45 seconds ###

A consumer group holds a member until its session times out — 45 seconds by default — and the
broker cannot tell a process that has died from one that is merely slow. So if you stop a stream
consumer and start another straight away, or run `reset.sh` and start one immediately, **the new
consumer can join the group, be given no partitions at all, and sit in silence** while records pile
up behind it.

**That looks exactly like a lost event and it is not one.** `lag.sh` is what tells you apart, and
the column that answers it is **CONSUMER-ID, not CURRENT**: a partition nobody holds prints
`NOT ASSIGNED`, and a partition somebody holds but has never committed on prints `held, nothing
committed yet`. Both show a dash for CURRENT, which is why the dash on its own tells you nothing.
Wait for the assignment before you believe anything a quiet consumer is telling you.

This is the stream-shaped version of the same lesson: **the broker's answer is about what the broker
currently believes, not about what is true.**

## Why this compose file looks the way it does ##

**Two containers, and that is on purpose.** Kafka runs in **KRaft** mode, so there is no
Zookeeper, no schema registry and no control centre — none of which any exercise uses, and
which between them are the difference between a 700 MB download and a 3 GB one on a conference
network.

RabbitMQ's `hostname:` is pinned because it keeps its message store in a directory named after
the host. Without the pin, a restart lands the store somewhere else and the "persistent"
messages you were about to demonstrate are gone.

Kafka's topics are created with **three partitions** by the code rather than by the broker, so
that the partition count is the exercise's decision and not a broker setting — exercise 3 needs
more than one partition to make its point. There is one topic, `streams.OrderPlaced`, until the
optional exercise 4 adds `streams.PriceChanged` beside it.

## If something is already on those ports ##

The file wants **5672** and **15672** (RabbitMQ) and **9092** (Kafka). **If you already run
either broker locally, stop your containers for the day.** That really is the cheaper answer,
and here is why moving the exercises instead is worse than it looks:

- **There is one copy of the gateway per exercise directory**, so the RabbitMQ address in
  `SimpleMessaging/` exists three times — once in `01-message-pump`, once in `02-failing-well`,
  once in `03-streams` — and changing the first one does not change the other two. The Kafka
  address in `SimpleEventing/` exists once, in `03-streams`.
- **The RabbitMQ address in the code is a host name, not a port.** Nothing in
  `SimpleMessaging/` names 5672 at all; it is the AMQP default and the client supplies it. So
  moving RabbitMQ's AMQP port is not a string edit — it is a change to how the connection is
  opened.
- **`queues.sh`, `peek.sh` and `reset.sh` have 15672 in them**, because the management API is
  where they get their numbers. Move the console and you move those too.

If you have to do it, **do it at home**, and run one probe from exercise 1 afterwards to prove
it took.
