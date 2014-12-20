#include "PbrotCommon.h"

typedef struct {
	OMPfraction_t a, b;
} complex;

__inline void complexSquare(complex* n) {
	// FOIL, bitches!
	OMPfraction_t tempA = n->a;
	//n->a = pow(n->a, 2) - pow(n->b, 2);
	n->a = (n->a * n->a) - (n->b * n->b);
	n->b = 2 * tempA * n->b;
}

// computes the distance from x to y. sqrt is crazy slow and we don't need precision here.
__inline OMPfraction_t complexDistance(complex* x, complex* y) {
	return /*sqrt*/((x->a - y->a) * (x->a - y->a) + (x->b - y->b) * (x->b - y->b));
}

// adds y to x and stores the result in x
__inline void complexAdd(complex* x, complex* y) {
	x->a += y->a;
	x->b += y->b;
}

__inline OMPfraction_t getImgCoord(OMPfraction_t n, uint32_t gridSize, uint32_t gridRange) {
	OMPfraction_t temp = n - gridSize / 2;
	return (temp / (OMPfraction_t)(gridSize / 2)) * gridRange;
}

__inline int getGridCoord(complex* c, uint32_t gridRange, uint32_t gridSize) {
	/*a = (a + gridRange) / (gridRange);
	OMPfraction_t scale = (gridSize - 1) / gridRange;
	a *= scale;
	return a;*/

	// the denominator in fracs and scale may need to be gridRange * 2
	OMPfraction_t xFrac = (c->a + gridRange) / (gridRange);
	OMPfraction_t yFrac = (c->b + gridRange) / (gridRange);

	if (xFrac < 0 || yFrac < 0 || xFrac > 1.0 || yFrac > 1.0)
		return -1;

	OMPfraction_t scale = (gridSize - 1) / (gridRange);

	int32_t x = (int32_t)(xFrac * scale);
	int32_t y = (int32_t)(yFrac * scale);

	return x + y * gridSize;
}

uint8_t* normalizeOMPGrid(OMPbucket_t** grid, int32_t numThreads, uint32_t gridSize) {
	uint64_t i;
	uint32_t j, temp, max = 0;
	// find the largest number of hits in a single position
	/*for (i = 0; i < gridSize; i++) {
		for (j = 0; j < gridSize; j++) {
			temp = 0;
			for (k = 0; k < numThreads; k++) {
				temp += grid[k][i][j];
			}
			if (temp > max)
				max = temp;
		}
	}*/
	for (i = 0; i < gridSize * gridSize; i++) {
		temp = 0;
		for (j = 0; j < (uint32_t)numThreads; j++) {
			temp += grid[j][i];
		}
		// save the sum of each thread to thread 0 to make the next step easier
		grid[0][i] = temp;
		if (temp > max)
			max = temp;
	}

	for (i = 1; i < numThreads; i++) {
		free(grid[i]);
	}

	/*
	TODO

	OMP the normalization step
	*/
	// then normalize it to that maximum
	uint8_t* outGrid = (uint8_t*)CoTaskMemAlloc(sizeof(uint8_t) * gridSize * gridSize);
	//uint16_t* outGrid = (uint16_t*)malloc(sizeof(uint16_t) * (int)pow(GRID_SIZE / SUPERSAMPLE_SIZE, 2));

	for (i = 0; i < gridSize * gridSize; i++) {
		uint8_t val = (uint8_t)((double)grid[0][i] / max) * 0xFF;	// the maximum value of a uint8
		outGrid[i] = val;
	}

	//for (i = 0; i < GRID_SIZE; i++) {
	//	for (j = 0; j < GRID_SIZE; j++) {
	//		uint16_t temp = 0;
	//		for (k = 0; k < numThreads; k++) {
	//			temp += grid[k][i][j];
	//			//temp += grid[i][j];
	//		}
	//		uint16_t val = ((double)temp / (double)max) * 0xfff;	// max value of uint16
	//		outGrid[j + GRID_SIZE * i] = val;
	//	}
	//}

	free(grid[0]);
	free(grid);

	return outGrid;
}

extern __declspec(dllexport) uint8_t* RunOMPbrot(uint16_t numThreads, uint32_t gridSize, uint32_t maxIterations, uint32_t minIterations,
												uint32_t supersampling, OMPfraction_t gridRange, OMPfraction_t maxOrbit) {
	omp_set_num_threads(numThreads);
	//clock_t start = clock();
	// do math
	OMPfraction_t stepSize = 1.0 / (double)supersampling;
	OMPbucket_t** grid;
	complex** cache;
	int64_t i, j;
	uint32_t k, n, coord, thread, rows = 0, pRows = 0;
	#pragma omp parallel private(i, j, k, n, coord, thread) firstprivate(pRows) shared(grid)
	{
		#pragma omp master
		{
			numThreads = omp_get_num_threads();
			grid = malloc(sizeof(OMPbucket_t*) * numThreads);
			for (k = 0; k < numThreads; k++) {
				grid[k] = malloc(sizeof(OMPbucket_t) * gridSize * gridSize);
			}

			cache = malloc(sizeof(complex*) * numThreads);
			for (k = 0; k < numThreads; k++) {
				cache[k] = malloc(sizeof(complex) * (maxIterations - minIterations));
			}
		}

		thread = omp_get_thread_num();
		memset(grid[thread], 0x00, gridSize * gridSize * sizeof(OMPbucket_t));
		
		#pragma omp barrier
		memset(cache[thread], 0x00, (maxIterations - minIterations) * sizeof(complex));
		#pragma omp for schedule(dynamic,100)
		for (i = 0; i < gridSize * supersampling; i++) {
			for (j = 0; j < gridSize * supersampling; j++) {
				// create and initialize c
				complex c;
				c.a = getImgCoord((OMPfraction_t)j * stepSize, gridSize, gridRange);
				c.b = getImgCoord((OMPfraction_t)i * stepSize, gridSize, gridRange);
				// and z_0
				complex z;
				memcpy(&z, &c, sizeof(complex));
				// then do the actual iterations
				for (k = 0; k < maxIterations; k++) {
					if (k >= minIterations) {
						cache[thread][k - minIterations] = z;
					}
					// Z_n+1 = Z_n^2 + c
					complexSquare(&z);
					complexAdd(&z, &c);
					OMPfraction_t cDist = complexDistance(&z, &c);
					if (cDist > maxOrbit) {
						// c is NOT in the set, so read through the cached positions and record them
						for (n = 0; n < k - minIterations; n++) {
							z = cache[thread][n];
							coord = getGridCoord(&z, gridRange, gridSize);
							if (coord == -1)
								continue;
							grid[thread][coord]++;
						}
						break;
					}

					//if (cDist > MAX_ORBIT_DIST) {
					//	// c is NOT in the set, so start the iterations over, recording positions
					//	memcpy(&z, &c, sizeof(complex));
					//	for (n = 0; n < k; n++) {
					//		complexSquare(&z);
					//		complexAdd(&z, &c);
					//		cDist = complexDistance(&z, &c);
					//		x = getGridCoord(z.a);
					//		if (x < 0 || x >= GRID_SIZE)	// this "particle" is outside the grid, but has not escaped yet
					//			continue;
					//		y = getGridCoord(z.b);
					//		if (y < 0 || y >= GRID_SIZE)	// this "particle" is outside the grid, but has not escaped yet
					//			continue;
					//		if (n > MIN_ITER)
					//			grid[thread][y][x]++;
					//	}
					//	break;
					//}
				}
			}
			pRows++;
			if (pRows % 10 == 0) {
				#pragma omp atomic
					rows += 10;
				pRows = 0;
				if (rows % 100 == 0) {
					printf("Finished %d rows out of %d\n", rows, gridSize * supersampling);
				}
			}
		}
	}
	for (i = 0; i < numThreads; i++) {
		free(cache[i]);
	}
	free(cache);
	//clock_t calc = clock();
	//printf("Finished calculations in %f seconds, beginning normalization\n", ((double)calc - (double)start) / CLOCKS_PER_SEC);
	uint8_t* normalized = normalizeOMPGrid(grid, numThreads, gridSize);
	return normalized;
	//clock_t norm = clock();
	//printf("Finished normalization in %f seconds, beginning write\n", ((double)norm - (double)calc) / CLOCKS_PER_SEC);
	//writeImage(GRID_SIZE, GRID_SIZE, normalized);
	//clock_t done = clock();
	//printf("Finished all operations in %f seconds.\n", ((double)done - (double)start) / CLOCKS_PER_SEC);
	//// free grids here
	//free(normalized);
}