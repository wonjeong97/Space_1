#include <Servo.h>

// 이동 최대 각도는 0 ~ 180 이라고 함. 360으로 늘려봤는데 원형으로 안돌아가더라..

// ===== 서보 파라미터 =====
static const int SERVO_PIN = 9;
static const int SERVO_MIN_DEG = 0;
static const int SERVO_MAX_DEG = 180;
static const int SERVO_MIN_US  = 1000;
static const int SERVO_MAX_US  = 2000;

// ===== 모션 파라미터 =====
static const int SLEW_DEG_PER_STEP = 2;
static const unsigned long STEP_INTERVAL_MS = 10;

Servo g_servo;
int g_targetDeg = 90;    
int g_currentDeg = 90;
unsigned long g_lastStepMs = 0;

// ===== 직렬 수신 버퍼 =====
String g_line;

// 현재 각도를 목표 각도로 서서히 근접
void stepServoToTarget()
{
  unsigned long now = millis();
  if (now - g_lastStepMs < STEP_INTERVAL_MS) return;
  g_lastStepMs = now;

  if (g_currentDeg == g_targetDeg) return;

  int diff = g_targetDeg - g_currentDeg;
  int step = (diff > 0) ? SLEW_DEG_PER_STEP : -SLEW_DEG_PER_STEP;

  if (abs(diff) <= abs(step)) {
    g_currentDeg = g_targetDeg;
  } else {
    g_currentDeg += step;
  }

  // 경계 보호
  if (g_currentDeg < SERVO_MIN_DEG) g_currentDeg = SERVO_MIN_DEG;
  if (g_currentDeg > SERVO_MAX_DEG) g_currentDeg = SERVO_MAX_DEG;

  g_servo.write(g_currentDeg);
}

// "A B" 형태 분리
bool split2(const String& s, String& a, String& b)
{
  int idx = s.indexOf(' ');
  if (idx < 0) return false;
  a = s.substring(0, idx);
  b = s.substring(idx + 1);
  a.trim(); b.trim();
  return (a.length() > 0 && b.length() > 0);
}

// 명령 처리
void handleCommand(const String& line)
{
  // 지원: SET <deg>, ADD <deg>, uS <us>, HOME, GET
  if (line.equalsIgnoreCase("HOME")) {
    g_targetDeg = 90;
    g_currentDeg = 90;          
    g_servo.write(g_currentDeg);
    return;
  }

  if (line.equalsIgnoreCase("GET"))  {
    Serial.print("CUR="); Serial.print(g_currentDeg);
    Serial.print(",TGT="); Serial.println(g_targetDeg);
    return;
  }

  String cmd, arg;
  if (!split2(line, cmd, arg)) return;

  if (cmd.equalsIgnoreCase("SET")) {
    long deg = arg.toInt();
    if (deg < SERVO_MIN_DEG) deg = SERVO_MIN_DEG;
    if (deg > SERVO_MAX_DEG) deg = SERVO_MAX_DEG;
    g_targetDeg = (int)deg;
    return;
  }

  if (cmd.equalsIgnoreCase("ADD")) {
    long delta = arg.toInt();
    long next = (long)g_targetDeg + delta;
    if (next < SERVO_MIN_DEG) next = SERVO_MIN_DEG;
    if (next > SERVO_MAX_DEG) next = SERVO_MAX_DEG;
    g_targetDeg = (int)next;
    return;
  }

  if (cmd.equalsIgnoreCase("uS")) {
    long us = arg.toInt();
    if (us < SERVO_MIN_US) us = SERVO_MIN_US;
    if (us > SERVO_MAX_US) us = SERVO_MAX_US;
    g_servo.writeMicroseconds((int)us);
    return;
  }
}

// CR/LF 기준 라인 파서
void pumpSerial()
{
  while (Serial.available() > 0) {
    int c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      String line = g_line; g_line = "";
      line.trim();
      if (line.length() > 0) handleCommand(line);
    } else {
      g_line += (char)c;
    }
  }
}

void setup()
{
  Serial.begin(9600);
  g_servo.attach(SERVO_PIN, SERVO_MIN_US, SERVO_MAX_US);
  g_servo.write(g_currentDeg);
  g_lastStepMs = millis();  // 초기 스텝 타이밍 초기화
}

void loop()
{
  pumpSerial();
  stepServoToTarget();
}
