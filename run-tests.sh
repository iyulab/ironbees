#!/bin/bash
# Ironbees Test Runner Script
# 테스트 카테고리별 실행 스크립트

set -e

# Default values
CATEGORY="all"
COVERAGE=false

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
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--category all|unit|performance|integration|ci] [--coverage]"
            exit 1
            ;;
    esac
done

echo "🐝 Ironbees Test Runner"
echo "Category: $CATEGORY"
echo ""

# Base test command
TEST_CMD="dotnet test --configuration Debug --verbosity normal"

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
        echo "▶️  Running CI tests (excluding Performance)"
        TEST_CMD="$TEST_CMD --filter \"Category!=Performance\""
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
