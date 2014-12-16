#include <stdlib.h>
#include <stdio.h>
#include <math.h>
#include <string.h>
#include <time.h>
#include <stdint.h>
#include <omp.h>
#include "lodepng.h"

#define GRID_SIZE 10000
#define SUPERSAMPLE_SIZE 5
#define MAX_ITER 200
#define MIN_ITER 0
#define GRID_RANGE 2.0
#define MAX_ORBIT_DIST 10.0
//#define OMP_THREADS 4

#define bucket_t uint16_t
#define fraction_t double

typedef struct {
	fraction_t a, b;
} complex;

bucket_t** createGrid(int gridSize) {
	bucket_t** grid = (bucket_t**)malloc(sizeof(bucket_t*) * GRID_SIZE);
	int i, j;
	for (i = 0; i < GRID_SIZE; i++) {
		grid[i] = (bucket_t*)malloc(sizeof(bucket_t) * GRID_SIZE);
	}

	// initialize grid to 0
	for (i = 0; i < GRID_SIZE; i++) {
		memset(grid[i], 0, GRID_SIZE * sizeof(bucket_t));
	}
	return grid;
}

uint16_t* normalizeGrid(bucket_t*** grid, int numThreads) {
	int i, j, k, x, y;
	uint32_t temp, max = 0;
	// find the largest number of hits in a single position
	for (i = 0; i < GRID_SIZE; i++) {
		for (j = 0; j < GRID_SIZE; j++) {
			temp = 0;
			for (k = 0; k < numThreads; k++) {
				temp += grid[k][i][j];
				//temp += grid[i][j];
			}
			if (temp > max)
				max = temp;
		}
	}

	printf("max is %d\n", max);
	// then normalize it to that maximum
	uint16_t* outGrid = (uint16_t*)malloc(sizeof(uint16_t) * GRID_SIZE * GRID_SIZE);
	//uint16_t* outGrid = (uint16_t*)malloc(sizeof(uint16_t) * (int)pow(GRID_SIZE / SUPERSAMPLE_SIZE, 2));

	for (i = 0; i < GRID_SIZE; i++) {
		for (j = 0; j < GRID_SIZE; j++) {
			uint16_t temp = 0;
			for (k = 0; k < numThreads; k++) {
				temp += grid[k][i][j];
				//temp += grid[i][j];
			}
			uint16_t val = ((double)temp / (double)max) * 0xfff;	// max value of uint16
			outGrid[j + GRID_SIZE * i] = val;
		}
	}

	return outGrid;
}

__inline void complexSquare(complex* n) {
	// FOIL, bitches!
	fraction_t tempA = n->a;
	//n->a = pow(n->a, 2) - pow(n->b, 2);
	n->a = (n->a * n->a) - (n->b * n->b);
	n->b = 2 * tempA * n->b;
}

// computes the manhattan distance from x to y. sqrt is crazy slow and we don't need precision here.
__inline fraction_t complexDistance(complex* x, complex* y) {
	return /*sqrt*/((x->a - y->a) * (x->a - y->a) + (x->b - y->b) * (x->b - y->b));
}

// adds y to x and stores the result in x
__inline void complexAdd(complex* x, complex* y) {
	x->a += y->a;
	x->b += y->b;
}

__inline fraction_t getImgCoord(fraction_t n) {
	fraction_t temp = n - GRID_SIZE / 2;
	return (temp / (fraction_t)(GRID_SIZE / 2)) * GRID_RANGE;
}

__inline int getGridCoord(fraction_t a) {
	a = (a + GRID_RANGE) / (GRID_RANGE);
	fraction_t scale = (GRID_SIZE - 1) / GRID_RANGE;
	a *= scale;
	return a;
}

//__inline fraction_t getRandomCoord(mt_state* rng) {
//	//return (drand48() - 0.5) * 2.0 * GRID_RANGE;
//	return (mts_drand(rng) - 0.5) * 2.0 * GRID_RANGE;
//}

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
	state.encoder.zlibsettings.nicematch = 64;


	uint32_t error = lodepng_encode(&png, &pngsize, buffer, width, height, &state);
	if (!error)
		lodepng_save_file(png, pngsize, "out.png");
	else
		fprintf(stderr, "PNG encoding failed with code %u: %s\n", error, lodepng_error_text(error));

	lodepng_state_cleanup(&state);
	free(png);
}

int main() {
	//omp_set_num_threads(1);
	printf("Calculating Buddhabrot with size %d and max iterations %d\n", GRID_SIZE, MAX_ITER);
	clock_t start = clock();
	// do math
	fraction_t stepSize = 1.0 / (double)SUPERSAMPLE_SIZE;
	bucket_t*** grid;
	int i, j, k, rows = 0, pRows = 0;
	int numThreads;
#pragma omp parallel private(i, j, k, pRows) shared(grid)
	{
#pragma omp master
		{
			numThreads = omp_get_num_threads();
			grid = malloc(sizeof(bucket_t**) * numThreads);
			for (k = 0; k < numThreads; k++) {
				grid[k] = createGrid(GRID_SIZE);
			}
		}
#pragma omp barrier
#pragma omp for schedule(dynamic,100)
		for (i = 0; i < GRID_SIZE * SUPERSAMPLE_SIZE; i++) {
			for (j = 0; j < GRID_SIZE * SUPERSAMPLE_SIZE; j++) {
				// create and initialize c
				complex c;
				c.a = getImgCoord((fraction_t)j * stepSize);
				c.b = getImgCoord((fraction_t)i * stepSize);
				// and z_0
				complex z;
				memcpy(&z, &c, sizeof(complex));
				// then do the actual iterations
				for (k = 0; k < MAX_ITER; k++) {
					// Z_n+1 = Z_n^2 + c
					complexSquare(&z);
					complexAdd(&z, &c);
					fraction_t cDist = complexDistance(&z, &c);
					if (cDist > MAX_ORBIT_DIST) {
						// c is NOT in the set, so start the iterations over, recording positions
						memcpy(&z, &c, sizeof(complex));
						for (k = 0; k < MAX_ITER; k++) {
							complexSquare(&z);
							complexAdd(&z, &c);
							cDist = complexDistance(&z, &c);
							if (cDist > MAX_ORBIT_DIST)	// this "particle" has escaped, stop calculating it
								break;
							int x = getGridCoord(z.a);
							if (x < 0 || x >= GRID_SIZE)	// this "particle" is outside the grid, but has not escaped yet
								continue;
							int y = getGridCoord(z.b);
							if (y < 0 || y >= GRID_SIZE)	// this "particle" is outside the grid, but has not escaped yet
								continue;
							if (k > MIN_ITER) {
								int thread = omp_get_thread_num();
								grid[thread][y][x]++;
							}
						}
						break;
					}
					/*
					int x = getGridCoord(z.a);
					if (x < 0 || x >= GRID_SIZE)
					continue;
					int y = getGridCoord(z.b);
					if (y < 0 || y >= GRID_SIZE)
					continue;
					if (k > MIN_ITERATIONS)
					grid[omp_get_thread_num()][y][x]++;*/

				}
			}
			pRows++;
			if (pRows % 10 == 0) {
#pragma omp atomic
				rows += 10;
				pRows = 0;
				if (rows % 100 == 0) {
					printf("Finished %d rows out of %d\n", rows, GRID_SIZE * SUPERSAMPLE_SIZE);
				}
			}
		}
	}
	clock_t calc = clock();
	printf("Finished calculations in %f seconds, beginning normalization\n", ((double)calc - (double)start) / CLOCKS_PER_SEC);
	uint16_t* normalized = normalizeGrid(grid, numThreads);
	//for (y = 0; y < GRID_SIZE; y++) {
	//	for (x = 0; x < numThreads; x++) {
	//		free(grid[x][y]);
	//	}
	//	//free(grid[x]);
	//}
	for (i = 0; i < numThreads; i++) {
		for (j = 0; j < GRID_SIZE; j++) {
			free(grid[i][j]);
		}
		free(grid[i]);
	}
	free(grid);
	clock_t norm = clock();
	printf("Finished normalization in %f seconds, beginning write\n", ((double)norm - (double)calc) / CLOCKS_PER_SEC);
	writeImage(GRID_SIZE, GRID_SIZE, normalized);
	clock_t done = clock();
	printf("Finished all operations in %f seconds.\n", ((double)done - (double)start) / CLOCKS_PER_SEC);
	// free grids here
	free(normalized);
}