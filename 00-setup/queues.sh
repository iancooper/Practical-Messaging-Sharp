#!/usr/bin/env bash
#
# What RabbitMQ thinks is going on. The management console at http://localhost:15672 shows all
# of this and more, and you should use it -- but this is quicker when you want the number.
#
#   ./queues.sh              every practical-messaging queue
#   ./queues.sh failing      only queues matching 'failing'
#
set -uo pipefail
filter="${1:-}"

curl -fsu guest:guest 'http://localhost:15672/api/queues/%2F' \
| python3 -c '
import json, sys
f = sys.argv[1] if len(sys.argv) > 1 else ""
qs = [q for q in json.load(sys.stdin)
      if any(x in q["name"] for x in ("message-pump", "failing-well", "streams.")) and f in q["name"]]
if not qs:
    print("No matching queues. Has the consumer been started at least once?")
    raise SystemExit
print("%-46s%7s%9s%11s" % ("queue", "ready", "unacked", "consumers"))
print("-" * 73)
for q in sorted(qs, key=lambda q: q["name"]):
    print("%-46s%7d%9d%11d" % (q["name"], q["messages_ready"],
                               q["messages_unacknowledged"], q["consumers"]))
' "$filter"
