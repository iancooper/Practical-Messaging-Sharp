#!/usr/bin/env bash
# Generated from the canonical exercise source (shared/00-setup/lag.sh) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit.
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

# Capture rather than pipe. The CLI writes its failures to stdout, exits 0 while doing it, and
# mixes prose in with the table -- so neither the exit status nor a pipe can be trusted, and the
# output has to be read.
out=$(docker exec "$container" /opt/kafka/bin/kafka-consumer-groups.sh \
        --bootstrap-server localhost:9092 --describe --group "$GROUP" 2>&1)

case "$out" in
  *GroupIdNotFoundException*)
    echo "No consumer group '$GROUP'. Nothing has ever read this topic under that name." >&2
    exit 1 ;;
  *Exception*|*"Error while"*)
    echo "Kafka could not describe '$GROUP'. Is the broker up?" >&2
    echo "$out" | head -3 >&2
    exit 1 ;;
esac

# Only lines whose PARTITION column is a number are data; everything else the CLI prints --
# the header, "has no active members", a stray blank -- is not, and must not be formatted as
# though it were.
#
# CONSUMER-ID ($7) is the field that says whether anyone is holding the partition. CURRENT ($4)
# is a dash whenever the group has never committed there -- which is true of a partition nobody
# holds AND of a partition held by a consumer stuck on its very first record. Those are
# different problems and only $7 tells them apart.
echo "$out" | awk '
  BEGIN {printf "%-10s %-22s %-9s %-9s %-7s %s\n", "PARTITION","TOPIC","CURRENT","LOG-END","LAG","STATE"}
  $3 ~ /^[0-9]+$/ && NF >= 7 {
    if ($7 == "-")      state = "NOT ASSIGNED -- nobody holds it"
    else if ($4 == "-") state = "held, nothing committed yet"
    else if ($6 == "0") state = "caught up"
    else                state = "BEHIND"
    printf "%-10s %-22s %-9s %-9s %-7s %s\n", $3, $2, $4, $5, $6, state
  }'

case "$out" in
  *"has no active members"*) echo; echo "(Nobody is in the group right now.)" ;;
esac

echo
echo "A partition whose CURRENT offset never moves while LOG-END climbs is stuck on one record."
echo "If CURRENT is a dash and someone IS holding it, it is stuck on the very first one."
