
#define DOWN_IN 2  // Fully DOWN detection
#define UP_IN 3    // Fully UP detection

#define UP_OUT 22    // Lift up control
#define DOWN_OUT 26  // Lift down control
#define POWER 24     // A site 220V power

// 5 general-purpose relay outputs, controlled from the PC (output word bits 3..7)
#define RELAY1 28
#define RELAY2 30
#define RELAY3 32
#define RELAY4 34
#define RELAY5 36
const uint8_t RELAY_PINS[5] = { RELAY1, RELAY2, RELAY3, RELAY4, RELAY5 };

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
}

uint8_t systemRespoenseInput[10] = { 0x44, 0x45, 0x06, 0x49, 0x00, 0xE8, 0x00, 0x00, 0x52, 0x56 };
uint8_t systemRespoenseOutput[10] = { 0x44, 0x45, 0x4F, 0x00, 0x52, 0x56 };
uint8_t LastStartState = false;

void CollectInput() {
  uint8_t startSignal = digitalRead(DOWN_IN);

  // Serial.println(startSignal);

  if (startSignal != LastStartState) {
    delay(50);
    startSignal = digitalRead(DOWN_IN);
    if (startSignal != LastStartState) {
      if (LastStartState) {
        digitalWrite(POWER, LOW);
      }
      LastStartState = startSignal;
      bitWrite(systemRespoenseInput[5], 1, startSignal);
      unsigned char xorTemp = systemRespoenseInput[0];
      for (int i = 1; i < 8; i++) {
        xorTemp ^= systemRespoenseInput[i];
      }
      systemRespoenseInput[8] = xorTemp;

      Serial.write(systemRespoenseInput, 10);
    }
  }
}


void ResponseInput() {
  uint8_t downFlag = digitalRead(DOWN_IN);
  uint8_t upFlag = digitalRead(UP_IN);

  bitWrite(systemRespoenseInput[5], 0, upFlag);
  bitWrite(systemRespoenseInput[5], 1, downFlag);
  unsigned char xorTemp = systemRespoenseInput[0];
  for (int i = 1; i < 8; i++) {
    xorTemp ^= systemRespoenseInput[i];
  }
  systemRespoenseInput[8] = xorTemp;
  Serial.write(systemRespoenseInput, 10);
}

void SetSystemOutput(uint8_t data[4]) {
  uint32_t data32 = 0x00000000;
  // 00000100

  for (int index = 0; index < 4; index++) {
    data32 = data32 << 8 | data[index];
  }
    uint8_t down = digitalRead(DOWN_IN);

    if(!down){
        digitalWrite(POWER, LOW);

    }else{
        digitalWrite(POWER, bitRead(data32, 0));

    }

  digitalWrite(UP_OUT, bitRead(data32, 1));
  digitalWrite(DOWN_OUT, bitRead(data32, 2));

  // bits 3..7 = RELAY1..RELAY5
  for (int i = 0; i < 5; i++) {
    digitalWrite(RELAY_PINS[i], bitRead(data32, 3 + i));
  }
}