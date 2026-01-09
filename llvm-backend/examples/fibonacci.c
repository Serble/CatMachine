// Fibonacci example for Cat VM
// Demonstrates recursion and more complex control flow

int fibonacci(int n) {
    if (n <= 1) {
        return n;
    }
    return fibonacci(n - 1) + fibonacci(n - 2);
}

int main() {
    int result = fibonacci(10);
    return result;  // Should return 55
}
