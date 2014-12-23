#include "PbrotCommon.h"

#define CLfraction_t float
#define CLbucket_t uint32_t

static CLbucket_t* sGrid;
static int sGridSize;

extern __declspec(dllexport) uint8_t* normalizeCLGrid() {
	uint64_t i;
	uint32_t temp, max = 0;
	// find the largest number of hits in a single position
	for (i = 0; i < sGridSize * sGridSize; i++) {
		temp = sGrid[i];
		if (temp > max)
			max = temp;
	}
	//printf("max is %d\n", max);
	// then normalize it to that maximum
	uint8_t* outGrid = (uint8_t*)CoTaskMemAlloc(sizeof(uint8_t) * sGridSize * sGridSize);

	for (i = 0; i < sGridSize * sGridSize; i++) {
		uint8_t val = ((double)sGrid[i] / max) * 0xFF;	// the maximum value of a uint8
		outGrid[i] = val;
	}
	free(sGrid);
	return outGrid;
}

// a copy of sclGetCLSoftware that takes a string as the kernel instead of a file path
sclSoft getCLSoftwareFromString(char* source, char* name, sclHard hardware) {
	sclSoft software;
	/* Load program source
	########################################################### */
	//char *source = _sclLoadProgramSource(path);
	/* ########################################################### */

	sprintf(software.kernelName, "%s", name);

	/* Create program objects from source
	########################################################### */
	software.program = _sclCreateProgram(source, hardware.context);
	/* ########################################################### */

	/* Build the program (compile it)
	############################################ */
	_sclBuildProgram(software.program, hardware.device, name);
	/* ############################################ */

	/* Create the kernel object
	########################################################################## */
	software.kernel = _sclCreateKernel(software);
	/* ########################################################################## */

	return software;

}

extern __declspec(dllexport) void RunCLbrot(char* kern, uint8_t deviceNo, uint32_t gridSize, uint32_t maxIterations,
												uint32_t minIterations, uint32_t supersampling, CLfraction_t gridRange,
												CLfraction_t maxOrbit) {
	sGridSize = gridSize;
	
	sclHard hardware;
	sclSoft software;

	size_t global_size[2];
	size_t local_size[2];
	size_t dataLength = gridSize * gridSize;
	size_t dataSize = sizeof(CLbucket_t) * dataLength;

	// we can only send a 1D array to the gpu
	CLbucket_t* grid = malloc(dataSize);
	memset(grid, 0, dataSize);

	global_size[0] = dataLength;
	global_size[1] = 1;

	local_size[0] = 64;
	local_size[1] = 1;

	int found = 0;
	hardware = sclGetAllHardware(&found)[deviceNo];//sclGetGPUHardware(0, &found2);
	software = getCLSoftwareFromString(kern, "buddhabrot", hardware);
	clock_t start = clock();
	//printf("The screen is about to freeze. Don't panic, this is supposed to happen. If you decide you want it to stop, hold down the power button or unplug your computer.\n");
	sclManageArgsLaunchKernel(hardware, software,
		global_size, local_size,
		"%R %a %a %a %a %a %a",
		dataSize, (void*)grid,					// grid
		sizeof(uint32_t), &gridSize,			// GRID_SIZE
		sizeof(uint32_t), &maxIterations,		// MAX_ITER
		sizeof(uint32_t), &minIterations,		// MIN_ITER
		sizeof(uint32_t), &supersampling,		// SUPERSAMPLE_SIZE
		sizeof(CLfraction_t), &gridRange,		// GRID_RANGE
		sizeof(CLfraction_t), &maxOrbit);		// MAX_ORBIT_DIST
	sGrid = grid;
	//uint8_t* normalized = normalizeCLGrid(grid, gridSize);
	
	//return normalized;
}