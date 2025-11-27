#include <Servo.h>

const int SERVO_PIN = 9;

// ===== 논리 각도 범위(Unity/명령에서 쓰는 값) =====
const float VIRTUAL_MIN = -180.0f;
const float VIRTUAL_MAX =  180.0f;

// ===== 실제 서보가 사용할 물리 범위(대략 270° 사용 가정) =====
const float PHYSICAL_RANGE_DEG = 270.0f;            // 실제 쓰고 싶은 총 범위
const float PHYSICAL_HALF_DEG  = PHYSICAL_RANGE_DEG * 0.5f; // ±135°

/*
 * 펄스 폭 범위 (직접 테스트 필수)
 * 너무 끝까지 밀면 기계 스토퍼에 박히니 여유 있게 잡고 시작합니다.
 * 예: 600~2400, 이후 테스트로 조금씩 줄이거나 늘려서 최종 확정.
 */
const int SERVO_MIN_US = 400;   // 물리 -135° 근처
const int SERVO_MAX_US = 2400;  // 물리 +135° 근처

Servo g_servo;

///<Summary>논리 각도(-180~+180)를 물리 각도(-135~+135)로 변환</Summary>
float VirtualToPhysicalDeg(float virtualDeg)
{
    // 논리 각도 클램프
    if (virtualDeg < VIRTUAL_MIN) virtualDeg = VIRTUAL_MIN;
    else if (virtualDeg > VIRTUAL_MAX) virtualDeg = VIRTUAL_MAX;

    // -180 -> -135, 0 -> 0, +180 -> +135 가 되도록 스케일링
    float scale = PHYSICAL_HALF_DEG / VIRTUAL_MAX; // 135 / 180 = 0.75
    float physicalDeg = virtualDeg * scale;

    // 안전 차원에서 한 번 더 클램프
    if (physicalDeg < -PHYSICAL_HALF_DEG) physicalDeg = -PHYSICAL_HALF_DEG;
    else if (physicalDeg >  PHYSICAL_HALF_DEG) physicalDeg =  PHYSICAL_HALF_DEG;

    return physicalDeg;
}

///<Summary>물리 각도(-135~+135)를 펄스 폭(us)으로 변환</Summary>
int PhysicalDegToPulseUs(float physicalDeg)
{
    // 범위 클램프
    if (physicalDeg < -PHYSICAL_HALF_DEG) physicalDeg = -PHYSICAL_HALF_DEG;
    else if (physicalDeg >  PHYSICAL_HALF_DEG) physicalDeg =  PHYSICAL_HALF_DEG;

    // -135° -> 0, +135° -> 1 로 정규화
    float t = (physicalDeg + PHYSICAL_HALF_DEG) / PHYSICAL_RANGE_DEG; // 0~1
    long us = SERVO_MIN_US + (long)((SERVO_MAX_US - SERVO_MIN_US) * t);
    return (int)us;
}

///<Summary>논리 각도(-180~+180)로 서보 위치 지정</Summary>
void SetVirtualAngle(float virtualDeg)
{
    float physicalDeg = VirtualToPhysicalDeg(virtualDeg);
    int pulse = PhysicalDegToPulseUs(physicalDeg);
    g_servo.writeMicroseconds(pulse);
}

///<Summary>초기화: 서보 attach 및 중앙(0°)으로 이동</Summary>
void setup()
{
    g_servo.attach(SERVO_PIN);
    SetVirtualAngle(0.0f); // 논리 0° -> 물리 0°
    delay(1000);
}

///<Summary>논리 -180° ↔ +180°를 계속 왕복</Summary>
void loop()
{
    static float virtualAngle = VIRTUAL_MIN;  // 현재 논리 각도(-180에서 시작)
    static int direction = 1;                 // +1: 증가, -1: 감소

    // 현재 위치 반영
    SetVirtualAngle(virtualAngle);

    // 속도 조절
    const float stepDeg = 3.0f;   // 루프당 3°씩 이동
    const int waitMs = 20;        // 20ms 간격

    delay(waitMs);

    // 각도 갱신
    virtualAngle += direction * stepDeg;

    // 끝에서 방향 반전(-180 ↔ +180 왕복)
    if (virtualAngle >= VIRTUAL_MAX)
    {
        virtualAngle = VIRTUAL_MAX;
        direction = -1;
    }
    else if (virtualAngle <= VIRTUAL_MIN)
    {
        virtualAngle = VIRTUAL_MIN;
        direction = 1;
    }
}
