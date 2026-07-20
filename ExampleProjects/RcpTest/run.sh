#!/bin/sh

ROM_PATH="./bin/rcptest.bin"
ARGUMENTS="--dump-errors --test-ints -d RealityCoProcessor"

# Navigate to the script's directory
cd "$(dirname "$0")"

./build.sh || exit $?

if [[ -z "${CAT_LAUNCHER_COMMAND}" ]]; then
  CAT_LAUNCHER_COMMAND=catlaunch
fi

echo "Running..."
$CAT_LAUNCHER_COMMAND run --rom "$ROM_PATH" $ARGUMENTS $*
status=$?
echo "Application exited with status code $status"
exit $status
