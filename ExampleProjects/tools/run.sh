#!/bin/bash

MAIN_FILE=$1
echo $MAIN_FILE

echo "Running..."
echo dotnet run --project ../../CatLauncher/CatLauncher.csproj -- run --rom "./bin/"$*
dotnet run --project ../../CatLauncher/CatLauncher.csproj -- run --rom "./bin/"$*
status=$?
echo "Application exited with status code $status"
exit $status
