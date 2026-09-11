/*
  LED COLOR INSPECTION - IO Box firmware (Arduino Mega 2560).

  One test station: product 220 V power relay, one pneumatic cylinder (UP / DOWN valves,
  one DOWN sensor) and five general-purpose relays. Full protocol: ../LED_IOBOX_Protocol.html

  Wire protocol (must stay in sync with LED Vision/DeviceControl.cs):
      Every frame, both directions, is 4 bytes:  [cmd][key][value][0x11]
      Baud 9600 8N1. A frame whose 4th byte is not 0x11 is dropped and the parser resyncs.

  PC -> board
      0x52 SET    key = output 0..7, value = 0 off / 1 on. One frame changes one output.
                  0 POWER (interlocked: OFF unless DOWN sensor active), 1 UP_OUT, 2 DOWN_OUT,
                  3..7 RELAY1..RELAY5.            Reply: the same frame echoed.
      0x49 GET    key = 0 all inputs / 1 UP / 2 DOWN. Reply: one [0x49][key][level][0x11] per input.
      0xD5 RESET  every output OFF.               Reply: the same frame echoed.

  board -> PC, unsolicited
      [0x49][2][level][0x11]  every time the DOWN sensor changes (50 ms debounce).
      level = raw pin level of the INPUT_PULLUP sensor: 1 = cylinder fully down, 0 = not down.
      The board also cuts POWER by itself the moment the DOWN sensor releases.

  Pin map:
      Input   DIO 2  DOWN_IN (INPUT_PULLUP)      DIO 3  UP_IN (reserved)
      Output  DIO 22 UP_OUT   DIO 24 POWER   DIO 26 DOWN_OUT
              DIO 28 RELAY1  DIO 30 RELAY2  DIO 32 RELAY3  DIO 34 RELAY4  DIO 36 RELAY5
*/

#include "SystemIOPin.h"

void setup() {
  SetSystemIOPinMode();
}

void loop() {
  CollectInput();
}

// 4-byte frames: [cmd][key][value][FRAME_END]. Anything that does not end in FRAME_END is dropped one byte at a time.
uint8_t rxBuf[4];
uint8_t rxLen = 0;

void serialEvent() {
  while (Serial.available()) {
    rxBuf[rxLen++] = Serial.read();
    if (rxLen < 4) continue;
    rxLen = 0;

    if (rxBuf[3] != FRAME_END) {
      // resync: shift left by one and keep collecting
      rxBuf[0] = rxBuf[1]; rxBuf[1] = rxBuf[2]; rxBuf[2] = rxBuf[3];
      rxLen = 3;
      continue;
    }

    uint8_t cmd = rxBuf[0], key = rxBuf[1], value = rxBuf[2];
    switch (cmd) {
      case CMD_SET:
        SetOutput(key, value);
        SendFrame(CMD_SET, key, value);
        break;
      case CMD_GET:
        ReportInputs(key);
        break;
      case CMD_RESET:
        AllOutputsOff();
        SendFrame(CMD_RESET, 0, 0);
        break;
      default:
        break;  // unknown command: no reply
    }
  }
}
