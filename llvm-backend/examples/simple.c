// Simple arithmetic example for Cat VM
// This demonstrates basic function calls and arithmetic operations

int add(int a, int b) {
    return a + b;
}

int subtract(int a, int b) {
    return a - b;
}

int multiply(int a, int b) {
    return a * b;
}

int main() {
    int x = 10;
    int y = 5;
    
    int sum = add(x, y);        // 15
    int diff = subtract(x, y);  // 5
    int prod = multiply(x, y);  // 50
    
    // Simple conditional
    if (sum > 10) {
        return 0; // success
    }
    
    return 1; // failure
}
