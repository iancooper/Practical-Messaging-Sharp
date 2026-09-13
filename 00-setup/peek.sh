#!/usr/bin/env bash
# Generated from the canonical exercise source (shared/00-setup/peek.sh) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit.
#
# Look at a message without taking it off the queue.
#
# This is what *Get Message* does in the management console, from a terminal. It asks for the
# message, prints its headers and its body, and puts it back -- so you can run it as often as
# you like and the queue depth does not change.
#
# You want this in exercise 2, for the `x-death` header. That header is RabbitMQ's record of
# a message's entire history, and it is the thing the pump reads to decide whether to retry.
#
#   ./peek.sh dead.failing-well.Model.PlaceOrder      the message and its headers
#   ./peek.sh invalid.failing-well.Model.PlaceOrder
#   ./peek.sh dead.failing-well.Model.PlaceOrder 3    the first three, if there are several
#
set -uo pipefail

queue="${1:-}"
count="${2:-1}"

if [ -z "$queue" ]; then
  echo "Usage: ./peek.sh <queue-name> [count]" >&2
  echo >&2
  echo "Run ./queues.sh first if you want the exact spelling of a queue name." >&2
  exit 2
fi

# The queue name goes in the path, and it contains dots and may contain anything else, so it
# has to be percent-encoded. The vhost is "/", which encodes to %2F.
encoded=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1],safe=''))" "$queue")

# ackmode ack_requeue_true is the console's default: hand it to us, then put it back. Anything
# else here removes the message, which is not what a *peek* should do.
body=$(printf '{"count":%d,"ackmode":"ack_requeue_true","encoding":"auto","truncate":50000}' "$count")

out=$(curl -fsu guest:guest -H 'content-type:application/json' \
        -X POST "http://localhost:15672/api/queues/%2F/${encoded}/get" -d "$body" 2>&1) || {
  echo "Could not read '$queue'." >&2
  echo "Either the broker is not up, or that queue does not exist -- ./queues.sh lists them." >&2
  exit 1
}

echo "$out" | python3 -c '
import json, sys

msgs = json.load(sys.stdin)
if not msgs:
    print("The queue is empty. Nothing to look at -- which, for the invalid and dead letter")
    print("queues, is the answer you want nearly all of the time.")
    raise SystemExit

for i, m in enumerate(msgs, 1):
    print("--- message %d of %d " % (i, len(msgs)) + "-" * 52)
    print("  body: %s" % m.get("payload", ""))
    headers = (m.get("properties") or {}).get("headers") or {}

    deaths = headers.pop("x-death", None)
    for k in sorted(headers):
        print("  %s: %s" % (k, headers[k]))

    if deaths is None:
        print("  x-death: (none -- this message has never been dead-lettered)")
        continue

    # One entry per (queue, reason) pair, each with its own count. The pump reads the entry
    # for the *retry* queue: that is the number of laps round the retry cycle.
    print("  x-death: %d entr%s" % (len(deaths), "y" if len(deaths) == 1 else "ies"))
    for d in deaths:
        print("    count=%-4s reason=%-12s queue=%s"
              % (d.get("count"), d.get("reason"), d.get("queue")))

print()
print("Still on the queue -- this put it back, the way the console does.")
'
