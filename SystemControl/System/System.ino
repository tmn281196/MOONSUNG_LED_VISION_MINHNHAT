/*
  LED COLOR INSPECTION - IO Box firmware (Arduino Mega 2560).

  One test station: product 220 V power relay, one pneumatic cylinder (UP / DOWN valves,
  one DOWN sensor) and five general-purpose relays. Full protocol: ../LED_IOBOX_Protocol.html

  Wire protocol (must stay in sync with LED Vision/DeviceControl.cs):
      Every frame, both directions, is 5 bytes:  [cmd][key][value][chk][0x11]
      chk = cmd XOR key XOR value.
      Baud 9600 8N1. A frame whose chk is wrong or whose 5th byte is not 0x11 is dropped
      and the parser resyncs one byte at a time.

  PC -> board
      0x52 SET    key = output 0..7, value = 0 off / 1 on. One frame changes one output.
                  0 POWER (interlocked: OFF unless DOWN sensor active), 1 UP_OUT, 2 DOWN_OUT,
                  3..7 RELAY1..RELAY5.            Reply: the same frame echoed.
      0x49 GET    key = 0 all inputs / 1 UP / 2 DOWN. Reply: one [0x49][key][level][chk][0x11] per input.
      0xD5 RESET  every output OFF.               Reply: the same frame echoed.

  board -> PC, unsolicited
      [0x49][2][level][chk][0x11]  every time the DOWN sensor changes (50 ms debounce).
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

// 5-byte frames: [cmd][key][value][chk][FRAME_END], chk = cmd ^ key ^ value.
// A frame with a wrong chk or terminator is dropped one byte at a time until the parser resyncs.
uint8_t rxBuf[FRAME_LEN];
uint8_t rxLen = 0;

void serialEvent() {
  while (Serial.available()) {
    rxBuf[rxLen++] = Serial.read();
    if (rxLen < FRAME_LEN) continue;
    rxLen = 0;

    if (rxBuf[4] != FRAME_END || rxBuf[3] != FrameChecksum(rxBuf[0], rxBuf[1], rxBuf[2])) {
      // resync: shift left by one and keep collecting
      for (uint8_t i = 0; i < FRAME_LEN - 1; i++) rxBuf[i] = rxBuf[i + 1];
      rxLen = FRAME_LEN - 1;
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
