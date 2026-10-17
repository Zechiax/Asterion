#!/bin/sh
set -e

# As root create the data directory
if [ "$(id -u)" = "0" ]; then
    mkdir -p "$ASTERION_DATA_DIR"
    find "$ASTERION_DATA_DIR" \! -user "$PUID" -exec chown "$PUID:$PGID" {} +
    exec setpriv --reuid="$PUID" --regid="$PGID" --clear-groups -- "$@"
fi

exec "$@"
