#define DOWN_IN 2  // Fully DOWN detection
#define UP_IN 3    // Fully UP detection (reserved, not fitted)

#define UP_OUT 22    // Lift up control
#define DOWN_OUT 26  // Lift down control
#define POWER 24     // A site 220V power

// 5 general-purpose relay outputs (output keys 3..7)
#define RELAY1 28
#define RELAY2 30
#define RELAY3 32
#define RELAY4 34
#define RELAY5 36
const uint8_t RELAY_PINS[5] = { RELAY1, RELAY2, RELAY3, RELAY4, RELAY5 };

// ---- 5-byte key/value protocol: [cmd][key][value][chk][FRAME_END] in both directions ----
//      chk = cmd XOR key XOR value. A frame with a wrong chk or a wrong terminator is dropped.
#define FRAME_LEN 5
#define FRAME_END 0x11
#define CMD_SET 0x52    // PC -> board: set one output   key = output 0..7, value = 0/1. Board echoes the frame.
#define CMD_GET 0x49    // PC -> board: read inputs      key = 0 all / 1 UP / 2 DOWN. Board answers one frame per input.
#define CMD_RESET 0xD5  // PC -> board: every output OFF. Board echoes.
                        // board -> PC event: [CMD_GET][2][downLevel][chk][FRAME_END] every time the DOWN sensor changes.

#define KEY_POWER 0
#define KEY_UP 1
#define KEY_DOWN 2
#define KEY_RELAY1 3

#define IN_UP 1
#define IN_DOWN 2

uint8_t LastStartState = false;

uint8_t FrameChecksum(uint8_t cmd, uint8_t key, uint8_t value) {
  return cmd ^ key ^ value;
}

void SendFrame(uint8_t cmd, uint8_t key, uint8_t value) {
  uint8_t f[FRAME_LEN] = { cmd, key, value, FrameChecksum(cmd, key, value), FRAME_END };
  Serial.write(f, FRAME_LEN);
}

void SetSystemIOPinMode() {
  Serial.begin(9600);
  pinMode(DOWN_IN, INPUT_PULLUP);
  pinMode(UP_IN, INPUT_PULLUP);
  pinMode(POWER, OUTPUT);
  pinMode(UP_OUT, OUTPUT);
  pinMode(DOWN_OUT, OUTPUT);

  digitalWrite(POWER, LOW);
  digitalWrite(UP_OUT, LOW);
  digitalWrite(DOWN_OUT, LOW);

  for (int i = 0; i < 5; i++) {
    pinMode(RELAY_PINS[i], OUTPUT);
    digitalWrite(RELAY_PINS[i], LOW);
  }

  LastStartState = digitalRead(DOWN_IN);
}

// DOWN sensor edge (50 ms debounce) -> cut POWER when the cylinder leaves the bottom, then report the new level
void CollectInput() {
  uint8_t startSignal = digitalRead(DOWN_IN);

  if (startSignal != LastStartState) {
    delay(50);
    startSignal = digitalRead(DOWN_IN);
    if (startSignal != LastStartState) {
      if (LastStartState) {
        digitalWrite(POWER, LOW);
      }
      LastStartState = startSignal;
      SendFrame(CMD_GET, IN_DOWN, startSignal);
    }
  }
}

// Answer a GET: key 0 = every input (one frame each), 1 = UP only, 2 = DOWN only
void ReportInputs(uint8_t key) {
  if (key == 0 || key == IN_UP) SendFrame(CMD_GET, IN_UP, digitalRead(UP_IN));
  if (key == 0 || key == IN_DOWN) SendFrame(CMD_GET, IN_DOWN, digitalRead(DOWN_IN));
}

// Set ONE output. key: 0 POWER (interlocked: OFF unless DOWN sensor active), 1 UP_OUT, 2 DOWN_OUT, 3..7 RELAY1..5
void SetOutput(uint8_t key, uint8_t value) {
  uint8_t level = value ? HIGH : LOW;
  switch (key) {
    case KEY_POWER:
      if (!digitalRead(DOWN_IN)) level = LOW;
      digitalWrite(POWER, level);
      break;
    case KEY_UP: digitalWrite(UP_OUT, level); break;
    case KEY_DOWN: digitalWrite(DOWN_OUT, level); break;
    default:
      if (key >= KEY_RELAY1 && key < KEY_RELAY1 + 5) digitalWrite(RELAY_PINS[key - KEY_RELAY1], level);
      break;
  }
}

void AllOutputsOff() {
  digitalWrite(POWER, LOW);
  digitalWrite(UP_OUT, LOW);
  digitalWrite(DOWN_OUT, LOW);
  for (int i = 0; i < 5; i++) digitalWrite(RELAY_PINS[i], LOW);
}
