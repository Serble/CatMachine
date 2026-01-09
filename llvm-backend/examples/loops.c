// Loop example for Cat VM
// Demonstrates while loops and accumulation

int sum_to_n(int n) {
    int sum = 0;
    int i = 1;
    
    while (i <= n) {
        sum = sum + i;
        i = i + 1;
    }
    
    return sum;
}

int factorial(int n) {
    int result = 1;
    int i = 2;
    
    while (i <= n) {
        result = result * i;
        i = i + 1;
    }
    
    return result;
}

int main() {
    int sum = sum_to_n(100);     // 5050
    int fact = factorial(5);      // 120
    
    return sum + fact;
}
