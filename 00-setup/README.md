<!-- Generated from the canonical exercise source (docs/00-setup/README.md) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit. -->
# Setup #

```
./prereqs.sh
```

**Run it before the course, at home.** It checks your tools, pulls the two broker images, starts
them, proves you can reach both, and builds all three exercises. It is safe to run repeatedly.

| | |
|---|---|
| `docker-compose.yml` | RabbitMQ and Kafka, one container each |
| `prereqs.sh` | pull, start, and check everything. **Do this at home** |
| `queues.sh` | RabbitMQ: ready, unacked and consumers per queue |
| `lag.sh` | Kafka: current offset, log end and lag per partition |
| `reset.sh` | delete the exercises' queues, topic and consumer group |

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

Kafka's topic is created with **three partitions** by the code rather than by the broker, so
that the partition count is the exercise's decision and not a broker setting — exercise 3 needs
more than one partition to make its point.

## If something is already on those ports ##

The file wants **5672** and **15672** (RabbitMQ) and **9092** (Kafka). If you already run either
broker locally, stop your containers for the day — or change the ports here and in the two
constants the code uses: `SimpleMessaging.Channel` (RabbitMQ host) and `SimpleEventing.Stream`
(`BootstrapServers`).
