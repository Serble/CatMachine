int add(int a, int b) {
    return a + b;
}

int print(int s) {
    asm("INT 0x80");
    return 0;
}

int main() {
    int x = add(2, 3);
    if (x == 5) return 0;
    
    int b = 'A';
    
    int mystr = "Hello World!";
    print(mystr);

    while (1 == 1) {
        // inf
    }
    return 1;
}
