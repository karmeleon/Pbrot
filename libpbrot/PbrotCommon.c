#include "PbrotCommon.h"

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