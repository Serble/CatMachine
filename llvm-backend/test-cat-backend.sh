#!/bin/bash
# test-cat-backend.sh - Comprehensive test suite for Cat LLVM backend
# Tests the Cat backend with various C programs and validates output

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$SCRIPT_DIR/test-output"
EXAMPLES_DIR="$SCRIPT_DIR/examples"

# Test counters
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

# Print functions
print_test() {
    echo -e "${BLUE}[TEST]${NC} $1"
}

print_pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
    ((TESTS_PASSED++))
}

print_fail() {
    echo -e "${RED}[FAIL]${NC} $1"
    ((TESTS_FAILED++))
}

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_section() {
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}$1${NC}"
    echo -e "${GREEN}========================================${NC}"
}

# Check if LLVM tools are available
check_tools() {
    print_section "Checking Tools"
    
    local missing_tools=()
    
    if ! command -v clang &> /dev/null; then
        missing_tools+=(clang)
    else
        print_pass "clang found: $(which clang)"
    fi
    
    if ! command -v llc &> /dev/null; then
        missing_tools+=(llc)
    else
        print_pass "llc found: $(which llc)"
    fi
    
    if [ ${#missing_tools[@]} -ne 0 ]; then
        print_fail "Missing tools: ${missing_tools[*]}"
        echo ""
        echo "Please ensure LLVM is installed and in your PATH"
        echo "If you just built LLVM, run:"
        echo "  source ~/llvm-cat/setup-env.sh"
        exit 1
    fi
    
    # Check if Cat target is available
    if llc --version | grep -q "cat"; then
        print_pass "Cat target is registered"
    else
        print_fail "Cat target not found in llc"
        echo ""
        echo "The Cat backend may not be properly installed"
        exit 1
    fi
}

# Create test directory
setup_test_dir() {
    print_section "Setting Up Test Environment"
    
    rm -rf "$TEST_DIR"
    mkdir -p "$TEST_DIR"
    
    print_info "Test directory: $TEST_DIR"
}

# Run a single test
run_test() {
    local test_name="$1"
    local c_file="$2"
    local expected_pattern="$3"  # Optional: pattern to look for in assembly
    
    ((TESTS_RUN++))
    print_test "Test $TESTS_RUN: $test_name"
    
    local base_name=$(basename "$c_file" .c)
    local ll_file="$TEST_DIR/${base_name}.ll"
    local asm_file="$TEST_DIR/${base_name}.s"
    
    # Step 1: Compile C to LLVM IR
    print_info "  Step 1: Compiling C to LLVM IR..."
    if clang -S -emit-llvm -O2 "$c_file" -o "$ll_file" 2>&1 | tee "$TEST_DIR/${base_name}.clang.log"; then
        print_info "    ✓ LLVM IR generated"
    else
        print_fail "$test_name - Failed to generate LLVM IR"
        return 1
    fi
    
    # Step 2: Compile LLVM IR to Cat assembly
    print_info "  Step 2: Compiling LLVM IR to Cat assembly..."
    if llc -march=cat "$ll_file" -o "$asm_file" 2>&1 | tee "$TEST_DIR/${base_name}.llc.log"; then
        print_info "    ✓ Cat assembly generated"
    else
        print_fail "$test_name - Failed to generate Cat assembly"
        cat "$TEST_DIR/${base_name}.llc.log"
        return 1
    fi
    
    # Step 3: Validate output
    print_info "  Step 3: Validating output..."
    
    if [ ! -s "$asm_file" ]; then
        print_fail "$test_name - Assembly file is empty"
        return 1
    fi
    
    # Check for expected pattern if provided
    if [ -n "$expected_pattern" ]; then
        if grep -q "$expected_pattern" "$asm_file"; then
            print_info "    ✓ Found expected pattern: $expected_pattern"
        else
            print_fail "$test_name - Expected pattern not found: $expected_pattern"
            return 1
        fi
    fi
    
    # Check for basic structure
    local checks_passed=true
    
    if ! grep -q "RET" "$asm_file"; then
        print_info "    ✗ Warning: No RET instruction found"
        checks_passed=false
    fi
    
    if ! grep -q "MOV" "$asm_file"; then
        print_info "    ✗ Warning: No MOV instruction found"
        checks_passed=false
    fi
    
    if $checks_passed; then
        print_info "    ✓ Assembly structure looks valid"
    fi
    
    # Show assembly stats
    local line_count=$(wc -l < "$asm_file")
    local inst_count=$(grep -E "^\s+(MOV|ADD|SUB|MUL|PUSH|POP|JMP|CALL|RET)" "$asm_file" | wc -l)
    print_info "    Assembly: $line_count lines, ~$inst_count instructions"
    
    print_pass "$test_name"
    return 0
}

# Test with different optimization levels
test_optimization_levels() {
    print_section "Testing Optimization Levels"
    
    local test_file="$EXAMPLES_DIR/simple.c"
    
    for opt in "-O0" "-O1" "-O2" "-O3" "-Os"; do
        ((TESTS_RUN++))
        print_test "Test $TESTS_RUN: Optimization level $opt"
        
        local ll_file="$TEST_DIR/simple_${opt}.ll"
        local asm_file="$TEST_DIR/simple_${opt}.s"
        
        if clang -S -emit-llvm $opt "$test_file" -o "$ll_file" 2>/dev/null; then
            if llc -march=cat "$ll_file" -o "$asm_file" 2>/dev/null; then
                local size=$(wc -l < "$asm_file")
                print_pass "Optimization $opt - Generated $size lines"
            else
                print_fail "Optimization $opt - LLC failed"
            fi
        else
            print_fail "Optimization $opt - Clang failed"
        fi
    done
}

# Create test cases
create_test_cases() {
    print_section "Creating Additional Test Cases"
    
    # Test 1: Empty main
    cat > "$TEST_DIR/test_empty.c" << 'EOF'
int main() {
    return 0;
}
EOF
    
    # Test 2: Arithmetic
    cat > "$TEST_DIR/test_arithmetic.c" << 'EOF'
int test_add(int a, int b) {
    return a + b;
}

int test_sub(int a, int b) {
    return a - b;
}

int test_mul(int a, int b) {
    return a * b;
}

int main() {
    int x = 10;
    int y = 5;
    return test_add(x, y) + test_sub(x, y) + test_mul(x, y);
}
EOF
    
    # Test 3: Conditionals
    cat > "$TEST_DIR/test_conditionals.c" << 'EOF'
int max(int a, int b) {
    if (a > b) {
        return a;
    } else {
        return b;
    }
}

int main() {
    return max(10, 20);
}
EOF
    
    # Test 4: Loops
    cat > "$TEST_DIR/test_loops.c" << 'EOF'
int sum(int n) {
    int total = 0;
    int i = 0;
    while (i < n) {
        total = total + i;
        i = i + 1;
    }
    return total;
}

int main() {
    return sum(10);
}
EOF
    
    # Test 5: Multiple arguments
    cat > "$TEST_DIR/test_args.c" << 'EOF'
int add3(int a, int b, int c) {
    return a + b + c;
}

int add4(int a, int b, int c, int d) {
    return a + b + c + d;
}

int main() {
    return add3(1, 2, 3) + add4(1, 2, 3, 4);
}
EOF
    
    # Test 6: Pointer basics
    cat > "$TEST_DIR/test_pointers.c" << 'EOF'
int deref(int *p) {
    return *p;
}

void set(int *p, int val) {
    *p = val;
}

int main() {
    int x = 42;
    int *p = &x;
    return deref(p);
}
EOF
    
    print_info "Created 6 additional test cases"
}

# Run all tests
run_all_tests() {
    # Test existing examples
    print_section "Testing Example Programs"
    
    if [ -f "$EXAMPLES_DIR/simple.c" ]; then
        run_test "Simple arithmetic" "$EXAMPLES_DIR/simple.c" "add:"
    fi
    
    if [ -f "$EXAMPLES_DIR/fibonacci.c" ]; then
        run_test "Fibonacci recursion" "$EXAMPLES_DIR/fibonacci.c" "fibonacci:"
    fi
    
    if [ -f "$EXAMPLES_DIR/loops.c" ]; then
        run_test "Loops and iteration" "$EXAMPLES_DIR/loops.c" "sum_to_n:"
    fi
    
    # Test generated cases
    print_section "Testing Generated Test Cases"
    
    run_test "Empty main" "$TEST_DIR/test_empty.c" "main:"
    run_test "Arithmetic operations" "$TEST_DIR/test_arithmetic.c" "test_add:"
    run_test "Conditional branches" "$TEST_DIR/test_conditionals.c" "CMP"
    run_test "While loops" "$TEST_DIR/test_loops.c" "sum:"
    run_test "Multiple arguments" "$TEST_DIR/test_args.c" "add3:"
    run_test "Pointer operations" "$TEST_DIR/test_pointers.c" "deref:"
}

# Generate detailed report
generate_report() {
    print_section "Test Report"
    
    echo ""
    echo "╔════════════════════════════════════════════╗"
    echo "║  Cat Backend Test Results                  ║"
    echo "╚════════════════════════════════════════════╝"
    echo ""
    echo "Total tests run:    $TESTS_RUN"
    echo "Tests passed:       $TESTS_PASSED"
    echo "Tests failed:       $TESTS_FAILED"
    echo ""
    
    if [ $TESTS_FAILED -eq 0 ]; then
        print_pass "All tests passed! ✓"
        echo ""
        echo "The Cat backend is working correctly."
        return 0
    else
        print_fail "Some tests failed"
        echo ""
        echo "Check the logs in: $TEST_DIR"
        return 1
    fi
}

# Show sample output
show_sample_output() {
    print_section "Sample Output"
    
    local sample_file="$TEST_DIR/simple.s"
    if [ -f "$sample_file" ]; then
        echo ""
        echo "First 30 lines of generated Cat assembly (simple.c):"
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        head -30 "$sample_file" | cat -n
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        echo ""
        echo "Full output available at: $sample_file"
    fi
}

# Main test flow
main() {
    echo ""
    echo "╔════════════════════════════════════════════╗"
    echo "║  Cat LLVM Backend Test Suite               ║"
    echo "║  Version: 1.0                              ║"
    echo "╚════════════════════════════════════════════╝"
    echo ""
    
    check_tools
    setup_test_dir
    create_test_cases
    run_all_tests
    test_optimization_levels
    generate_report
    show_sample_output
    
    echo ""
    echo "Test artifacts saved in: $TEST_DIR"
    echo ""
    
    # Return exit code based on test results
    if [ $TESTS_FAILED -eq 0 ]; then
        exit 0
    else
        exit 1
    fi
}

# Handle script arguments
if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo "Usage: $0"
    echo ""
    echo "Run comprehensive tests for the Cat LLVM backend"
    echo ""
    echo "Prerequisites:"
    echo "  - LLVM with Cat backend must be installed"
    echo "  - clang and llc must be in PATH"
    echo ""
    echo "The script will:"
    echo "  1. Test existing example programs"
    echo "  2. Generate and test additional test cases"
    echo "  3. Test different optimization levels"
    echo "  4. Generate a detailed report"
    echo ""
    exit 0
fi

# Run main
main
