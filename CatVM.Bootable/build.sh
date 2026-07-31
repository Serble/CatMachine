#!/bin/sh
# Builds the bootable CatVM image.
#
# The machine boots one thing: the firmware ROM you give it. FIRMWARE says what
# that is, and it is the only required setting:
#
#   FIRMWARE=../../Cat/CatOS/bin/catos.bin ./build.sh          a built ROM
#   FIRMWARE=examples/console.cat ./build.sh                   assembled here
#   FIRMWARE=../ExampleProjects/PlatformerCatnip/main.nip ./build.sh   compiled here
#
# Everything that goes into the image is built in containers so that the host does
# not need a musl toolchain, a .NET SDK or raylib's build dependencies:
#
#   1. CatVM.Metal, published as a Native AOT binary for linux-musl-x64
#   2. the firmware ROM, if it needs assembling or compiling
#   3. raylib, built against musl for X11 + desktop OpenGL 3.3
#   4. alpine-make-vm-image, which needs root on the host, to lay the image down
#
# Only step 4 needs sudo. Set SKIP_IMAGE=yes to do everything except that.
#
# Environment:
#   FIRMWARE        ROM to boot, or a .cat/.nip source to build (required)
#   FIRMWARE_ROOT   directory made available when building a source, in case it
#                   includes files from outside its own directory (default: the
#                   directory the source is in)
#   OPS             CPU cycles per second, e.g. 50M. Setting this paces the CPU to
#                   that speed; leaving it unset lets the CPU run flat out, which is
#                   the machine's default and ignores the cycle rate altogether
#   MEMORY          RAM for the machine, e.g. 64M (default is the machine's 16M)
#   ALPINE_BRANCH   Alpine branch for the image and the raylib build (v3.21)
#   BOOT_MODE       BIOS or UEFI (BIOS)
#   IMAGE           image file, or a block device to write straight to (alpine.raw)
#   IMAGE_SIZE      size of the image file (2G)
#   KERNEL_FLAVOR   Alpine kernel flavour (lts)
#   EXTRA_PACKAGES  more apk packages, e.g. GPU firmware
#   RAYLIB_VERSION  raylib tag to build (5.5)
#   CONTAINER       container engine (podman)
#   SDK_IMAGE       .NET SDK image (mcr.microsoft.com/dotnet/sdk:10.0-alpine)
#   SKIP_APP        yes to reuse the binaries already in build/ (no)
#   SKIP_RAYLIB     yes/no/auto, auto reuses build/lib/libraylib.so if present
#   SKIP_IMAGE      yes to stop after staging (no)
#   ALLOW_BLOCK_DEVICE  yes to let IMAGE point at a real block device (no)
#   SUDO            command used to get root (sudo)
set -eu

cd "$(dirname "$0")"

: "${FIRMWARE:=}"
: "${FIRMWARE_ROOT:=}"
: "${OPS:=}"
: "${MEMORY:=}"
: "${ALPINE_BRANCH:=v3.21}"
: "${BOOT_MODE:=BIOS}"
: "${IMAGE:=alpine.raw}"
: "${IMAGE_SIZE:=2G}"
: "${KERNEL_FLAVOR:=lts}"
: "${EXTRA_PACKAGES:=}"
: "${RAYLIB_VERSION:=5.5}"
: "${CONTAINER:=podman}"
: "${SDK_IMAGE:=mcr.microsoft.com/dotnet/sdk:10.0-alpine}"
: "${SKIP_APP:=no}"
: "${SKIP_RAYLIB:=auto}"
: "${SKIP_IMAGE:=no}"
: "${ALLOW_BLOCK_DEVICE:=no}"
: "${SUDO:=sudo}"

REPO_ROOT=$(cd .. && pwd)
BUILD=build
SKEL=$BUILD/skel
ALPINE_IMAGE="alpine:${ALPINE_BRANCH#v}"

# Runtime dependencies of the machine. Deliberately short: X with no window
# manager, mesa for OpenGL 3.3 (llvmpipe when there is no GPU driver), eudev so
# libinput can enumerate input devices, and the two libraries a Native AOT binary
# links against.
PACKAGES="alpine-base
eudev udev-init-scripts
xorg-server xinit xset
xf86-input-libinput xf86-video-vesa xf86-video-fbdev
mesa-dri-gallium mesa-gl mesa-egl
libx11 libxext libxrender libxrandr libxinerama libxcursor libxi
libgcc libstdc++"

if [ "$BOOT_MODE" = 'UEFI' ]; then
    PACKAGES="$PACKAGES
grub grub-efi"
fi

if [ -n "$EXTRA_PACKAGES" ]; then
    PACKAGES="$PACKAGES
$EXTRA_PACKAGES"
fi

step() {
    printf '\n\033[1;36m==> %s\033[0m\n' "$1"
}

die() {
    printf '\033[1;31mbuild.sh: %s\033[0m\n' "$1" >&2
    exit 1
}

case "$BOOT_MODE" in
    BIOS | UEFI) ;;
    *) die "BOOT_MODE must be BIOS or UEFI, got: $BOOT_MODE" ;;
esac

command -v "$CONTAINER" >/dev/null || die "$CONTAINER is not installed"

mkdir -p "$BUILD/lib"

# ---------------------------------------------------------------------------
# What is this machine going to boot?
#
# The firmware is whatever ROM the operator supplies: an OS that goes looking for
# a disk to boot, a single game, a test program. The machine has no opinion, and
# no firmware of its own to fall back on.
if [ -z "$FIRMWARE" ]; then
    die "FIRMWARE is required: the ROM this machine boots.

  FIRMWARE=/path/to/your.rom ./build.sh              a ROM you have already built
  FIRMWARE=examples/console.cat ./build.sh           a program that is the whole machine
  FIRMWARE=examples/diskloader.cat ./build.sh        a loader that boots off a disk

A .cat or .nip source is built for you; anything else is taken as a finished ROM."
fi

[ -e "$FIRMWARE" ] || die "FIRMWARE does not exist: $FIRMWARE"

firmware_path=$(cd "$(dirname "$FIRMWARE")" && pwd)/$(basename "$FIRMWARE")

case "$FIRMWARE" in
    *.cat) firmware_kind=cat ;;
    *.nip) firmware_kind=nip ;;
    *)     firmware_kind=rom ;;
esac

if [ "$firmware_kind" = 'rom' ]; then
    echo "firmware: $firmware_path (prebuilt ROM)"
    install -D -m 0644 "$firmware_path" "$BUILD/firmware.rom"
else
    [ -n "$FIRMWARE_ROOT" ] || FIRMWARE_ROOT=$(dirname "$firmware_path")
    firmware_root=$(cd "$FIRMWARE_ROOT" && pwd)

    case "$firmware_path" in
        "$firmware_root"/*) ;;
        *) die "FIRMWARE ($firmware_path) is not inside FIRMWARE_ROOT ($firmware_root)" ;;
    esac

    firmware_rel=${firmware_path#"$firmware_root"/}
    echo "firmware: $firmware_rel ($firmware_kind source, built from $firmware_root)"
fi

# ---------------------------------------------------------------------------
step "Building CatVM.Metal ($SDK_IMAGE)"

if [ "$SKIP_APP" = 'yes' ]; then
    echo "SKIP_APP=yes, reusing $BUILD/publish"
    [ -x "$BUILD/publish/CatVM.Metal" ] || die "$BUILD/publish/CatVM.Metal is missing"

    if [ "$firmware_kind" != 'rom' ]; then
        echo "SKIP_APP=yes, so $BUILD/firmware.rom is reused rather than rebuilt from the source"
    fi
else
    # The container is given the firmware source read-only when it has to build
    # one, plus a stream of the repository. The source is streamed rather than
    # bind mounted so the container's build does not fight with the host's obj/
    # and bin/ directories.
    set -- run --rm -i \
        --security-opt label=disable \
        -v "$PWD/$BUILD":/out \
        -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        -e DOTNET_NOLOGO=1 \
        -e "FIRMWARE_KIND=$firmware_kind"

    if [ "$firmware_kind" != 'rom' ]; then
        set -- "$@" -v "$firmware_root":/firmware:ro -e "FIRMWARE_REL=$firmware_rel"
    fi

    set -- "$@" "$SDK_IMAGE" sh -eu -c '
        apk add --no-cache clang build-base zlib-dev >/dev/null
        mkdir -p /src && tar -x -C /src
        cd /src

        dotnet publish CatVM.Metal/CatVM.Metal.csproj \
            -c Release -r linux-musl-x64 -o /out/publish

        case "$FIRMWARE_KIND" in
            cat)
                dotnet run --project CatAssembler/CatAssembler.csproj -c Release -- \
                    "/firmware/$FIRMWARE_REL" -o /out/firmware.rom
                ;;
            nip)
                dotnet run --project Catnip.Compiler/Catnip.Compiler.csproj -c Release -- \
                    "/firmware/$FIRMWARE_REL" -o /out/firmware.rom \
                    -s /out/firmware.asm -d /out/firmware.asm.debug
                ;;
        esac
    '

    tar -c -C "$REPO_ROOT" \
        --exclude=bin \
        --exclude=obj \
        --exclude=./.git \
        --exclude=./Docs/dist \
        --exclude=./BenchmarkDotNet.Artifacts \
        --exclude=./CatVM.Bootable/build \
        --exclude='*.raw' \
        . | "$CONTAINER" "$@" || die 'the application build failed'
fi

[ -s "$BUILD/firmware.rom" ] || die 'there is no firmware ROM to put in the image'

# ---------------------------------------------------------------------------
step "Building raylib $RAYLIB_VERSION for musl ($ALPINE_IMAGE)"

if [ "$SKIP_RAYLIB" = 'yes' ] || { [ "$SKIP_RAYLIB" = 'auto' ] && [ -s "$BUILD/lib/libraylib.so" ]; }; then
    echo "reusing $BUILD/lib/libraylib.so (set SKIP_RAYLIB=no to rebuild)"
else
    # The Raylib-cs package only ships a glibc build, so the image gets one built
    # here instead. Desktop OpenGL 3.3 is required by the PPU's shaders, which
    # rules out raylib's DRM/GLES platform.
    "$CONTAINER" run --rm \
        --security-opt label=disable \
        -v "$PWD/$BUILD":/out \
        -e RAYLIB_VERSION="$RAYLIB_VERSION" \
        "$ALPINE_IMAGE" sh -eu -c '
            apk add --no-cache build-base cmake ca-certificates wget \
                mesa-dev libx11-dev libxrandr-dev libxinerama-dev libxcursor-dev \
                libxi-dev >/dev/null

            wget -q -O /tmp/raylib.tar.gz \
                "https://github.com/raysan5/raylib/archive/refs/tags/$RAYLIB_VERSION.tar.gz"
            tar -xzf /tmp/raylib.tar.gz -C /tmp

            cmake -S "/tmp/raylib-$RAYLIB_VERSION" -B /tmp/rbuild \
                -DCMAKE_BUILD_TYPE=Release \
                -DBUILD_SHARED_LIBS=ON \
                -DBUILD_EXAMPLES=OFF \
                -DPLATFORM=Desktop \
                -DGRAPHICS=GRAPHICS_API_OPENGL_33 \
                -DGLFW_BUILD_X11=ON \
                -DGLFW_BUILD_WAYLAND=OFF \
                -DCMAKE_INSTALL_PREFIX=/tmp/rinstall >/dev/null
            cmake --build /tmp/rbuild -j"$(nproc)" >/dev/null
            cmake --install /tmp/rbuild >/dev/null

            cp "$(readlink -f /tmp/rinstall/lib/libraylib.so)" /out/lib/libraylib.so
        ' || die 'the raylib build failed'
fi

[ -s "$BUILD/lib/libraylib.so" ] || die 'libraylib.so was not built'

# ---------------------------------------------------------------------------
step 'Staging the image contents'

rm -rf "$SKEL"
mkdir -p "$SKEL"
cp -a overlay/. "$SKEL"/

install -D -m 0755 "$BUILD/publish/CatVM.Metal" "$SKEL/opt/catvm/vm"
install -D -m 0644 "$BUILD/firmware.rom" "$SKEL/opt/catvm/firmware.rom"
# Kept next to the binary: .NET's P/Invoke resolution looks there first, so the
# machine cannot pick up some other raylib that happens to be installed.
install -D -m 0644 "$BUILD/lib/libraylib.so" "$SKEL/opt/catvm/libraylib.so"
chmod 0755 "$SKEL/opt/catvm/start-catvm.sh" "$SKEL/opt/catvm/xsession"

find "$SKEL" -type f -exec ls -l {} + | awk '{ printf "  %-52s %s bytes\n", $NF, $5 }'

if [ "$SKIP_IMAGE" = 'yes' ]; then
    step 'SKIP_IMAGE=yes, stopping before the image is built'
    exit 0
fi

# ---------------------------------------------------------------------------
step "Building the $BOOT_MODE image: $IMAGE"

if [ -b "$IMAGE" ]; then
    [ "$ALLOW_BLOCK_DEVICE" = 'yes' ] || die \
        "$IMAGE is a block device. Everything on it would be destroyed; pass ALLOW_BLOCK_DEVICE=yes if that is really what you want."
    echo "writing directly to the block device $IMAGE"
elif [ -e "$IMAGE" ]; then
    # alpine-make-vm-image reuses an existing file, which would keep its old size
    # and any leftovers in it.
    echo "removing the previous $IMAGE"
    rm -f "$IMAGE"
fi

for tool in qemu-img qemu-nbd rsync sfdisk; do
    command -v "$tool" >/dev/null || die "$tool is required on the host (see README.md)"
done

if [ "$BOOT_MODE" = 'UEFI' ]; then
    command -v mkfs.vfat >/dev/null || die 'mkfs.vfat (dosfstools) is required for UEFI images'
fi

$SUDO ./alpine-make-vm-image \
    --branch "$ALPINE_BRANCH" \
    --arch x86_64 \
    --boot-mode "$BOOT_MODE" \
    --image-format raw \
    --image-size "$IMAGE_SIZE" \
    --kernel-flavor "$KERNEL_FLAVOR" \
    --initfs-features 'ata ide scsi usb virtio nvme kms mmc' \
    --packages "$PACKAGES" \
    --fs-skel-dir "$SKEL" \
    --fs-skel-chown root:root \
    --script-chroot \
    "$IMAGE" -- ./configure.sh "$BOOT_MODE" "$OPS" "$MEMORY"

step "Done: $IMAGE"
echo 'Try it with ./qemu-run.sh (pass a boot disk so there is something to boot).'
