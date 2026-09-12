#!/usr/bin/env bash
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

echo "Kafka: deleting topic and consumer group..."
docker exec "$container" /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server localhost:9092 --delete --topic streams.OrderPlaced 2>/dev/null \
  && echo "  deleted topic streams.OrderPlaced"
docker exec "$container" /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 --delete --group practical-messaging-streams 2>/dev/null \
  | sed 's/^/  /'

echo
echo "Clean. The queues and the topic are recreated the next time you start a consumer."
