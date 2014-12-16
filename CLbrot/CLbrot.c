#include <stdio.h>
#include <stdint.h>
#include <math.h>
#include <time.h>
#include "simpleCL.h"
#include "lodepng.h"

#define GRID_SIZE 10000
#define MAX_ITER 20
#define MIN_ITER 0
#define GRID_RANGE 2.0
#define MAX_ORBIT_DIST 4.0

#define SUPERSAMPLE_SIZE 2

#define bucket_t uint32_t
#define fraction_t float

void writeImage(int width, int height, uint16_t* buffer) {
	unsigned char* png;
	size_t pngsize;
	LodePNGState state;
	lodepng_state_init(&state);

	state.info_raw.bitdepth = 16;
	state.info_raw.colortype = LCT_GREY;

	state.encoder.zlibsettings.use_lz77 = 1;
	state.encoder.zlibsettings.btype = 2;
	state.encoder.zlibsettings.minmatch = 3;
	state.encoder.zlibsettings.nicematch = 258;


	int error = lodepng_encode(&png, &pngsize, buffer, width, height, &state);
	if (!error)
		lodepng_save_file(png, pngsize, "out.png");
	else
		fprintf(stderr, "PNG encoding failed with code %u: %s\n", error, lodepng_error_text(error));

	lodepng_state_cleanup(&state);
	free(png);
}

uint16_t* normalizeGrid(bucket_t* grid) {
	int i, j, x, y;
	bucket_t max = 0;
	// find the largest number of hits in a single position
	for (i = 0; i < GRID_SIZE; i++) {
		for (j = 0; j < GRID_SIZE; j++) {
			bucket_t temp = grid[j + GRID_SIZE * i];
			if (temp > max)
				max = temp;
		}
	}
	printf("max is %d\n", max);
	// then normalize it to that maximum
	uint16_t* outGrid = (uint16_t*)malloc(sizeof(uint16_t) * GRID_SIZE * GRID_SIZE);

	for (i = 0; i < GRID_SIZE; i++) {
		for (j = 0; j < GRID_SIZE; j++) {
			uint16_t val = ((double)grid[j + GRID_SIZE * i] / max) * 0xFFFF;	// the maximum value of a uint16
			outGrid[j + GRID_SIZE * i] = val;
		}
	}
	return outGrid;
}

void main() {
	sclHard hardware;
	sclSoft software;

	size_t global_size[2];
	size_t local_size[2];
	size_t dataLength = pow(GRID_SIZE, 2);
	size_t dataSize = sizeof(bucket_t) * dataLength;

	// we can only send a 1D array to the gpu
	bucket_t* grid = malloc(dataSize);
	memset(grid, 0, dataSize);

	int gridSize = GRID_SIZE;
	int maxIterations = MAX_ITER;
	int minIterations = MIN_ITER;
	int supersampling = SUPERSAMPLE_SIZE;
	fraction_t gridRange = GRID_RANGE;
	fraction_t maxOrbit = MAX_ORBIT_DIST;


	global_size[0] = dataLength;
	global_size[1] = 1;

	local_size[0] = 64;
	local_size[1] = 1;

	int found = 0;
	//sclHard* allHardware = sclGetHardware(&found);
	//hardware = sclGetFastestDevice(allHardware, found);
	int found2;
	hardware = sclGetGPUHardware(0, &found2);
	software = sclGetCLSoftware("buddhabrot.cl", "buddhabrot", hardware);
	clock_t start = clock();
	printf("The screen is about to freeze. Don't panic, this is supposed to happen. If you decide you want it to stop, hold down the power button or unplug your computer.\n");
	sclManageArgsLaunchKernel(hardware, software,
		global_size, local_size,
		"%R %a %a %a %a %a %a",
		dataSize, (void*)grid,				// grid
		sizeof(int), &gridSize,				// GRID_SIZE
		sizeof(int), &maxIterations,		// MAX_ITER
		sizeof(int), &minIterations,		// MIN_ITER
		sizeof(int), &supersampling,		// SUPERSAMPLE_SIZE
		sizeof(fraction_t), &gridRange,		// GRID_RANGE
		sizeof(fraction_t), &maxOrbit);		// MAX_ORBIT_DIST
	clock_t calc = clock();
	printf("\nFinished calculations in %f seconds, beginning normalization\n", ((double)calc - (double)start) / CLOCKS_PER_SEC);
	uint16_t* normalized = normalizeGrid(grid);
	free(grid);
	clock_t norm = clock();
	printf("Finished normalization in %f seconds, beginning write\n", ((double)norm - (double)calc) / CLOCKS_PER_SEC);
	writeImage(GRID_SIZE, GRID_SIZE, normalized);
	clock_t done = clock();
	printf("Finished all operations in %f seconds.\n", ((double)done - (double)start) / CLOCKS_PER_SEC);
	// free grids here
	free(normalized);
}