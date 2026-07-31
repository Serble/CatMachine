#!/bin/sh
# Runs inside the image, chrooted, once the base system, the packages and the
# staged files are in place. Turns a stock Alpine install into an appliance whose
# only job is to be a Cat machine.
set -eu

BOOT_MODE=${1:-BIOS}
OPS=${2:-}
MEMORY=${3:-}

APP=/opt/catvm/vm
FIRMWARE=/opt/catvm/firmware.rom
KERNEL_EXTRA_OPTS='quiet loglevel=3 vt.global_cursor_default=0'

for file in "$APP" "$FIRMWARE" /opt/catvm/libraylib.so /opt/catvm/start-catvm.sh /opt/catvm/xsession; do
    [ -f "$file" ] || { echo "configure.sh: $file is missing from the image" >&2; exit 1; }
done

# ---------------------------------------------------------------------------
echo 'Installing the machine'

chown -R root:root /opt/catvm
chmod 0755 "$APP" /opt/catvm/start-catvm.sh /opt/catvm/xsession
chmod 0644 "$FIRMWARE" /opt/catvm/libraylib.so

install -d -o root -g root -m 0755 /etc/catvm
install -d -o root -g root -m 0750 /var/lib/catvm
install -d -o root -g root -m 0750 /var/log/catvm

# ---------------------------------------------------------------------------
echo 'Writing the service configuration'

# Everything in command_args is passed straight through to the VM binary. Run
# `/opt/catvm/vm --help` on the machine for the full list.
default_args=''

if [ -n "$MEMORY" ]; then
    default_args="--memory $MEMORY"
fi

if [ -n "$OPS" ]; then
    # Naming a cycle rate paces the CPU to it; without one the machine runs flat out.
    default_args="${default_args:+$default_args }--ops $OPS"
fi

if [ -n "$default_args" ]; then
    echo "  machine arguments: $default_args"
fi

cat >/etc/conf.d/catvm <<EOF
# Arguments for the Cat machine. See /opt/catvm/vm --help.
#
# By default the CPU runs flat out, every disk the host is not using is attached,
# and the guest shutting down powers the machine off.
command_args="$default_args"
EOF

cat >>/etc/conf.d/catvm <<'EOF'

# Examples:
#
# Pace the CPU to 50 MHz instead of letting it run flat out, and give it more RAM.
# While the CPU is uncapped the cycle rate is ignored altogether, so this is only
# worth setting for a guest that wants a specific cycle budget:
#   command_args="--memory 64M --ops 50M"
#
# Only ever use one specific disk, named so it cannot move between boots:
#   command_args="--no-auto-disks --disk /dev/disk/by-id/ata-SomeDisk_1234"
#
# Keep a disk to yourself:
#   command_args="--exclude-disk /dev/sdb"
#
# Show the machine in a window with an FPS counter instead of taking the screen:
#   command_args="--no-fullscreen --fps"

# Which virtual terminal and X display the machine takes over.
CATVM_VT="vt1"
CATVM_DISPLAY=":0"
EOF

chmod 0644 /etc/conf.d/catvm

cat >/etc/init.d/catvm <<'EOF'
#!/sbin/openrc-run

name="catvm"
description="Cat machine"

# start-catvm.sh brings X up on its own VT and execs the machine inside it.
# command_args comes from /etc/conf.d/catvm and is passed on to the machine.
command="/opt/catvm/start-catvm.sh"
command_user="root:root"
directory="/var/lib/catvm"

supervisor="supervise-daemon"
output_log="/var/log/catvm/catvm.log"
error_log="/var/log/catvm/catvm.log"

# The machine is the point of this system, so bring it back if it dies.
respawn_delay=2
respawn_max=0

# Give the guest's queued disk writes time to reach the disks.
stop_timeout=30

depend() {
    need localmount

    # Device nodes and input devices have to exist before X starts. These are
    # ordering rules rather than requirements, so a system using mdev still boots.
    after devfs udev udev-trigger udev-settle hwdrivers modules

    use net
}

start_pre() {
    if [ ! -x "$command" ]; then
        eerror "$command is missing or not executable"
        return 1
    fi

    if [ ! -e /dev/tty1 ]; then
        ewarn "/dev/tty1 does not exist, X may not be able to take a console"
    fi

    if [ ! -d /dev/input ]; then
        ewarn "/dev/input does not exist, there may be no keyboard or mouse"
    fi

    export CATVM_VT CATVM_DISPLAY

    return 0
}
EOF

chmod 0755 /etc/init.d/catvm
rc-update add catvm default

# ---------------------------------------------------------------------------
echo 'Switching device management to udev'

# X's libinput driver enumerates devices through libudev, which mdev cannot
# provide. This is what setup-devd(8) does on a normal Alpine install.
rc-update del mdev sysinit 2>/dev/null || true
rc-update add udev sysinit
rc-update add udev-trigger sysinit
rc-update add udev-settle sysinit
rc-update add udev-postmount default 2>/dev/null || true

# ---------------------------------------------------------------------------
echo 'Freeing tty1 for the display'

# X takes vt1, so nothing else may own it. tty2 onwards keep their logins, so
# there is still a way into the system.
sed -Ei 's|^(tty1::respawn.*)$|# \1  (taken by the Cat machine, see /etc/init.d/catvm)|' /etc/inittab

# ---------------------------------------------------------------------------
echo 'Configuring the bootloader'

kernel=$(ls /boot/vmlinuz-* 2>/dev/null | head -1 | xargs -r basename)
initramfs=$(ls /boot/initramfs-* 2>/dev/null | head -1 | xargs -r basename)

if [ "$BOOT_MODE" = 'BIOS' ]; then
    current=$(sed -nE 's|^[# ]*default_kernel_opts="?([^"]*)"?.*|\1|p' /etc/update-extlinux.conf | head -1)
    sed -Ei "s|^[# ]*(default_kernel_opts)=.*|\1=\"$current $KERNEL_EXTRA_OPTS\"|" \
        /etc/update-extlinux.conf

    # Not fatal: the outer script already installed a working extlinux.conf, and
    # this only adds the quiet boot options to it.
    update-extlinux --warn-only 2>&1 \
        | { grep -Fv 'extlinux: cannot open device /dev' || :; } || true
else
    [ -n "$kernel" ] || { echo 'configure.sh: no kernel in /boot' >&2; exit 1; }

    # startup.nsh already holds a working command line, so reuse it rather than
    # trying to rebuild one.
    if [ -f /boot/startup.nsh ]; then
        cmdline=$(sed -E -e 's|^[^ ]+ ||' -e 's|initrd=[^ ]+ ?||' /boot/startup.nsh | tr -d '\n')
        printf '%s initrd=%s %s %s\n' "$kernel" "$initramfs" "$cmdline" "$KERNEL_EXTRA_OPTS" \
            > /boot/startup.nsh
    else
        root_spec=$(awk '$2 == "/" { print $1 }' /etc/fstab | head -1)
        cmdline="root=$root_spec rootfstype=ext4 console=tty0"
    fi

    # Real UEFI firmware will not run startup.nsh, so install GRUB the way
    # removable media does it (EFI/BOOT/BOOTX64.EFI), which needs no NVRAM entry.
    grub-install --target=x86_64-efi --efi-directory=/boot --boot-directory=/boot \
        --removable --no-nvram

    mkdir -p /boot/grub
    cat >/boot/grub/grub.cfg <<EOF
set default=0
set timeout=1

menuentry "CatVM" {
    search --no-floppy --label EFI --set=root
    linux /$kernel $cmdline $KERNEL_EXTRA_OPTS
    initrd /$initramfs
}
EOF
fi

# ---------------------------------------------------------------------------
echo 'Final touches'

cat >/etc/motd <<'EOF'
This machine runs CatVM on the hardware.

  rc-service catvm stop      stop the machine and get the console back
  rc-service catvm start     start it again
  /opt/catvm/vm --help       what the machine can be told to do
  /etc/conf.d/catvm          how it is configured here
  /var/log/catvm/catvm.log   what it and X have been saying

EOF

echo 'catvm' > /etc/hostname

# Nothing here needs a package cache at runtime.
rm -rf /var/cache/apk/*

echo 'CatVM configuration complete'
