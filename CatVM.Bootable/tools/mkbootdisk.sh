#!/bin/sh
# Builds a disk image for the examples/diskloader.cat firmware.
#
# The image gets a boot record in block 0 describing the payload, and the payload
# itself from block 1 onwards. Nothing in the machine knows about this format: it
# is the loader example's own, and an OS would define its own instead. See
# examples/diskloader.cat for the layout.
#
# Usage: mkbootdisk.sh <payload.rom> <output.img> [origin] [size]
#
#   origin  address the payload is assembled to run at (default 0x2400)
#   size    final image size, passed to truncate (default 16M)
#
# catasm emits flat ROMs whose labels are absolute addresses counted from 0, so a
# payload that is going to run at some other address has to be assembled with
# that address reserved in front of it:
#
#   #const ORG, 0x2400
#       res8 ORG
#   main:
#       ...
#
# This script strips those leading zero bytes back off (it refuses to continue if
# they are not zero) and tells the firmware to load what is left at the origin.
# The origin must be at or above 0x2400, which is where the firmware's own code
# and scratch space end.
set -eu

if [ $# -lt 2 ]; then
    sed -n '2,25p' "$0" | sed 's/^# \?//'
    exit 1
fi

PAYLOAD=$1
IMAGE=$2
ORIGIN=$(( ${3:-0x2400} ))
SIZE=${4:-16M}

BLOCK_SIZE=512
MAGIC=0x42544143   # "CATB"
LOAD_MIN=0x2400

if [ ! -f "$PAYLOAD" ]; then
    echo "mkbootdisk: $PAYLOAD not found" >&2
    exit 1
fi

if [ "$ORIGIN" -lt $(( LOAD_MIN )) ]; then
    printf 'mkbootdisk: origin 0x%x is below the firmware limit 0x%x\n' "$ORIGIN" $(( LOAD_MIN )) >&2
    exit 1
fi

file_size=$(wc -c < "$PAYLOAD")

if [ "$file_size" -le "$ORIGIN" ]; then
    printf 'mkbootdisk: %s is only %s bytes, it cannot be assembled for 0x%x\n' \
        "$PAYLOAD" "$file_size" "$ORIGIN" >&2
    exit 1
fi

# The reserved area in front of the payload must really be empty, otherwise the
# payload was not assembled for this origin and stripping it would lose code.
padding_bytes=$(dd if="$PAYLOAD" bs=1 count="$ORIGIN" 2>/dev/null | tr -d '\000' | wc -c)
if [ "$padding_bytes" -ne 0 ]; then
    printf 'mkbootdisk: the first 0x%x bytes of %s are not empty, so it is not assembled for that origin\n' \
        "$ORIGIN" "$PAYLOAD" >&2
    exit 1
fi

payload_size=$(( file_size - ORIGIN ))
blocks=$(( (payload_size + BLOCK_SIZE - 1) / BLOCK_SIZE ))

# Emits a 32 bit little endian value. The inner printf builds an escape sequence
# rather than the bytes themselves so that NUL bytes survive the command
# substitution.
put_u32() {
    v=$(( $1 ))
    printf "$(printf '\\%03o\\%03o\\%03o\\%03o' \
        $(( v & 255 )) $(( (v >> 8) & 255 )) $(( (v >> 16) & 255 )) $(( (v >> 24) & 255 )))"
}

: > "$IMAGE"

{
    put_u32 "$MAGIC"
    put_u32 "$ORIGIN"
    put_u32 "$blocks"
    put_u32 "$ORIGIN"
} >> "$IMAGE"

# Pad the boot record out to a full block, then append the payload without its
# reserved area.
truncate -s "$BLOCK_SIZE" "$IMAGE"
dd if="$PAYLOAD" bs=1 skip="$ORIGIN" oflag=append conv=notrunc of="$IMAGE" status=none

# Round the payload up to a whole number of blocks, then grow the image.
truncate -s $(( (1 + blocks) * BLOCK_SIZE )) "$IMAGE"
truncate -s ">$SIZE" "$IMAGE"

printf 'mkbootdisk: %s\n' "$IMAGE"
printf '  payload     %s (%s bytes of code, %s blocks)\n' "$PAYLOAD" "$payload_size" "$blocks"
printf '  origin      0x%x\n' "$ORIGIN"
printf '  image size  %s bytes\n' "$(wc -c < "$IMAGE")"
