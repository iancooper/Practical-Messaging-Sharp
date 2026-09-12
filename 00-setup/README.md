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

## One thing to know before you trust a number ##

**The management console's statistics refresh on a timer — about every five seconds.**

So immediately after you kill a consumer, the console may still show the state from before you
killed it. If a probe's number looks wrong, wait ten seconds and look again before you conclude
your prediction was wrong. It usually was not.

This is not a quirk of the exercises; it is how you will misread a production dashboard one day.
`queues.sh` reads the same API and has the same delay.

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
