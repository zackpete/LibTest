extern int add(int, int);
extern void add_one(int*);

int add(int a, int b) {
  return a + b;
}

void add_one(int* a) {
  *a = *a + 1;
}
