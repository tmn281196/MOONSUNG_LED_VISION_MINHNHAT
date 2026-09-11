/*
  LED COLOR INSPECTION - IO Box firmware (Arduino Mega 2560).

  One test station: product 220 V power relay, one pneumatic cylinder (UP / DOWN valves,
  one DOWN sensor) and five general-purpose relays. Full protocol: ../LED_IOBOX_Protocol.html

  Wire protocol (must stay in sync with LED Vision/DeviceControl.cs):
      TX  PC -> board : 10 bytes [44 45 06 cmd d0 d1 d2 d3 xor 56]   xor = XOR of bytes 0..7
      RX  board -> PC : ack 6 bytes [44 45 4F 00 52 56]
                        input 10 bytes [44 45 06 49 00 in 00 00 xor 56]
                          in bit0 = UP sensor raw level, bit1 = DOWN sensor raw level (1 = fully down)
      Baud 9600 8N1.

  Commands (byte 3):
      0x52  Set ONE output   d0 = index 0..7, d1 = 0 off / 1 on. One frame changes one output.
                             0 POWER (interlocked: OFF unless DOWN sensor active), 1 UP_OUT, 2 DOWN_OUT,
                             3..7 RELAY1..RELAY5
      0x4F  Set ALL (legacy) d3 bit0 POWER, bit1 UP_OUT, bit2 DOWN_OUT. Does not touch the relays.
      0x49 at byte 2         request input -> board answers with the input frame

  Events (board -> PC, unsolicited): input frame every time the DOWN sensor changes (50 ms debounce).
  Interlock: POWER is cut by the board itself the moment the DOWN sensor releases.

  Pin map:
      Input   DIO 2  DOWN_IN (INPUT_PULLUP)      DIO 3  UP_IN (reserved)
      Output  DIO 22 UP_OUT   DIO 24 POWER   DIO 26 DOWN_OUT
              DIO 28 RELAY1  DIO 30 RELAY2  DIO 32 RELAY3  DIO 34 RELAY4  DIO 36 RELAY5
*/

#include "SystemIOPin.h"

uint8_t responseBytes[] = { 0x44, 0x45, 0x53, 0x00, 0x52, 0x56 };

void setup() {
  Serial.begin(9600);
  SetSystemIOPinMode();
}

void loop() {
  // collect system input and update
  CollectInput();
}

// runs whenever there's serial buffer
// System control event
void serialEvent() {
  uint8_t bytesData[20];
  if (Serial.available()) {
    delay(10);
    uint8_t readdedbyte = Serial.read();
    if (readdedbyte == 0x44) {
      int index = 0;
      bytesData[index] = readdedbyte;
      while (Serial.available()) {
        readdedbyte = Serial.read();
        index++;
        bytesData[index] = readdedbyte;
        if (index >= 9) {
          // check the command
          if (bytesData[3] == 0x4F) {
            // legacy: all outputs in one word
            uint8_t bytes[4] = { bytesData[4], bytesData[5], bytesData[6], bytesData[7] };
            SetSystemOutput(bytes);
            Serial.write(systemRespoenseOutput, 6);
          } else if (bytesData[3] == 0x52) {
            // one output per frame: [index][state]
            SetOutput(bytesData[4], bytesData[5]);
            Serial.write(systemRespoenseOutput, 6);
          }
          break;
        }
        if (index == 2 && bytesData[2] == 0x49) {
          ResponseInput();
        }
      }
    }
    while (Serial.available()) {
      readdedbyte = Serial.read();
    }
  }
}