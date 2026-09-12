#!/usr/bin/env bash
#
# What Kafka thinks is going on: the consumer group's offset per partition, and the lag.
#
# There is no management console here -- that is not an oversight, it is the actual difference
# between operating a queue and operating a stream. RabbitMQ hands you a web UI that shows you
# a message. Kafka hands you a log, an offset, and a subtraction.
#
# **Lag is the only symptom a stuck partition has.** A partition retrying one bad record in
# place looks exactly like a partition under load, until you notice its offset is not moving.
#
#   ./lag.sh
#
set -uo pipefail
GROUP="${1:-practical-messaging-streams}"

container=practical-messaging-kafka
docker inspect "$container" >/dev/null 2>&1 || container=kafka

docker exec "$container" /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 --describe --group "$GROUP" \
| awk 'BEGIN {printf "%-10s %-22s %-9s %-9s %-7s %s\n", "PARTITION","TOPIC","CURRENT","LOG-END","LAG","STATE"}
       $3 == "PARTITION" || NF < 6 { next }
       { printf "%-10s %-22s %-9s %-9s %-7s %s\n", $3, $2, $4, $5, $6,
                ($4 == "-" ? "not assigned" : ($6 == "0" ? "caught up" : "BEHIND")) }'

echo
echo "A partition whose CURRENT offset never moves while LOG-END climbs is stuck on one record."
