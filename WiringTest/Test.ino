#define UP_OUT 22    // Lift up control
#define DOWN_OUT 26  // Lift down control
#define POWER 24     // 220V power relay
#define DOWN_IN 2    // Fully DOWN detection
#define UP_IN 3      // Fully DOWN detection

String cmd;

void setup() {
  Serial.begin(9600);
  pinMode(DOWN_IN, INPUT_PULLUP);
  pinMode(UP_IN, INPUT_PULLUP);

  pinMode(UP_OUT, OUTPUT);
  pinMode(DOWN_OUT, OUTPUT);
  pinMode(POWER, OUTPUT);

  // Tắt tất cả khi khởi động
  digitalWrite(UP_OUT, LOW);
  digitalWrite(DOWN_OUT, LOW);
  digitalWrite(POWER, LOW);

  Serial.println("READY");
}

void stopAll() {
  digitalWrite(UP_OUT, LOW);
  digitalWrite(DOWN_OUT, LOW);
}

void loop() {

  int a = digitalRead(DOWN_IN);
  Serial.println(a);

  if (Serial.available()) {
    cmd = Serial.readStringUntil('\n');
    cmd.trim();  // bỏ \r \n

    if (cmd == "UP ON") {
      // stopAll();                   // tránh UP & DOWN cùng lúc
      digitalWrite(UP_OUT, HIGH);
      Serial.println("UP ON");
    } else if (cmd == "UP OFF") {
      digitalWrite(UP_OUT, LOW);
      Serial.println("UP OFF");
    } else if (cmd == "DOWN ON") {
      // stopAll();
      digitalWrite(DOWN_OUT, HIGH);
      Serial.println("DOWN ON");
    } else if (cmd == "DOWN OFF") {
      digitalWrite(DOWN_OUT, LOW);
      Serial.println("DOWN OFF");
    } else if (cmd == "POWER ON") {
      digitalWrite(POWER, HIGH);
      Serial.println("POWER ON");
    } else if (cmd == "POWER OFF") {
      digitalWrite(POWER, LOW);
      Serial.println("POWER OFF");
    } else if (cmd == "STOP") {
      stopAll();
      Serial.println("STOP ALL");
    } else {
      Serial.print("UNKNOWN CMD: ");
      Serial.println(cmd);
    }
  }
}
