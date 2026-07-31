#!/bin/sh
# Brings up the display server and hands it to the machine.
#
# This is what the catvm service runs. Everything passed to it goes through to
# the VM binary, so /etc/conf.d/catvm is the only place that needs editing to
# change how the machine is configured.
#
# X is used rather than talking to DRM directly because the PPU's tiled display
# mode needs desktop OpenGL 3.3, which the DRM/GLES path cannot give it.
set -eu

VT=${CATVM_VT:-vt1}
DISPLAY_NUM=${CATVM_DISPLAY:-:0}

exec /usr/bin/xinit /opt/catvm/xsession "$@" -- "$DISPLAY_NUM" "$VT" -nolisten tcp
