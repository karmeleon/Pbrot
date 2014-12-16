#define fraction_t float
#define bucket_t uint

typedef struct {
	fraction_t a, b;
} complex;

void complexSquare(complex* n) {
	// FOIL, bitches!
	fraction_t tempA = n->a;
	//n->a = pow(n->a, 2) - pow(n->b, 2);
	n->a = (n->a * n->a) - (n->b * n->b);
	n->b = 2 * tempA * n->b;
}

// computes the distance from x to y. sqrt is crazy slow and we don't need precision here.
fraction_t complexDistance(complex* x, complex* y) {
	//return sqrt(pow(x->a - y->a, 2) + pow(x->b - y->b, 2));
	return /*sqrt*/((x->a - y->a) * (x->a - y->a) + (x->b - y->b) * (x->b - y->b));
}

// adds y to x and stores the result in x
void complexAdd(complex* x, complex* y) {
	x->a += y->a;
	x->b += y->b;
}

fraction_t getImgCoord(int n, int GRID_SIZE, int GRID_RANGE) {
	fraction_t temp = n - GRID_SIZE / 2;
	return (temp / (fraction_t)(GRID_SIZE / 2)) * GRID_RANGE;
}

int getGridCoord(fraction_t a, int GRID_SIZE, int GRID_RANGE) {
	a = (a + GRID_RANGE) / (GRID_RANGE);
	fraction_t scale = (GRID_SIZE - 1) / GRID_RANGE;
	a *= scale;
	return (int)a;
}

__kernel void buddhabrot(__global bucket_t *grid, __const int GRID_SIZE, __const int MAX_ITER,
	__const int MIN_ITER, __const int SUPERSAMPLE_SIZE, __const fraction_t GRID_RANGE, __const fraction_t MAX_ORBIT_DIST)
{
	size_t rank = get_global_id(0);	// the index we're working on
	//size_t workSize = get_work_dim();	// number of elements to work on
	//size_t totalRanks = get_global_size(0);	// total ranks

	int gridX = rank % GRID_SIZE;
	int gridY = rank / GRID_SIZE;

	fraction_t distanceToNextPixel = GRID_RANGE * 2.0 / GRID_SIZE;
	fraction_t supersamplingStep = distanceToNextPixel / SUPERSAMPLE_SIZE;
	//fraction_t startA = getImgCoord

	int i, j, k, n;

	fraction_t initialA = getImgCoord(gridX /*- distanceToNextPixel / 2.0*/, GRID_SIZE, GRID_RANGE);
	fraction_t initialB = getImgCoord(gridY /*- distanceToNextPixel / 2.0*/, GRID_SIZE, GRID_RANGE);

	complex c;
	complex z;
	c.a = initialA;
	c.b = initialB;

	for (i = 0; i < SUPERSAMPLE_SIZE; i++, c.b += supersamplingStep) {
		for (j = 0; j < SUPERSAMPLE_SIZE; j++, c.a += supersamplingStep) {
			z.a = c.a;
			z.b = c.b;

			for (k = 0; k < MAX_ITER; k++) {
				complexSquare(&z);
				complexAdd(&z, &c);
				fraction_t cDist = complexDistance(&z, &c);
				if (cDist > MAX_ORBIT_DIST) {
					// this point is not in the Mandelbrot set, so we're interested in it
					z.a = c.a;
					z.b = c.b;
					for (n = 0; n < MAX_ITER; n++) {
						complexSquare(&z);
						complexAdd(&z, &c);
						cDist = complexDistance(&z, &c);
						if (cDist > MAX_ORBIT_DIST)
							break;
						int x = getGridCoord(z.a, GRID_SIZE, GRID_RANGE);
						if (x < 0 || x >= GRID_SIZE)
							continue;
						int y = getGridCoord(z.b, GRID_SIZE, GRID_RANGE);
						if (y < 0 || y >= GRID_SIZE)
							continue;
						if (n > MIN_ITER)
							atomic_inc(&(grid[x + GRID_SIZE * y]));
					}
					break;
				}
			}
		}
		c.a = initialA;
	}

	/*complex c;
	c.a = getImgCoord(gridX, GRID_SIZE, GRID_RANGE);
	c.b = getImgCoord(gridY, GRID_SIZE, GRID_RANGE);
	complex z;
	z.a = getImgCoord(gridX, GRID_SIZE, GRID_RANGE);
	z.b = getImgCoord(gridY, GRID_SIZE, GRID_RANGE);

	int k, i;
	for (k = 0; k < MAX_ITER; k++) {
		complexSquare(&z);
		complexAdd(&z, &c);
		fraction_t cDist = complexDistance(&z, &c);
		if (cDist > MAX_ORBIT_DIST) {
			z.a = getImgCoord(gridX, GRID_SIZE, GRID_RANGE);
			z.b = getImgCoord(gridY, GRID_SIZE, GRID_RANGE);
			for (i = 0; i < k; i++) {
				complexSquare(&z);
				complexAdd(&z, &c);
				cDist = complexDistance(&z, &c);
				if (cDist > MAX_ORBIT_DIST)
					break;
				int x = getGridCoord(z.a, GRID_SIZE, GRID_RANGE);
				if (x < 0 || x >= GRID_SIZE)
					continue;
				int y = getGridCoord(z.b, GRID_SIZE, GRID_RANGE);
				if (y < 0 || y >= GRID_SIZE)
					continue;
				if (i > MIN_ITER)
					atomic_inc(&(grid[x + GRID_SIZE * y]));
			}
			break;
		}
	}*/
}