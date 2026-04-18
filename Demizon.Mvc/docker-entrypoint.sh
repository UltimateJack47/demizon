#!/bin/sh
# Počkej až bude /data writable (Railway volume mount timing)
echo "Waiting for /data volume to become available..."
MAX_WAIT=60
WAITED=0
while [ $WAITED -lt $MAX_WAIT ]; do
    if touch /data/.probe 2>/dev/null; then
        rm -f /data/.probe
        echo "/data is writable after ${WAITED}s"
        break
    fi
    sleep 1
    WAITED=$((WAITED + 1))
done

if [ $WAITED -ge $MAX_WAIT ]; then
    echo "WARNING: /data not writable after ${MAX_WAIT}s, starting anyway..."
fi

exec dotnet Demizon.Mvc.dll
