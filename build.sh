echo "Starting build"
echo "Dir: $PWD"

MODE=$1

if [ -z "$MODE" ]; then
    MODE="Release"
fi

echo "Mode: $MODE"

msbuild src/*.sln /p:Configuration=$MODE
