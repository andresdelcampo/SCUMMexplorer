#include <cassert>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <algorithm>
typedef uint8_t byte; typedef uint8_t uint8; typedef int8_t int8; typedef uint16_t uint16; typedef int16_t int16; typedef int32_t int32; typedef uint32_t uint32; typedef unsigned int uint;
#define ARRAYSIZE(a) (sizeof(a)/sizeof((a)[0]))
#define READ_LE_UINT16(p) ((uint16)(((const byte*)(p))[0] | (((const byte*)(p))[1] << 8)))
#define debugC(...) do{}while(0)
#define DEBUG_SOUND 0
template<class T> T ABS(T v){return v<0?-v:v;}
template<class T> T CLIP(T v,T lo,T hi){return v<lo?lo:(v>hi?hi:v);}
#define GID_LOOM 1
namespace Audio { namespace Mixer { enum { kMaxChannelVolume = 255 }; } }
static int g_cb = -1; static FILE *g_log;
struct FakeOPL { void writeReg(int r, int v) { fprintf(g_log, "%d %d %d\n", g_cb, r, v & 0xff); } };
struct Res { void lock(int,int){} void unlock(int,int){} };
struct Game { int id; };
struct ScummEngine { Game _game; Res *_res; };
enum { rtSound = 1 };
static void error(const char *f, ...) { fprintf(stderr, "error\n"); exit(3); }
namespace Scumm {
/**
 * Sound output for v3/v4 AdLib data.
 */
class Player_AD {
public:

	// MusicEngine API
	void setMusicVolume(int vol);
	void startSound(int sound);
	void stopSound(int sound);
	void stopAllSounds();
	int  getMusicTimer();
	int  getSoundStatus(int sound) const;


	// Timer callback
	void onTimer();

public:
	ScummEngine *_vm;
	int _mutexDummy;

	void setupVolume();
	int _musicVolume;
	int _sfxVolume;

	FakeOPL *_opl2;

	int _musicResource;
	int32 _engineMusicTimer;

	struct SfxSlot;

	struct HardwareChannel {
		bool allocated;
		int priority;
		SfxSlot *sfxOwner;
	} _hwChannels[9];
	int _numHWChannels;
	static const int _operatorOffsetToChannel[22];

	int allocateHWChannel(int priority, SfxSlot *owner = nullptr);
	void freeHWChannel(int channel);
	void limitHWChannels(int newCount);

	// AdLib register utilities
	uint8 _registerBackUpTable[256];
	void writeReg(int r, int v);
	uint8 readReg(int r) const;

	// Instrument setup
	void setupChannel(const uint channel, const byte *instrOffset);
	void setupOperator(const uint opr, const byte *&instrOffset);
	static const int _operatorOffsetTable[18];

	// Music handling
	void startMusic();
	void stopMusic();
	void updateMusic();
	bool parseCommand();
	uint parseVLQ();
	void noteOff(uint channel);
	void setupFrequency(uint channel, int8 frequency);
	void setupRhythm(uint rhythmInstr, uint instrOffset);

	const byte *_musicData;
	uint _timerLimit;
	uint _musicTicks;
	uint32 _musicTimer;
	uint32 _internalMusicTimer;
	bool _loopFlag;
	uint _musicLoopStart;
	uint _instrumentOffset[16];

	struct VoiceChannel {
		uint lastEvent;
		uint frequency;
		uint b0Reg;
	} _voiceChannels[9];
	void freeVoiceChannel(uint channel);

	void musicSeekTo(const uint position);
	bool _isSeeking;

	uint _mdvdrState;

	uint32 _curOffset;
	uint32 _nextEventTimer;

	static const uint _noteFrequencies[12];
	static const uint _mdvdrTable[6];
	static const uint _rhythmOperatorTable[6];
	static const uint _rhythmChannelTable[6];

	// SFX handling
	enum {
		kNoteStatePreInit = -1,
		kNoteStateAttack = 0,
		kNoteStateDecay = 1,
		kNoteStateSustain = 2,
		kNoteStateRelease = 3,
		kNoteStateOff = 4
	};

	struct Note {
		int state;
		int playTime;
		int sustainTimer;
		int instrumentValue;
		int bias;
		int preIncrease;
		int adjust;

		struct Envelope {
			int stepIncrease;
			int step;
			int stepCounter;
			int timer;
		} envelope;
	};

	enum {
		kChannelStateOff = 0,
		kChannelStateParse = 1,
		kChannelStatePlay = 2
	};

	struct Channel {
		int state;
		const byte *currentOffset;
		const byte *startOffset;
		uint8 instrumentData[7];

		Note notes[2];

		int hardwareChannel;
	};

	struct SfxSlot {
		int resource;
		int priority;

		Channel channels[3];
	} _sfx[3];

	SfxSlot *allocateSfxSlot(int priority);
	bool startSfx(SfxSlot *sfx, const byte *resource);
	void stopSfx(SfxSlot *sfx);

	void updateSfx();
	void clearChannel(const Channel &channel);
	void updateChannel(Channel *channel);
	void parseSlot(Channel *channel);
	void updateSlot(Channel *channel);
	void parseNote(Note *note, const Channel &channel, const byte *offset);
	bool processNote(Note *note, const Channel &channel, const byte *offset);
	void noteOffOn(int channel);
	void writeRegisterSpecial(int channel, uint8 value, int offset);
	uint8 readRegisterSpecial(int channel, uint8 defaultValue, int offset);
	void setupNoteEnvelopeState(Note *note, int steps, int adjust);
	bool processNoteEnvelope(Note *note);

	int _sfxTimer;

	uint8 _rndSeed;
	uint8 getRnd();

	static const uint _noteBiasTable[7];
	static const uint _numStepsTable[16];
	static const uint _noteAdjustScaleTable[7];
	static const uint _noteAdjustTable[16];
	static const bool _useOperatorTable[7];
	static const uint _channelOffsetTable[11];
	static const uint _channelOperatorOffsetTable[7];
	static const uint _baseRegisterTable[7];
	static const uint _registerMaskTable[7];
	static const uint _registerShiftTable[7];
};
void Player_AD::onTimer() {

	if (_curOffset) {
		updateMusic();
	}

	updateSfx();
}
int Player_AD::allocateHWChannel(int priority, SfxSlot *owner) {
	// We always reaLlocate the channel with the lowest priority in case none
	// is free.
	int channel = -1;
	int minPrio = priority;

	for (int i = 0; i < _numHWChannels; ++i) {
		if (!_hwChannels[i].allocated) {
			channel = i;
			break;
		}

		// We don't allow SFX to reallocate their own channels. Otherwise we
		// would call stopSfx in the midst of startSfx and that can lead to
		// horrible states...
		// We also prevent the music from reallocating its own channels like
		// in the original.
		if (_hwChannels[i].priority <= minPrio && _hwChannels[i].sfxOwner != owner) {
			minPrio = _hwChannels[i].priority;
			channel = i;
		}
	}

	if (channel != -1) {
		// In case the HW channel belongs to a SFX we will completely
		// stop playback of that SFX.
		// TODO: Maybe be more fine grained in the future and allow
		// detachment of individual channels of a SFX?
		if (_hwChannels[channel].allocated && _hwChannels[channel].sfxOwner) {
			stopSfx(_hwChannels[channel].sfxOwner);
		}

		_hwChannels[channel].allocated = true;
		_hwChannels[channel].priority = priority;
		_hwChannels[channel].sfxOwner = owner;
	}

	return channel;
}

void Player_AD::freeHWChannel(int channel) {
	assert(_hwChannels[channel].allocated);
	_hwChannels[channel].allocated = false;
	_hwChannels[channel].sfxOwner = nullptr;
}

void Player_AD::limitHWChannels(int newCount) {
	for (int i = newCount; i < ARRAYSIZE(_hwChannels); ++i) {
		if (_hwChannels[i].allocated) {
			freeHWChannel(i);
		}
	}
	_numHWChannels = newCount;
}

const int Player_AD::_operatorOffsetToChannel[22] = {
	 0,  1,  2,  0,  1,  2, -1, -1,
	 3,  4,  5,  3,  4,  5, -1, -1,
	 6,  7,  8,  6,  7,  8
};

void Player_AD::writeReg(int r, int v) {
	if (r >= 0 && r < ARRAYSIZE(_registerBackUpTable)) {
		_registerBackUpTable[r] = v;
	}

	// Handle volume scaling depending on the sound type.
	if (r >= 0x40 && r <= 0x55) {
		const int operatorOffset = r - 0x40;
		const int channel = _operatorOffsetToChannel[operatorOffset];
		if (channel != -1) {
			const bool twoOPOutput = (readReg(0xC0 + channel) & 0x01) != 0;

			int scale = Audio::Mixer::kMaxChannelVolume;
			// We only scale the volume of operator 2 unless both operators
			// are set to directly produce sound.
			if (twoOPOutput || operatorOffset == _operatorOffsetTable[channel * 2 + 1]) {
				if (_hwChannels[channel].sfxOwner) {
					scale = _sfxVolume;
				} else {
					scale = _musicVolume;
				}
			}

			int vol = 0x3F - (v & 0x3F);
			vol = vol * scale / Audio::Mixer::kMaxChannelVolume;
			v &= 0xC0;
			v |= (0x3F - vol);
		}
	}

	// Since AdLib's lowest volume level does not imply that the sound is
	// completely silent we ignore key on in such a case.
	// We also ignore key on for music whenever we do seeking.
	if (r >= 0xB0 && r <= 0xB8) {
		const int channel = r - 0xB0;
		bool mute = false;
		if (_hwChannels[channel].sfxOwner) {
			if (!_sfxVolume) {
				mute = true;
			}
		} else {
			if (!_musicVolume) {
				mute = true;
			} else {
				mute = _isSeeking;
			}
		}

		if (mute) {
			v &= ~0x20;
		}
	}

	_opl2->writeReg(r, v);
}

uint8 Player_AD::readReg(int r) const {
	if (r >= 0 && r < ARRAYSIZE(_registerBackUpTable)) {
		return _registerBackUpTable[r];
	} else {
		return 0;
	}
}

void Player_AD::setupChannel(const uint channel, const byte *instrOffset) {
	instrOffset += 2;
	writeReg(0xC0 + channel, *instrOffset++);
	setupOperator(_operatorOffsetTable[channel * 2 + 0], instrOffset);
	setupOperator(_operatorOffsetTable[channel * 2 + 1], instrOffset);
}

void Player_AD::setupOperator(const uint opr, const byte *&instrOffset) {
	writeReg(0x20 + opr, *instrOffset++);
	writeReg(0x40 + opr, *instrOffset++);
	writeReg(0x60 + opr, *instrOffset++);
	writeReg(0x80 + opr, *instrOffset++);
	writeReg(0xE0 + opr, *instrOffset++);
}

const int Player_AD::_operatorOffsetTable[18] = {
	 0,  3,  1,  4,
	 2,  5,  8, 11,
	 9, 12, 10, 13,
	16, 19, 17, 20,
	18, 21
};

// Music

void Player_AD::startMusic() {
	memset(_instrumentOffset, 0, sizeof(_instrumentOffset));

	bool hasRhythmData = false;
	uint instruments = _musicData[10];
	for (uint i = 0; i < instruments; ++i) {
		const int instrIndex = _musicData[11 + i] - 1;
		if (0 <= instrIndex && instrIndex < 16) {
			_instrumentOffset[instrIndex] = i * 16 + 16 + 3;
			hasRhythmData |= (_musicData[_instrumentOffset[instrIndex] + 13] != 0);
		}
	}

	if (hasRhythmData) {
		_mdvdrState = 0x20;
		limitHWChannels(6);
	} else {
		_mdvdrState = 0;
		limitHWChannels(9);
	}

	_curOffset = 0x93;
	// TODO: is this the same for Loom?
	_nextEventTimer = 40;
	_engineMusicTimer = 0;
	_internalMusicTimer = 0;
	_musicTimer = 0;

	writeReg(0xBD, _mdvdrState);

	const bool isLoom = (_vm->_game.id == GID_LOOM);
	_timerLimit = isLoom ? 473 : 256;
	_musicTicks = _musicData[3] * (isLoom ? 2 : 1);
	_loopFlag = (_musicData[4] == 0);
	_musicLoopStart = _curOffset + READ_LE_UINT16(_musicData + 5);
}

void Player_AD::stopMusic() {
	if (_musicResource == -1) {
		return;
	}

	// Unlock the music resource if present
	_vm->_res->unlock(rtSound, _musicResource);
	_musicResource = -1;

	// Stop the music playback
	_curOffset = 0;

	// Stop all music voice channels
	for (int i = 0; i < ARRAYSIZE(_voiceChannels); ++i) {
		if (_voiceChannels[i].lastEvent) {
			noteOff(i);
		}
	}

	// Reset rhythm state
	writeReg(0xBD, 0x00);
	limitHWChannels(9);
}

void Player_AD::updateMusic() {
	_musicTimer += _musicTicks;
	if (_musicTimer < _timerLimit) {
		return;
	}
	_musicTimer -= _timerLimit;

	++_internalMusicTimer;
	if (_internalMusicTimer > 120) {
		_internalMusicTimer = 0;
		++_engineMusicTimer;
	}

	--_nextEventTimer;
	if (_nextEventTimer) {
		return;
	}

	while (true) {
		if (parseCommand()) {
			// We received an EOT command. In case there's no music playing
			// we know there was no looping enabled. Thus, we stop further
			// handling. Otherwise we will just continue parsing. It is
			// important to note that we need to parse a command directly
			// at the new position, i.e. there is no time value we need to
			// parse.
			if (_musicResource == -1) {
				return;
			} else {
				continue;
			}
		}

		// In case there is a delay till the next event stop handling.
		if (_musicData[_curOffset] != 0) {
			break;
		}
		++_curOffset;
	}

	_nextEventTimer = parseVLQ();
	_nextEventTimer >>= (_vm->_game.id == GID_LOOM) ? 2 : 1;
	if (!_nextEventTimer) {
		_nextEventTimer = 1;
	}
}

bool Player_AD::parseCommand() {
	uint command = _musicData[_curOffset++];
	if (command == 0xFF) {
		// META EVENT
		// Get the command number.
		command = _musicData[_curOffset++];
		if (command == 47) {
			// End of track
			if (_loopFlag) {
				// In case the track is looping jump to the start.
				_curOffset = _musicLoopStart;
				_nextEventTimer = 0;
			} else {
				// Otherwise completely stop playback.
				stopMusic();
			}
			return true;
		} else if (command == 88) {
			// This is proposedly a debug information insertion. The CMS
			// player code handles this differently, but is still using
			// the same resources...
			_curOffset += 5;
		} else if (command == 81) {
			// Change tempo. This is used exclusively in Loom.
			const uint timing = _musicData[_curOffset + 2] | (_musicData[_curOffset + 1] << 8);
			_musicTicks = 0x73000 / timing;
			command = _musicData[_curOffset++];
			_curOffset += command;
		} else {
			// In case an unknown meta event occurs just skip over the
			// data by using the length supplied.
			command = _musicData[_curOffset++];
			_curOffset += command;
		}
	} else {
		if (command >= 0x90) {
			// NOTE ON
			// Extract the channel number and save it in command.
			command -= 0x90;

			const uint instrOffset = _instrumentOffset[command];
			if (instrOffset) {
				if (_musicData[instrOffset + 13] != 0) {
					setupRhythm(_musicData[instrOffset + 13], instrOffset);
				} else {
					// Priority 256 makes sure we always prefer music
					// channels over SFX channels.
					int channel = allocateHWChannel(256);
					if (channel != -1) {
						setupChannel(channel, _musicData + instrOffset);
						_voiceChannels[channel].lastEvent = command + 0x90;
						_voiceChannels[channel].frequency = _musicData[_curOffset];
						setupFrequency(channel, _musicData[_curOffset]);
					}
				}
			}
		} else {
			// NOTE OFF
			const uint note = _musicData[_curOffset];
			command += 0x10;

			// Find the output channel which plays the note.
			uint channel = 0xFF;
			for (int i = 0; i < ARRAYSIZE(_voiceChannels); ++i) {
				if (_voiceChannels[i].frequency == note && _voiceChannels[i].lastEvent == command) {
					channel = i;
					break;
				}
			}

			if (channel != 0xFF) {
				// In case a output channel playing the note was found,
				// stop it.
				noteOff(channel);
			} else {
				// In case there is no such note this will disable the
				// rhythm instrument played on the channel.
				command -= 0x90;
				const uint instrOffset = _instrumentOffset[command];
				if (instrOffset && _musicData[instrOffset + 13] != 0) {
					const uint rhythmInstr = _musicData[instrOffset + 13];
					if (rhythmInstr < 6) {
						_mdvdrState &= _mdvdrTable[rhythmInstr] ^ 0xFF;
						writeReg(0xBD, _mdvdrState);
					}
				}
			}
		}

		_curOffset += 2;
	}

	return false;
}

uint Player_AD::parseVLQ() {
	uint vlq = _musicData[_curOffset++];
	if (vlq & 0x80) {
		vlq -= 0x80;
		vlq <<= 7;
		vlq |= _musicData[_curOffset++];
	}
	return vlq;
}

void Player_AD::noteOff(uint channel) {
	writeReg(0xB0 + channel, _voiceChannels[channel].b0Reg & 0xDF);
	freeVoiceChannel(channel);
}

void Player_AD::setupFrequency(uint channel, int8 frequency) {
	frequency -= 31;
	if (frequency < 0) {
		frequency = 0;
	}

	uint octave = 0;
	while (frequency >= 12) {
		frequency -= 12;
		++octave;
	}

	const uint noteFrequency = _noteFrequencies[frequency];
	octave <<= 2;
	octave |= noteFrequency >> 8;
	octave |= 0x20;
	writeReg(0xA0 + channel, noteFrequency & 0xFF);
	_voiceChannels[channel].b0Reg = octave;
	writeReg(0xB0 + channel, octave);
}

void Player_AD::setupRhythm(uint rhythmInstr, uint instrOffset) {
	if (rhythmInstr == 1) {
		setupChannel(6, _musicData + instrOffset);
		writeReg(0xA6, _musicData[instrOffset++]);
		writeReg(0xB6, _musicData[instrOffset] & 0xDF);
		_mdvdrState |= 0x10;
		writeReg(0xBD, _mdvdrState);
	} else if (rhythmInstr < 6) {
		const byte *secondOperatorOffset = _musicData + instrOffset + 8;
		setupOperator(_rhythmOperatorTable[rhythmInstr], secondOperatorOffset);
		writeReg(0xA0 + _rhythmChannelTable[rhythmInstr], _musicData[instrOffset++]);
		writeReg(0xB0 + _rhythmChannelTable[rhythmInstr], _musicData[instrOffset++] & 0xDF);
		writeReg(0xC0 + _rhythmChannelTable[rhythmInstr], _musicData[instrOffset]);
		_mdvdrState |= _mdvdrTable[rhythmInstr];
		writeReg(0xBD, _mdvdrState);
	}
}

void Player_AD::freeVoiceChannel(uint channel) {
	VoiceChannel &vChannel = _voiceChannels[channel];
	assert(vChannel.lastEvent);

	freeHWChannel(channel);
	vChannel.lastEvent = 0;
	vChannel.b0Reg = 0;
	vChannel.frequency = 0;
}

void Player_AD::musicSeekTo(const uint position) {
	// This method is actually dangerous to use and should only be used for
	// loading save games because it does not set up anything like the engine
	// music timer or similar.
	_isSeeking = true;

	// Seek until the given position.
	while (_curOffset != position) {
		if (parseCommand()) {
			// We encountered an EOT command. This should not happen unless
			// we try to seek to an illegal position. In this case just abort
			// seeking.
			debugC(3, DEBUG_SOUND, "AD illegal seek to %u", position);
			break;
		}
		parseVLQ();
	}

	_isSeeking = false;

	// Turn on all notes.
	for (int i = 0; i < ARRAYSIZE(_voiceChannels); ++i) {
		if (_voiceChannels[i].lastEvent != 0) {
			const int reg = 0xB0 + i;
			writeReg(reg, readReg(reg));
		}
	}
}

const uint Player_AD::_noteFrequencies[12] = {
	0x200, 0x21E, 0x23F, 0x261,
	0x285, 0x2AB, 0x2D4, 0x300,
	0x32E, 0x35E, 0x390, 0x3C7
};

const uint Player_AD::_mdvdrTable[6] = {
	0x00, 0x10, 0x08, 0x04, 0x02, 0x01
};

const uint Player_AD::_rhythmOperatorTable[6] = {
	0x00, 0x00, 0x14, 0x12, 0x15, 0x11
};

const uint Player_AD::_rhythmChannelTable[6] = {
	0x00, 0x00, 0x07, 0x08, 0x08, 0x07
};

// SFX

Player_AD::SfxSlot *Player_AD::allocateSfxSlot(int priority) {
	// We always reaLlocate the slot with the lowest priority in case none is
	// free.
	SfxSlot *sfx = nullptr;
	int minPrio = priority;

	for (int i = 0; i < ARRAYSIZE(_sfx); ++i) {
		if (_sfx[i].resource == -1) {
			return &_sfx[i];
		} else if (_sfx[i].priority <= minPrio) {
			minPrio = _sfx[i].priority;
			sfx = &_sfx[i];
		}
	}

	// In case we reallocate a slot stop the old one.
	if (sfx) {
		stopSfx(sfx);
	}

	return sfx;
}

bool Player_AD::startSfx(SfxSlot *sfx, const byte *resource) {
	writeReg(0xBD, 0x00);

	// Clear the channels.
	sfx->channels[0].state = kChannelStateOff;
	sfx->channels[1].state = kChannelStateOff;
	sfx->channels[2].state = kChannelStateOff;

	// Set up the first channel to pick up playback.
	// Try to allocate a hardware channel.
	sfx->channels[0].hardwareChannel = allocateHWChannel(sfx->priority, sfx);
	if (sfx->channels[0].hardwareChannel == -1) {
		debugC(3, DEBUG_SOUND, "AD No hardware channel available");
		return false;
	}
	sfx->channels[0].currentOffset = sfx->channels[0].startOffset = resource + 2;
	sfx->channels[0].state = kChannelStateParse;

	// Scan for the start of the other channels and set them up if required.
	int curChannel = 1;
	const byte *bufferPosition = resource + 2;
	uint8 command = 0;
	while ((command = *bufferPosition) != 0xFF) {
		switch (command) {
		case 1:
			// INSTRUMENT DEFINITION
			bufferPosition += 15;
			break;

		case 2:
			// NOTE DEFINITION
			bufferPosition += 11;
			break;

		case 0x80:
			// LOOP
			bufferPosition += 1;
			break;

		default:
			// START OF CHANNEL
			bufferPosition += 1;
			if (curChannel >= 3) {
				error("AD SFX resource %d uses more than 3 channels", sfx->resource);
			}
			sfx->channels[curChannel].hardwareChannel = allocateHWChannel(sfx->priority, sfx);
			if (sfx->channels[curChannel].hardwareChannel == -1) {
				debugC(3, DEBUG_SOUND, "AD No hardware channel available");
				return false;
			}
			sfx->channels[curChannel].currentOffset = bufferPosition;
			sfx->channels[curChannel].startOffset = bufferPosition;
			sfx->channels[curChannel].state = kChannelStateParse;
			++curChannel;
			break;
		}
	}

	return true;
}

void Player_AD::stopSfx(SfxSlot *sfx) {
	if (sfx->resource == -1) {
		return;
	}

	// 1. step: Clear all the channels.
	for (int i = 0; i < ARRAYSIZE(sfx->channels); ++i) {
		if (sfx->channels[i].state) {
			clearChannel(sfx->channels[i]);
			sfx->channels[i].state = kChannelStateOff;
		}

		if (sfx->channels[i].hardwareChannel != -1) {
			freeHWChannel(sfx->channels[i].hardwareChannel);
			sfx->channels[i].hardwareChannel = -1;
		}
	}

	// 2. step: Unlock the resource.
	_vm->_res->unlock(rtSound, sfx->resource);
	sfx->resource = -1;
}

void Player_AD::updateSfx() {
	if (--_sfxTimer) {
		return;
	}
	_sfxTimer = 4;

	for (int i = 0; i < ARRAYSIZE(_sfx); ++i) {
		if (_sfx[i].resource == -1) {
			continue;
		}

		bool hasActiveChannel = false;
		for (int j = 0; j < ARRAYSIZE(_sfx[i].channels); ++j) {
			if (_sfx[i].channels[j].state) {
				hasActiveChannel = true;
				updateChannel(&_sfx[i].channels[j]);
			}
		}

		// In case no channel is active we will stop the sfx.
		if (!hasActiveChannel) {
			stopSfx(&_sfx[i]);
		}
	}
}

void Player_AD::clearChannel(const Channel &channel) {
	writeReg(0xA0 + channel.hardwareChannel, 0x00);
	writeReg(0xB0 + channel.hardwareChannel, 0x00);
}

void Player_AD::updateChannel(Channel *channel) {
	if (channel->state == kChannelStateParse) {
		parseSlot(channel);
	} else {
		updateSlot(channel);
	}
}

void Player_AD::parseSlot(Channel *channel) {
	while (true) {
		const byte *curOffset = channel->currentOffset;

		switch (*curOffset) {
		case 1:
			// INSTRUMENT DEFINITION
			++curOffset;
			channel->instrumentData[0] = *(curOffset + 0);
			channel->instrumentData[1] = *(curOffset + 2);
			channel->instrumentData[2] = *(curOffset + 9);
			channel->instrumentData[3] = *(curOffset + 8);
			channel->instrumentData[4] = *(curOffset + 4);
			channel->instrumentData[5] = *(curOffset + 3);
			channel->instrumentData[6] = 0;

			setupChannel(channel->hardwareChannel, curOffset);

			writeReg(0xA0 + channel->hardwareChannel, *(curOffset + 0));
			writeReg(0xB0 + channel->hardwareChannel, *(curOffset + 1) & 0xDF);

			channel->currentOffset += 15;
			break;

		case 2:
			// NOTE DEFINITION
			++curOffset;
			channel->state = kChannelStatePlay;
			noteOffOn(channel->hardwareChannel);
			parseNote(&channel->notes[0], *channel, curOffset + 0);
			parseNote(&channel->notes[1], *channel, curOffset + 5);
			return;

		case 0x80:
			// LOOP
			channel->currentOffset = channel->startOffset;
			break;

		default:
			// START OF CHANNEL
			// When we encounter a start of another channel while playback
			// it means that the current channel is finished. Thus, we will
			// stop it.
			clearChannel(*channel);
			channel->state = kChannelStateOff;
			return;
		}
	}
}

void Player_AD::updateSlot(Channel *channel) {
	const byte *curOffset = channel->currentOffset + 1;

	for (int num = 0; num <= 1; ++num, curOffset += 5) {
		if (!(*curOffset & 0x80)) {
			continue;
		}

		Note *const note = &channel->notes[num];
		bool updateNote = false;

		if (note->state == kNoteStateSustain) {
			if (!--note->sustainTimer) {
				updateNote = true;
			}
		} else {
			updateNote = processNoteEnvelope(note);

			if (note->bias) {
				writeRegisterSpecial(channel->hardwareChannel, note->bias - note->instrumentValue, *curOffset & 0x07);
			} else {
				writeRegisterSpecial(channel->hardwareChannel, note->instrumentValue, *curOffset & 0x07);
			}
		}

		if (updateNote) {
			if (processNote(note, *channel, curOffset)) {
				if (!(*curOffset & 0x08)) {
					channel->currentOffset += 11;
					channel->state = kChannelStateParse;
					continue;
				} else if (*curOffset & 0x10) {
					noteOffOn(channel->hardwareChannel);
				}

				note->state = kNoteStatePreInit;
				processNote(note, *channel, curOffset);
			}
		}

		if ((*curOffset & 0x20) && !--note->playTime) {
			channel->currentOffset += 11;
			channel->state = kChannelStateParse;
		}
	}
}

void Player_AD::parseNote(Note *note, const Channel &channel, const byte *offset) {
	if (*offset & 0x80) {
		note->state = kNoteStatePreInit;
		processNote(note, channel, offset);
		note->playTime = 0;

		if (*offset & 0x20) {
			note->playTime = (*(offset + 4) >> 4) * 118;
			note->playTime += (*(offset + 4) & 0x0F) * 8;
		}
	}
}

bool Player_AD::processNote(Note *note, const Channel &channel, const byte *offset) {
	if (++note->state == kNoteStateOff) {
		return true;
	}

	const int instrumentDataOffset = *offset & 0x07;
	note->bias = _noteBiasTable[instrumentDataOffset];

	uint8 instrumentDataValue = 0;
	if (note->state == kNoteStateAttack) {
		instrumentDataValue = channel.instrumentData[instrumentDataOffset];
	}

	uint8 noteInstrumentValue = readRegisterSpecial(channel.hardwareChannel, instrumentDataValue, instrumentDataOffset);
	if (note->bias) {
		noteInstrumentValue = note->bias - noteInstrumentValue;
	}
	note->instrumentValue = noteInstrumentValue;

	if (note->state == kNoteStateSustain) {
		note->sustainTimer = _numStepsTable[*(offset + 3) >> 4];

		if (*offset & 0x40) {
			note->sustainTimer = (((getRnd() << 8) * note->sustainTimer) >> 16) + 1;
		}
	} else {
		int timer1, timer2;
		if (note->state == kNoteStateRelease) {
			timer1 = *(offset + 3) & 0x0F;
			timer2 = 0;
		} else {
			timer1 = *(offset + note->state + 1) >> 4;
			timer2 = *(offset + note->state + 1) & 0x0F;
		}

		int adjustValue = ((_noteAdjustTable[timer2] * _noteAdjustScaleTable[instrumentDataOffset]) >> 16) - noteInstrumentValue;
		setupNoteEnvelopeState(note, _numStepsTable[timer1], adjustValue);
	}

	return false;
}

void Player_AD::noteOffOn(int channel) {
	const uint8 regValue = readReg(0xB0 | channel);
	writeReg(0xB0 | channel, regValue & 0xDF);
	writeReg(0xB0 | channel, regValue | 0x20);
}

void Player_AD::writeRegisterSpecial(int channel, uint8 value, int offset) {
	if (offset == 6) {
		return;
	}

	uint8 regNum;
	if (_useOperatorTable[offset]) {
		regNum = _operatorOffsetTable[_channelOperatorOffsetTable[offset] + channel * 2];
	} else {
		regNum = _channelOffsetTable[channel];
	}

	regNum += _baseRegisterTable[offset];

	uint8 regValue = readReg(regNum) & (~_registerMaskTable[offset]);
	regValue |= value << _registerShiftTable[offset];

	writeReg(regNum, regValue);
}

uint8 Player_AD::readRegisterSpecial(int channel, uint8 defaultValue, int offset) {
	if (offset == 6) {
		return 0;
	}

	uint8 regNum;
	if (_useOperatorTable[offset]) {
		regNum = _operatorOffsetTable[_channelOperatorOffsetTable[offset] + channel * 2];
	} else {
		regNum = _channelOffsetTable[channel];
	}

	regNum += _baseRegisterTable[offset];

	uint8 regValue;
	if (defaultValue) {
		regValue = defaultValue;
	} else {
		regValue = readReg(regNum);
	}

	regValue &= _registerMaskTable[offset];
	regValue >>= _registerShiftTable[offset];

	return regValue;
}

void Player_AD::setupNoteEnvelopeState(Note *note, int steps, int adjust) {
	note->preIncrease = 0;
	if (ABS(adjust) > steps) {
		note->preIncrease = 1;
		note->adjust = adjust / steps;
		note->envelope.stepIncrease = ABS(adjust % steps);
	} else {
		note->adjust = adjust;
		note->envelope.stepIncrease = ABS(adjust);
	}

	note->envelope.step = steps;
	note->envelope.stepCounter = 0;
	note->envelope.timer = steps;
}

bool Player_AD::processNoteEnvelope(Note *note) {
	if (note->preIncrease) {
		note->instrumentValue += note->adjust;
	}

	note->envelope.stepCounter += note->envelope.stepIncrease;
	if (note->envelope.stepCounter >= note->envelope.step) {
		note->envelope.stepCounter -= note->envelope.step;

		if (note->adjust < 0) {
			--note->instrumentValue;
		} else {
			++note->instrumentValue;
		}
	}

	if (--note->envelope.timer) {
		return false;
	} else {
		return true;
	}
}

uint8 Player_AD::getRnd() {
	if (_rndSeed & 1) {
		_rndSeed >>= 1;
		_rndSeed ^= 0xB8;
	} else {
		_rndSeed >>= 1;
	}

	return _rndSeed;
}

const uint Player_AD::_noteBiasTable[7] = {
	0x00, 0x00, 0x3F, 0x00, 0x3F, 0x00, 0x00
};

const uint Player_AD::_numStepsTable[16] = {
	    1,    4,    6,    8,
	   10,   14,   18,   24,
	   36,   64,  100,  160,
	  240,  340,  600, 1200
};

const uint Player_AD::_noteAdjustScaleTable[7] = {
	255,   7,  63,  15,  63,  15,  63
};

const uint Player_AD::_noteAdjustTable[16] = {
	    0,  4369,  8738, 13107,
	17476, 21845, 26214, 30583,
	34952, 39321, 43690, 48059,
	52428, 56797, 61166, 65535
};

const bool Player_AD::_useOperatorTable[7] = {
	false, false, true, true, true, true, false
};

const uint Player_AD::_channelOffsetTable[11] = {
	 0,  1,  2,  3,
	 4,  5,  6,  7,
	 8,  8,  7
};

const uint Player_AD::_channelOperatorOffsetTable[7] = {
	0, 0, 1, 1, 0, 0, 0
};

const uint Player_AD::_baseRegisterTable[7] = {
	0xA0, 0xC0, 0x40, 0x20, 0x40, 0x20, 0x00
};

const uint Player_AD::_registerMaskTable[7] = {
	0xFF, 0x0E, 0x3F, 0x0F, 0x3F, 0x0F, 0x00
};

const uint Player_AD::_registerShiftTable[7] = {
	0, 1, 0, 0, 0, 0, 0
};

} // End of namespace Scumm
using namespace Scumm;
int main(int argc, char **argv) {
	if (argc < 5) return 2;
	bool isMusic = !strcmp(argv[1], "music");
	FILE *f = fopen(argv[2], "rb"); std::vector<byte> data(65536); size_t n = fread(data.data(), 1, data.size(), f); fclose(f); data.resize(n + 64);
	g_log = fopen(argv[3], "w"); int callbacks = atoi(argv[4]);
	static Player_AD p; memset((void*)&p, 0, sizeof(p));
	static Res res; static ScummEngine vm; vm._game.id = GID_LOOM; vm._res = &res;
	ScummEngine *vmp = &vm; p._vm = vmp;
	static FakeOPL opl; p._opl2 = &opl;
	p._musicVolume = p._sfxVolume = 255;
	memset(p._registerBackUpTable, 0, sizeof(p._registerBackUpTable));
	p.writeReg(0x01, 0x00); p.writeReg(0xBD, 0x00); p.writeReg(0x08, 0x00); p.writeReg(0x01, 0x20);
	p._engineMusicTimer = 0; p._musicResource = -1; p._curOffset = 0; p._sfxTimer = 4; p._rndSeed = 1;
	for (int i = 0; i < 3; ++i) { p._sfx[i].resource = -1; for (int j = 0; j < 3; ++j) p._sfx[i].channels[j].hardwareChannel = -1; }
	p._numHWChannels = ARRAYSIZE(p._hwChannels); p._isSeeking = false;
	const byte *r = data.data();
	if (isMusic) { p.stopMusic(); p._musicResource = 1; p._musicData = r; p.startMusic(); }
	else {
		Player_AD::SfxSlot *sfx = p.allocateSfxSlot(r[0]);
		sfx->resource = 1; sfx->priority = r[0];
		if (!p.startSfx(sfx, r)) { fprintf(stderr, "start failed\n"); return 4; }
	}
	for (int cb = 0; cb < callbacks; cb++) { g_cb = cb; p.onTimer(); }
	fclose(g_log); return 0;
}
