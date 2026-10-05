#include <stdio.h>
#include <stdlib.h>
#include "opl3.h"
int main(int argc, char **argv) {
	opl3_chip chip; OPL3_Reset(&chip, 49716);
	FILE *f = fopen(argv[1], "r"); int callbacks = atoi(argv[2]); FILE *o = fopen(argv[3], "wb");
	int cb, reg, val, have = fscanf(f, "%d %d %d", &cb, &reg, &val) == 3; int rem = 0;
	OPL3_WriteReg(&chip, 0x105, 0); /* explicit OPL2 mode */
	static int16_t buf[4096];
	/* init writes (cb -1) are in the log too */
	for (int c = 0; c < callbacks; c++) {
		while (have && cb <= c) { OPL3_WriteRegBuffered(&chip, reg, val); have = fscanf(f, "%d %d %d", &cb, &reg, &val) == 3; }
		rem += 49716; int frames = rem / 472; rem %= 472;
		OPL3_GenerateStream(&chip, buf, frames); fwrite(buf, 4, frames, o);
	}
	fclose(o); return 0;
}
