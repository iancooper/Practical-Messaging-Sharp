#!/usr/bin/env bash
# Generated from the canonical exercise source (shared/00-setup/reset.sh) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit.
#
# Put the brokers back to empty. Run this between probes when you want a clean number.
#
# It deletes the exercises' queues, topic and consumer group. It does not touch anything else
# and it does not stop the containers.
#
#   ./reset.sh
#
set -uo pipefail

echo "RabbitMQ: deleting exercise queues..."
curl -fsu guest:guest 'http://localhost:15672/api/queues/%2F' \
| python3 -c '
import json, sys
for q in json.load(sys.stdin):
    if any(x in q["name"] for x in ("message-pump", "failing-well", "streams.")):
        print(q["name"])
' | while read -r q; do
  curl -fsu guest:guest -X DELETE "http://localhost:15672/api/queues/%2F/$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1],safe=''))" "$q")" \
    -o /dev/null && echo "  deleted $q"
done

container=practical-messaging-kafka
docker inspect "$container" >/dev/null 2>&1 || container=kafka

# Exercises 1 and 2 never touch Kafka, so most of the time there is nothing here to delete.
# The CLI reports that on *stdout* with a Java stack trace, so redirecting stderr does not
# hide it -- the output has to be read. A cleanup script whose only output is two exceptions
# reads like a failure, and it is not one.
#
# streams.PriceChanged and its group belong to exercise 4, which most people will not have run.
# Deleting a topic that was never created is not an error, so they are listed unconditionally.
echo "Kafka: deleting topics and consumer groups..."

for topic in streams.OrderPlaced streams.PriceChanged; do
  out=$(docker exec "$container" /opt/kafka/bin/kafka-topics.sh \
    --bootstrap-server localhost:9092 --delete --topic "$topic" 2>&1)
  case "$out" in
    "")                 echo "  deleted topic $topic" ;;
    *"does not exist"*) echo "  no topic $topic -- nothing to delete" ;;
    *)                  echo "$out" | sed 's/^/  /' ;;
  esac
done

for group in practical-messaging-streams practical-messaging-prices; do
  out=$(docker exec "$container" /opt/kafka/bin/kafka-consumer-groups.sh \
    --bootstrap-server localhost:9092 --delete --group "$group" 2>&1)
  case "$out" in
    *GroupIdNotFoundException*) echo "  no consumer group $group -- nothing to delete" ;;
    *GroupNotEmptyException*)   echo "  consumer group $group still has members -- stop its consumer and run this again" ;;
    *)                          echo "$out" | sed 's/^/  /' ;;
  esac
done

echo
echo "Clean. The queues and the topics are recreated the next time you start a consumer."
echo
echo "Exercise 4's local copy is a file on your disk, not broker state, so this script leaves"
echo "it alone. Probe C wants it gone:  rm -f ../04-lookup/prices.db"
