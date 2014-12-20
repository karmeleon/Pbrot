#include <stdio.h>
#include <stdint.h>
#include <math.h>
#include <time.h>
#include <objbase.h>
#include <omp.h>
#include "simpleCL.h"

#define CLbucket_t uint32_t
#define OMPbucket_t uint16_t

#define CLfraction_t float
#define OMPfraction_t double

uint8_t* normalizeCLGrid(CLbucket_t* grid, int gridSize);