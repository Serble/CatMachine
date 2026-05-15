#!/bin/sh

# Navigate to the script's directory
cd "$(dirname "$0")"

./build.sh || exit $?

echo "Running..."
dotnet run --project ../../CatLauncher/CatLauncher.csproj run --rom ./bin/device_test.bin --test-ints $*
status=$?
echo "Application exited with status code $status"
