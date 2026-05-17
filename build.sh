#!/usr/bin/env bash
set -euo pipefail

# Cake build script runner for CoffeeIRC

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CAKE_VERSION="4.0.0"

echo "=========================================="
echo "CoffeeIRC Cake Build Script"
echo "=========================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet SDK is not installed."
    echo "Please install .NET SDK 10.0 or later from https://dotnet.microsoft.com/download"
    exit 1
fi

echo "Found dotnet: $(dotnet --version)"
echo ""

# Install Cake tool if not already installed
if ! command -v dotnet-cake &> /dev/null; then
    echo "Installing Cake.Tool..."
    dotnet tool install -g Cake.Tool --version $CAKE_VERSION
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Run Cake with all arguments passed to this script
echo "Running Cake build..."
echo ""
dotnet cake "$@"
