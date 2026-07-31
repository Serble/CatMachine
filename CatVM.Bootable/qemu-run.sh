#!/bin/sh
# Boots the image in QEMU, with a display, so the machine can be tried without
# real hardware.
#
# The image's own disk is excluded from the guest automatically (the machine never
# hands the guest a disk the host is using), so if the firmware wants a disk it
# needs a second one. BOOT_DISK attaches one; it becomes the machine's first disk.
# A firmware that is the whole program needs none.
#
# Environment:
#   IMAGE      the bootable image (alpine.raw)
#   BOOT_DISK  disk to give the Cat machine (catdisk.img, skipped if missing)
#   BOOT_MODE  BIOS or UEFI, must match how the image was built (BIOS)
#   OVMF       UEFI firmware to use in UEFI mode (autodetected)
#   MEMORY     RAM for the QEMU guest, not for the Cat machine (1G)
#   DISPLAY_BACKEND  QEMU -display value (gtk)
#   KVM        no to run without hardware acceleration (yes)
set -eu

cd "$(dirname "$0")"

: "${IMAGE:=alpine.raw}"
: "${BOOT_DISK:=catdisk.img}"
: "${BOOT_MODE:=BIOS}"
: "${MEMORY:=1G}"
: "${DISPLAY_BACKEND:=gtk}"
: "${KVM:=yes}"

[ -f "$IMAGE" ] || { echo "qemu-run.sh: $IMAGE not found, run ./build.sh first" >&2; exit 1; }

set -- \
    -machine q35 \
    -m "$MEMORY" \
    -smp 2 \
    -drive "file=$IMAGE,format=raw,if=virtio" \
    -device virtio-vga \
    -display "$DISPLAY_BACKEND" \
    -usb -device usb-tablet \
    -serial mon:stdio

if [ "$KVM" = 'yes' ] && [ -w /dev/kvm ]; then
    set -- "$@" -enable-kvm -cpu host
else
    echo 'qemu-run.sh: running without KVM, the display will be slow'
fi

if [ -f "$BOOT_DISK" ]; then
    set -- "$@" -drive "file=$BOOT_DISK,format=raw,if=virtio"
    echo "qemu-run.sh: giving the Cat machine $BOOT_DISK"
else
    echo "qemu-run.sh: $BOOT_DISK not found, the firmware will have nothing to boot"
fi

if [ "$BOOT_MODE" = 'UEFI' ]; then
    if [ -z "${OVMF:-}" ]; then
        for candidate in \
            /usr/share/edk2/ovmf/OVMF_CODE.fd \
            /usr/share/OVMF/OVMF_CODE.fd \
            /usr/share/qemu/ovmf-x86_64-code.bin
        do
            [ -f "$candidate" ] && { OVMF=$candidate; break; }
        done
    fi

    [ -n "${OVMF:-}" ] || { echo 'qemu-run.sh: no OVMF firmware found, set OVMF=' >&2; exit 1; }
    set -- "$@" -drive "if=pflash,format=raw,unit=0,readonly=on,file=$OVMF"
fi

echo "qemu-run.sh: qemu-system-x86_64 $*"
exec qemu-system-x86_64 "$@"
