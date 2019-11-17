libexample.so: example.c
	gcc -shared -o $@ $<
