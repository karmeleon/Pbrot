#include <stdio.h>
#include <string.h>

extern __declspec(dllexport) void GetString(char* str) {
	static char* sTest = "String1";
	strcpy(str, sTest);
}