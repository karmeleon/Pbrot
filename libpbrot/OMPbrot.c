#include "PbrotCommon.h"

typedef struct {
	OMPfraction_t a, b;
} complex;

__inline void complexSquare(complex* n) {
	// FOIL, bitches!
	OMPfraction_t tempA = n->a;
	n->a = (n->a * n->a) - (n->b * n->b);
	n->b = 2 * tempA * n->b;
}

// computes the distance from x to y. sqrt is crazy slow and we don't need precision here.
__inline OMPfraction_t complexDistance(complex* x, complex* y) {
	return /*sqrt*/(abs(x->a - y->a) + abs(x->b - y->b));
}

// adds y to x and stores the result in x
__inline void complexAdd(complex* x, complex* y) {
	x->a += y->a;
	x->b += y->b;
}

__inline OMPfraction_t getImgCoord(OMPfraction_t n, uint32_t gridSize, OMPfraction_t gridRange) {
	OMPfraction_t temp = n - gridSize / 2;
	return (temp / (OMPfraction_t)(gridSize / 2)) * gridRange;
}

__inline int64_t getGridCoord(complex* c, uint32_t gridRange, uint32_t gridSize) {
	// the denominator in fracs and scale may need to be gridRange * 2
	OMPfraction_t xFrac = (c->a + gridRange) / (gridRange * 2);
	OMPfraction_t yFrac = (c->b + gridRange) / (gridRange * 2);

	if (xFrac < 0 || yFrac < 0 || xFrac > 1.0 || yFrac > 1.0)
		return -1;

	int64_t scale = (gridSize - 1);

	int64_t x = (int64_t)(xFrac * scale);
	int64_t y = (int64_t)(yFrac * scale);

	return x + y * gridSize;
}

static OMPbucket_t** sGrid;
static int32_t sNumThreads;
static uint32_t sGridSize;

extern __declspec(dllexport) uint8_t* normalizeOMPGrid() {
	uint64_t i;
	uint32_t j, temp, max = 0;
	// find the largest number of hits in a single position
	for (i = 0; i < sGridSize * sGridSize; i++) {
		temp = 0;
		for (j = 0; j < (uint32_t)sNumThreads; j++) {
			temp += sGrid[j][i];
		}
		// save the sum of each thread to thread 0 to make the next step easier
		sGrid[0][i] = temp;
		if (temp > max)
			max = temp;
	}

	for (i = 1; i < sNumThreads; i++) {
		free(sGrid[i]);
	}

	// then normalize it to that maximum
	uint8_t* outGrid = (uint8_t*)CoTaskMemAlloc(sizeof(uint8_t) * sGridSize * sGridSize);

	for (i = 0; i < sGridSize * sGridSize; i++) {
		uint8_t val = ((double)sGrid[0][i] / max) * 0xFF;	// the maximum value of uint8
		outGrid[i] = val;
	}

	free(sGrid[0]);
	free(sGrid);

	return outGrid;
}

extern __declspec(dllexport) void RunOMPbrot(uint16_t numThreads, uint32_t gridSize, uint32_t maxIterations, uint32_t minIterations, uint32_t supersampling,
												byte unsafeMode, double gridRange, double maxOrbit, volatile uint32_t* progress) {
	omp_set_num_threads(numThreads);
	if (!unsafeMode)
		sNumThreads = numThreads;
	else
		sNumThreads = 1;
	sGridSize = gridSize;
	*progress = 0;
	//clock_t start = clock();
	// do math
	OMPfraction_t stepSize = 1.0 / (OMPfraction_t)supersampling;
	OMPbucket_t** grid;
	complex** cache;
	int64_t i, j, coord;
	uint32_t k, n, thread, pRows = 0;
	#pragma omp parallel private(i, j, k, n, coord, thread) firstprivate(pRows) shared(grid)
	{
		#pragma omp master
		{
			numThreads = omp_get_num_threads();
			if (!unsafeMode) {
				grid = malloc(sizeof(OMPbucket_t*) * numThreads);
				for (k = 0; k < numThreads; k++) {
					grid[k] = malloc(sizeof(OMPbucket_t) * gridSize * gridSize);
				}
			}
			else {
				grid = malloc(sizeof(OMPbucket_t*));
				grid[0] = malloc(sizeof(OMPbucket_t) * gridSize * gridSize);
				memset(grid[0], 0x00, gridSize * gridSize * sizeof(OMPbucket_t));
			}

			cache = malloc(sizeof(complex*) * numThreads);
			for (k = 0; k < numThreads; k++) {
				cache[k] = malloc(sizeof(complex) * (maxIterations - minIterations));
			}
		}

		thread = omp_get_thread_num();
		
		#pragma omp barrier
		if (!unsafeMode)
			memset(grid[thread], 0x00, gridSize * gridSize * sizeof(OMPbucket_t));
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
					// Z_n+1 = Z_n^2 + c
					complexSquare(&z);
					complexAdd(&z, &c);
					OMPfraction_t cDist = complexDistance(&z, &c);
					if (k >= minIterations) {
						cache[thread][k - minIterations] = z;
					}
					if (cDist > maxOrbit) {
						// c is NOT in the set, so read through the cached positions and record them
						for (n = 0; n < k - minIterations + 1; n++) {
							z = cache[thread][n];
							coord = getGridCoord(&z, gridRange, gridSize);
							if (coord == -1)
								continue;
							if (!unsafeMode)
								grid[thread][coord]++;
							else
								grid[0][coord]++;
						}
						break;
					}
				}
			}
			pRows++;
			if (pRows % 100 == 0) {
				#pragma omp atomic
					*progress += 100;
				pRows = 0;
			}
		}
	}
	for (i = 0; i < numThreads; i++) {
		free(cache[i]);
	}
	free(cache);

	sGrid = grid;
}