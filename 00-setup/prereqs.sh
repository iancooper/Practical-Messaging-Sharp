#!/usr/bin/env bash
# Generated from the canonical exercise source (shared/00-setup/prereqs.sh) by emit.py, which lives in the practical-messaging-samples working directory alongside the five language repos. Do not edit this copy -- edit the canonical source and re-emit.
#
# Run this BEFORE the course, on the machine you are bringing, on a network you trust.
#
# It pulls the two broker images and proves they start. That is the part of the day that
# takes twenty minutes on venue wifi and there is no reason to spend the exercise slot on it.
#
#   ./prereqs.sh
#
set -uo pipefail
cd "$(dirname "$0")"

pass=0; fail=0
ok()   { printf '  \033[32mOK\033[0m    %s\n' "$1"; pass=$((pass+1)); }
bad()  { printf '  \033[31mFAIL\033[0m  %s\n' "$1"; fail=$((fail+1)); }
# Reports, and counts as neither. Exercise 4 is optional and taken home, so a delegate who is
# never going to start it should not be told their setup is broken because of it.
note() { printf '  \033[33mNOTE\033[0m  %s\n' "$1"; }
step() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# Set by step 7. Reported either way, because silence about it would be worse than a FAIL.
ex4="not checked"

step "1. Tools"
if command -v docker >/dev/null 2>&1; then ok "docker $(docker --version | sed 's/Docker version //;s/,.*//')"
else bad "docker not found -- install Docker Desktop"; fi

# Docker Desktop ships compose as a subcommand; homebrew ships it as its own binary.
# Both work; find whichever you have.
if docker compose version >/dev/null 2>&1;  then DC="docker compose";  ok "docker compose (plugin)"
elif docker-compose version >/dev/null 2>&1; then DC="docker-compose";  ok "docker-compose (standalone)"
else DC="docker compose"; bad "no docker compose found -- install Docker Desktop or 'brew install docker-compose'"; fi

if docker info >/dev/null 2>&1; then ok "docker daemon is running"
else bad "docker daemon is not running -- start Docker Desktop"; fi

if command -v dotnet >/dev/null 2>&1; then
  sdk=$(dotnet --version 2>/dev/null)
  major=${sdk%%.*}
  if [ "${major:-0}" -ge 10 ] 2>/dev/null; then ok "dotnet SDK $sdk"
  else bad "dotnet SDK $sdk -- the exercises target net10.0, install the .NET 10 SDK"; fi
else bad "dotnet not found -- install the .NET 10 SDK"; fi

step "2. Ports"
# The exercises need these three free. A port already in use is by far the most common
# setup failure, and Docker's own error for it ("proxy already running") says nothing useful.
busy=""
for spec in "5672:RabbitMQ (AMQP)" "15672:RabbitMQ (management console)" "9092:Kafka"; do
  port=${spec%%:*}; what=${spec#*:}
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    owner=$(docker ps --format '{{.Names}}\t{{.Ports}}' | awk -v p=":$port->" 'index($0,p){print $1; exit}')
    bad "port $port ($what) is already in use${owner:+ by container '$owner'}"
    busy="yes"
  else
    ok "port $port free -- $what"
  fi
done
if [ -n "$busy" ]; then
  printf '\n  Something already holds a port the exercises need. Either stop it:\n'
  printf '      docker ps            # find it\n      docker stop <name>\n'
  printf '  or change the ports in docker-compose.yml and in the two constants the code uses\n'
  printf '  (SimpleMessaging/Channel.cs and SimpleEventing/Stream.cs). See README.md.\n'
fi

step "3. Images (this is the slow part -- do it at home)"
if $DC pull; then ok "images pulled"; else bad "could not pull images"; fi

step "4. Brokers start"
started=""
if [ -n "$busy" ]; then
  bad "not starting the brokers -- a port they need is in use (see above)"
elif $DC up -d; then
  ok "compose up"; started="yes"
else
  bad "compose up failed -- see the error above"
fi

if [ -n "$started" ]; then
printf '  waiting for health'
for _ in $(seq 1 60); do
  rmq=$(docker inspect -f '{{.State.Health.Status}}' practical-messaging-rmq   2>/dev/null || echo none)
  kfk=$(docker inspect -f '{{.State.Health.Status}}' practical-messaging-kafka 2>/dev/null || echo none)
  [ "$rmq" = healthy ] && [ "$kfk" = healthy ] && break
  printf '.'; sleep 2
done
printf '\n'
[ "${rmq:-}" = healthy ] && ok "rabbitmq healthy" || bad "rabbitmq not healthy (status: ${rmq:-none})"
[ "${kfk:-}" = healthy ] && ok "kafka healthy"    || bad "kafka not healthy (status: ${kfk:-none})"
fi

step "5. You can reach them"
if curl -fsu guest:guest http://localhost:15672/api/overview >/dev/null 2>&1
then ok "management console on http://localhost:15672 (guest/guest)"
else bad "cannot reach the RMQ management console on port 15672"; fi

if docker exec practical-messaging-kafka /opt/kafka/bin/kafka-broker-api-versions.sh \
     --bootstrap-server localhost:9092 >/dev/null 2>&1
then ok "kafka answering on localhost:9092"
else bad "kafka not answering on port 9092"; fi

# Docker's Linux VM keeps its own clock and it drifts while the machine is asleep. Kafka
# rejects any record whose timestamp is more than an hour ahead of the broker, and the error
# it gives you is "Broker: Invalid timestamp" -- which does not mention clocks, does not
# mention Docker, and sends people looking at their code. Check it here instead.
step "6. The clocks agree"
vm=$(docker exec practical-messaging-kafka date -u +%s 2>/dev/null || echo "")
if [ -z "$vm" ]; then
  bad "cannot read the container's clock -- is Kafka running?"
else
  skew=$(( $(date -u +%s) - vm )); [ "$skew" -lt 0 ] && skew=$(( -skew ))
  if   [ "$skew" -le 60 ];  then ok "host and container clocks agree (${skew}s apart)"
  elif [ "$skew" -lt 1800 ]; then note "clocks are ${skew}s apart and drifting -- resync before the course (see below)"
  else
    bad "clocks are ${skew}s apart -- Kafka will reject every record you publish"
  fi
  if [ "$skew" -gt 60 ]; then
    printf '\n  Docker'"'"'s VM clock drifts while the machine sleeps. At an hour out, Kafka\n'
    printf '  refuses every record with "Broker: Invalid timestamp", which says nothing\n'
    printf '  about clocks. Resync it:\n'
    printf '      docker run --rm --privileged alpine hwclock -s\n'
    printf '  or restart Docker Desktop, and run this again.\n'
  fi
fi

step "7. The code builds"
for d in ../01-message-pump ../02-failing-well ../03-streams; do
  if (cd "$d" && dotnet build -v q --nologo >/dev/null 2>&1)
  then ok "$(basename "$d") builds"
  else bad "$(basename "$d") does not build -- run 'dotnet build' in it to see why"; fi
done

# The take-home, reported but not counted -- see 'note' at the top of this file.
if (cd ../04-lookup && dotnet build -v q --nologo >/dev/null 2>&1)
then ok "04-lookup builds (optional, take home)"; ex4="builds"
else note "04-lookup does not build -- it is optional, so this is not a failure"
     ex4="DOES NOT BUILD -- run 'dotnet build' in 04-lookup to see why"; fi

printf '\n\033[1m%d passed, %d failed\033[0m\n' "$pass" "$fail"
printf 'Exercise 4 (optional, take home): %s\n' "$ex4"
if [ "$fail" -eq 0 ]; then
  printf 'You are ready. Leave the containers up, or run "%s down" -- the volumes persist either way.\n' "$DC"
else
  printf 'Fix the failures above before the course. Bring this output with you if you are stuck.\n'
fi
exit $(( fail > 0 ))
