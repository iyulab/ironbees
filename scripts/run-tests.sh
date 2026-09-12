#!/bin/bash
# Ironbees Test Runner Script
# 테스트 카테고리별 실행 스크립트

set -e

# Default values
CATEGORY="all"
COVERAGE=false
# Release by default so a local run measures the same binaries CI measures.
CONFIGURATION="Release"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --category)
            CATEGORY="$2"
            shift 2
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--category all|unit|performance|integration|ci] [--coverage] [--configuration Debug|Release]"
            exit 1
            ;;
    esac
done

echo "🐝 Ironbees Test Runner"
echo "Category: $CATEGORY"
echo ""

# Base test command
TEST_CMD="dotnet test --configuration $CONFIGURATION --verbosity normal"

# Add coverage if requested
if [ "$COVERAGE" = true ]; then
    echo "📊 Code coverage enabled"
    TEST_CMD="$TEST_CMD --collect:\"XPlat Code Coverage\""
fi

# Filter by category
case $CATEGORY in
    all)
        echo "▶️  Running ALL tests (including Performance tests)"
        # No filter - run everything
        ;;
    unit)
        echo "▶️  Running UNIT tests only"
        TEST_CMD="$TEST_CMD --filter \"Category!=Performance&Category!=Integration\""
        ;;
    performance)
        echo "▶️  Running PERFORMANCE tests only"
        TEST_CMD="$TEST_CMD --filter \"Category=Performance\""
        ;;
    integration)
        echo "▶️  Running INTEGRATION tests only"
        TEST_CMD="$TEST_CMD --filter \"Category=Integration\""
        ;;
    ci)
        # The same exclusion set publish.yml applies solution-wide (ci.yml applies it per project).
        # "ci" used to exclude only Performance, so a local "ci" green did not mean what a CI green means.
        echo "▶️  Running CI tests (excluding Performance, Integration, RequiresApiKey — as publish.yml does)"
        TEST_CMD="$TEST_CMD --filter \"Category!=Performance&Category!=Integration&Category!=RequiresApiKey\""
        ;;
    *)
        echo "Invalid category: $CATEGORY"
        echo "Valid categories: all, unit, performance, integration, ci"
        exit 1
        ;;
esac

echo ""
echo "Command: $TEST_CMD"
echo ""

# Execute tests
eval $TEST_CMD

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Tests completed successfully!"
else
    echo ""
    echo "❌ Tests failed"
    exit 1
fi
