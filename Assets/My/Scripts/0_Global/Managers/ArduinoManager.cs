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
    // Max는 180이 최대. 360으로 늘려도 더 안돌아감
    private const int ServoMinDeg = 0;
    private const int ServoMaxDeg = 180;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if  (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        settings = JsonLoader.Instance?.settings;
        OpenPort(); // 시작 시 포트 열기
    }
    
    private void OnDestroy()
    {
        if (port == null) return;
        try { if (port.IsOpen) port.Close(); } catch { }
        port = null;
    }

    private void Update()
    {
        // 1번 키 -> 좌로 30도 이동
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ExcuteCommand("left 30");
        }
        // 2번 키 -> 우로 30도 이동
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ExcuteCommand("right 30");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ExcuteCommand("home");
        }
    }

    private void OpenPort()
    {
        if (port != null && port.IsOpen) return;
        if (settings == null) return;
        
        portName = settings.comPort;
        baudRate = settings.baudRate;
        
        port = new SerialPort(portName, baudRate);
        port.NewLine = "\n";
        port.ReadTimeout = 50;
        port.WriteTimeout = 50;
        port.DtrEnable = true;
        port.RtsEnable = true;

        try
        {
            port.Open();
            Thread.Sleep(2000); // 보드 리셋 대기
            Debug.Log($"[ArduinoManager] Serial opened COM: {portName}, baud: {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ArduinoManager] Serial open failed: {e}]");
        }
    }

    // 함수: 포트 보장 -> 필요 시 재시도
    private void EnsurePort()
    {
        if (port == null || !port.IsOpen) OpenPort();
    }

    // 함수: 한 줄 전송
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
            Debug.LogWarning($"[ArduinoManager] WriteLine failed: {e}");
        }
    }

    // 함수: "left/right N", "set N", "home" 등 명령 파싱 -> 아두이노 프로토콜로 전송
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

        // 2) 절대 설정: "set 120" / "설정 120"
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
                Debug.LogWarning($"[ArduinoManager] set: integer not found in '{s}'");
            }
            return;
        }

        // 3) 상대 회전: "left 90" / "right 30" (한국어도 허용: "좌 90", "우 30")
        bool isLeft;
        int amount;
        if (TryParseDirAndInt(s, out isLeft, out amount))
        {
            int signed = isLeft ? -Mathf.Abs(amount) : Mathf.Abs(amount);
            SendLine($"ADD {-signed}");
            return;
        }

        // 4) 알 수 없는 명령
        Debug.LogWarning($"[ArduinoManager] Unknown command: '{s}'");
    }

    // 함수: "left/right/좌/우" + 정수 추출
    private bool TryParseDirAndInt(string s, out bool isLeft, out int value)
    {
        isLeft = false;
        value = 0;

        // ^\s*(left|right|좌|우)\s*([-+]?\d+)\s*$
        Regex rx = new Regex(@"^\s*(left|right|좌|우)\s*([-+]?\d+)\s*$", RegexOptions.IgnoreCase);
        Match m = rx.Match(s);
        if (!m.Success) return false;

        string dir = m.Groups[1].Value;
        string num = m.Groups[2].Value;

        isLeft = dir.Equals("left", StringComparison.OrdinalIgnoreCase) || dir == "좌";

        bool ok = int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return ok;
    }

    // 함수: 문자열 내 첫 정수 추출 (예: "set 120")
    private bool TryParseFirstInt(string s, out int value)
    {
        value = 0;
        Regex rx = new Regex(@"[-+]?\d+");
        Match m = rx.Match(s);
        if (!m.Success) return false;
        return int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
