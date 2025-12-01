using System;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public class ArduinoManager : MonoBehaviour
{   
    public static ArduinoManager Instance;
    
    private Settings settings;
    private SerialPort port;
    private string portName;
    private int baudRate;

    // 서보 각도 제한
    private const int ServoMinDeg = 0;
    private const int ServoMaxDeg = 180;

    // 상대 회전 기본 시간(초) - 시간 미지정 시 사용
    private const float DefaultMoveSeconds = 2.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        settings = JsonLoader.Instance?.settings;
        OpenPort(); // 시작 시 포트 열기
    }
    
    private void OnDestroy()
    {
        if (port == null) return;

        try
        {
            if (port.IsOpen) port.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ArduinoManager] OnDestroy-> Close failed: {e}");
        }

        port = null;
    }
    
    /// <summary>
    /// 어플리케이션 종료(에디터 Stop, 빌드 Quit, Alt+F4 등) 시 호출.
    /// 포트가 닫히기 전에 Home 명령을 전송.
    /// </summary>
    private void OnApplicationQuit()
    {
        if (port != null && port.IsOpen)
        {
            Debug.Log("[ArduinoManager] Application Quitting -> Moving to HOME");
            
            ExcuteCommand("home");

            // 데이터가 시리얼 버퍼에서 전송될 수 있도록 아주 짧게 대기 (안전장치)
            try 
            { 
                Thread.Sleep(50); 
            } 
            catch { }
        }
    }

    // 포트를 여는 함수
    private void OpenPort()
    {
        if (port != null && port.IsOpen) return;
        if (settings == null) return;
        
        portName = settings.comPort;
        baudRate = settings.baudRate;
        
        port = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 50,
            WriteTimeout = 50,
            DtrEnable = true,
            RtsEnable = true
        };

        try
        {
            port.Open();
            Thread.Sleep(2000); // 보드 리셋 대기
            Debug.Log($"[ArduinoManager] OpenPort-> Serial opened COM: {portName}, baud: {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ArduinoManager] OpenPort-> Serial open failed: {e}");
        }
    }

    // 포트 보장 -> 필요 시 재시도
    private void EnsurePort()
    {
        if (port == null || !port.IsOpen) OpenPort();
    }

    // 한 줄 전송
    private void SendLine(string line)
    {
        EnsurePort();
        if (port == null || !port.IsOpen) return;

        try
        {
            port.WriteLine(line);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ArduinoManager] SendLine-> WriteLine failed: {e}");
        }
    }

    // "left/right/좌/우 값 [시간]" 형식 파싱
    // 예: "left 30 2.3", "우 45", "right -10 1.5"
    private bool TryParseDirAmountTime(string s, out bool isLeft, out int value, out float seconds)
    {
        isLeft = false;
        value = 0;
        seconds = DefaultMoveSeconds;

        // 그룹1: 방향, 그룹2: 정수, 그룹3: 선택적 시간(실수)
        Regex rx = new Regex(
            @"^\s*(left|right|좌|우)\s+([-+]?\d+)(?:\s+([0-9]*\.?[0-9]+))?\s*$",
            RegexOptions.IgnoreCase
        );

        Match m = rx.Match(s);
        if (!m.Success) return false;

        string dir = m.Groups[1].Value;
        string num = m.Groups[2].Value;
        string secStr = m.Groups[3].Value;

        isLeft = dir.Equals("left", StringComparison.OrdinalIgnoreCase) || dir == "좌";

        bool ok = int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        if (!ok) return false;

        if (!string.IsNullOrEmpty(secStr))
        {
            float parsedSeconds;
            if (float.TryParse(secStr, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSeconds))
            {
                if (parsedSeconds > 0.0f)
                {
                    seconds = parsedSeconds;
                }
            }
        }

        return true;
    }

    // 문자열 내 첫 정수 추출 (예: "set 120")
    private bool TryParseFirstInt(string s, out int value)
    {
        value = 0;
        Regex rx = new Regex(@"[-+]?\d+");
        Match m = rx.Match(s);
        if (!m.Success) return false;
        return int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // "left/right N [sec]", "set N", "home" 등 명령 파싱 -> 아두이노 프로토콜로 전송
    public void ExcuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        string s = command.Trim();

        // 1) 홈
        if (string.Equals(s, "home", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "홈",   StringComparison.Ordinal))
        {
            SendLine("HOME");
            return;
        }

        // 2) 절대 설정: "set 120" / "설정 120"  (즉시 지정, 시간 개념 없음)
        if (s.StartsWith("set", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("설정", StringComparison.Ordinal))
        {
            int val;
            if (TryParseFirstInt(s, out val))
            {
                int clamped = Mathf.Clamp(val, ServoMinDeg, ServoMaxDeg);
                SendLine($"SET {clamped}");
            }
            else
            {
                Debug.LogWarning($"[ArduinoManager] ExcuteCommand-> set: integer not found in '{s}'");
            }
            return;
        }

        // 3) 상대 회전: "left 90 2.3" / "right 30 1.5" / "좌 20 3"
        bool isLeft;
        int amount;
        float seconds;
        if (TryParseDirAmountTime(s, out isLeft, out amount, out seconds))
        {
            int signed = isLeft ? -Mathf.Abs(amount) : Mathf.Abs(amount);
            float clampedSeconds = Mathf.Max(0.1f, seconds); // 최소 0.1초
            int durationMs = Mathf.RoundToInt(clampedSeconds * 1000f);

            // 프로토콜: ADD <deltaDeg> <durationMs>
            SendLine($"ADD {signed} {durationMs}");
            return;
        }

        // 4) 알 수 없는 명령
        Debug.LogWarning($"[ArduinoManager] ExcuteCommand-> Unknown command: '{s}'");
    }
}
