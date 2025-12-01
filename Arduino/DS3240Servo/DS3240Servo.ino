#include <Servo.h>

// ===== 서보 파라미터 =====
static const int SERVO_PIN     = 9;
static const int SERVO_MIN_DEG = 0;
static const int SERVO_MAX_DEG = 270; // [수정] 최대 각도를 270도로 변경
static const int SERVO_MIN_US  = 500; // 270도 서보의 0도 펄스폭 (보통 500)
static const int SERVO_MAX_US  = 2500; // 270도 서보의 270도 펄스폭 (보통 2500)

// ===== 모션 파라미터 =====
static const unsigned long STEP_INTERVAL_MS = 10;      // 서보 업데이트 주기
static const unsigned long DEFAULT_MOVE_MS  = 2000UL;  // ADD에서 시간 생략 시 기본 2초

Servo g_servo;

// 현재/목표 각도 및 보간 정보
int g_currentDeg      = 135;   
int g_targetDeg       = 135;   
int g_startDeg        = 135;   

unsigned long g_lastStepMs    = 0;
unsigned long g_moveStartMs   = 0;
unsigned long g_moveDurationMs = 0;

// 직렬 수신 버퍼
String g_line;

// 각도를 펄스폭으로 변환하여 모터를 움직이는 헬퍼 함수
void writeServoAngle(int deg)
{
  // 각도(0~270)를 펄스폭(500~2500)으로 변환
  int us = map(deg, SERVO_MIN_DEG, SERVO_MAX_DEG, SERVO_MIN_US, SERVO_MAX_US);
  g_servo.writeMicroseconds(us);
}

// 함수: 목표 각도까지 지정 시간(ms) 동안 선형 보간 이동 시작
void startMoveTo(int targetDeg, unsigned long durationMs)
{
  if (targetDeg < SERVO_MIN_DEG) targetDeg = SERVO_MIN_DEG;
  if (targetDeg > SERVO_MAX_DEG) targetDeg = SERVO_MAX_DEG;

  if (durationMs == 0)
  {
    durationMs = 1; // 0 방지
  }

  g_startDeg        = g_currentDeg;
  g_targetDeg       = targetDeg;
  g_moveStartMs     = millis();
  g_moveDurationMs  = durationMs;
}

// 함수: 현재 시간 기준으로 시작각 -> 목표각까지 duration 동안 선형 보간
void stepServoToTarget()
{
  unsigned long now = millis();
  if (now - g_lastStepMs < STEP_INTERVAL_MS) return;
  g_lastStepMs = now;

  // 이동할 것이 없으면 리턴
  if (g_moveDurationMs == 0 || g_currentDeg == g_targetDeg)
  {
    return;
  }

  unsigned long elapsed = now - g_moveStartMs;

  if (elapsed >= g_moveDurationMs)
  {
    // 목표 시간 이상 경과 -> 정확히 목표각으로
    g_currentDeg      = g_targetDeg;
    g_moveDurationMs  = 0; // 이동 완료
  }
  else
  {
    float t = (float)elapsed / (float)g_moveDurationMs; // 0.0 ~ 1.0
    int newDeg = g_startDeg + (int)((float)(g_targetDeg - g_startDeg) * t);
    g_currentDeg = newDeg;
  }

  // 경계 보호
  if (g_currentDeg < SERVO_MIN_DEG) g_currentDeg = SERVO_MIN_DEG;
  if (g_currentDeg > SERVO_MAX_DEG) g_currentDeg = SERVO_MAX_DEG;

    writeServoAngle(g_currentDeg);
}

// 함수: "A B" 형태 문자열을 앞/뒤로 분리
bool split2(const String& s, String& a, String& b)
{
  int idx = s.indexOf(' ');
  if (idx < 0) return false;
  a = s.substring(0, idx);
  b = s.substring(idx + 1);
  a.trim();
  b.trim();
  return (a.length() > 0 && b.length() > 0);
}

// 함수: 명령 처리
void handleCommand(const String& line)
{
  // HOME -> 135도(중앙)로 즉시 이동 (필요에 따라 0 또는 90으로 변경 가능)
  if (line.equalsIgnoreCase("HOME"))
  {
    g_targetDeg      = 135;
    g_currentDeg     = 135;
    g_moveDurationMs = 0; // 이동 중지
    writeServoAngle(g_currentDeg); // [수정]
    return;
  }

  // GET -> 현재/목표 각도 출력
  if (line.equalsIgnoreCase("GET"))
  {
    Serial.print("CUR=");
    Serial.print(g_currentDeg);
    Serial.print(",TGT=");
    Serial.println(g_targetDeg);
    return;
  }

  String cmd, arg;
  if (!split2(line, cmd, arg)) return;

  // SET <deg> -> 즉시 해당 각도로 이동(보간 없이)
  if (cmd.equalsIgnoreCase("SET"))
  {
    long deg = arg.toInt();
    if (deg < SERVO_MIN_DEG) deg = SERVO_MIN_DEG;
    if (deg > SERVO_MAX_DEG) deg = SERVO_MAX_DEG;

    g_targetDeg      = (int)deg;
    g_currentDeg     = g_targetDeg;
    g_moveDurationMs = 0; // 보간 중지
    writeServoAngle(g_currentDeg); // [수정]
    return;
  }

  // ADD <deltaDeg> [durationMs]
  if (cmd.equalsIgnoreCase("ADD"))
  {
    String sDelta, sDur;
    long delta;
    unsigned long durMs = DEFAULT_MOVE_MS;

    if (split2(arg, sDelta, sDur))
    {
      delta = sDelta.toInt();
      long dur = sDur.toInt();
      if (dur > 0)
      {
        durMs = (unsigned long)dur;
      }
    }
    else
    {
      delta = arg.toInt();
    }

    long next = (long)g_targetDeg + delta;
    if (next < SERVO_MIN_DEG) next = SERVO_MIN_DEG;
    if (next > SERVO_MAX_DEG) next = SERVO_MAX_DEG;

    startMoveTo((int)next, durMs);
    return;
  }

  // uS <us> -> PWM 펄스폭 직접 지정(즉시 반영, 보간 중지)
  if (cmd.equalsIgnoreCase("uS"))
  {
    long us = arg.toInt();
    if (us < SERVO_MIN_US) us = SERVO_MIN_US;
    if (us > SERVO_MAX_US) us = SERVO_MAX_US;

    g_moveDurationMs = 0; // 시간 기반 이동 중지
    g_servo.writeMicroseconds((int)us);
    return;
  }
}

// 함수: CR/LF 기준 직렬 수신 라인 파서
void pumpSerial()
{
  while (Serial.available() > 0)
  {
    int c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n')
    {
      String line = g_line;
      g_line = "";
      line.trim();
      if (line.length() > 0)
      {
        handleCommand(line);
      }
    }
    else
    {
      g_line += (char)c;
    }
  }
}

void setup()
{
  Serial.begin(9600);
  g_servo.attach(SERVO_PIN, SERVO_MIN_US, SERVO_MAX_US);

  g_currentDeg      = 135;
  g_targetDeg       = 135;
  g_startDeg        = 135;
  g_moveDurationMs  = 0;

  writeServoAngle(g_currentDeg);
  g_lastStepMs = millis();
}

void loop()
{
  pumpSerial();
  stepServoToTarget();
}