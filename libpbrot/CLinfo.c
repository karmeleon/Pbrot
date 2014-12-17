#include "simpleCL.h"
#include <objbase.h>

extern __declspec(dllexport) char** GetCLDeviceStrings(int* count) {
	sclHard* allHardware = sclGetAllHardware(count);
	// assuming no one has more than 16 OCL devices on their system
	static char* strings[16];
	int i;
	for (i = 0; i < *count; i++) {
		strings[i] = CoTaskMemAlloc(sizeof(char) * 1024);
		clGetDeviceInfo(allHardware[i].device, CL_DEVICE_NAME, sizeof(char) * 1024, strings[i], NULL);
	}
	return strings;
}

extern __declspec(dllexport) char** GetCLPlatformStrings(int* count) {
	sclHard* allHardware = sclGetAllHardware(count);
	// assuming no one has more than 16 OCL devices on their system
	static char* strings[16];
	int i;
	for (i = 0; i < *count; i++) {
		strings[i] = CoTaskMemAlloc(sizeof(char) * 1024);
		clGetPlatformInfo(allHardware[i].platform, CL_PLATFORM_NAME, sizeof(char) * 1024, strings[i], NULL);
	}
	return strings;
}

extern __declspec(dllexport) unsigned long int* GetCLMaxBufferSize(int* count) {
	int i;
	sclHard* allHardware = sclGetAllHardware(count);
	static unsigned long int out[16];
	for (i = 0; i < *count; i++) {
		out[i] = _sclGetMaxMemAllocSize(allHardware[i].device);
	}
	return out;
}