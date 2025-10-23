using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary> 프로젝트 전역 효과음 재생 매니저 </summary>
public sealed class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("AudioSource")]
    [Tooltip("효과음 재생에 사용할 오디오 소스. 지정하지 않으면 자동 생성.")]
    [SerializeField] private AudioSource oneShotSource;

    private readonly Dictionary<string, AudioClip> clipCache =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, SoundSetting> soundMap =
        new Dictionary<string, SoundSetting>(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource loadCts;

    // ------------------------------------------------------------
    // 생명주기
    // ------------------------------------------------------------
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f; // 2D
        }

        // Setting.json의 개별 사운드 필드 등록
        Settings s = JsonLoader.Instance?.settings;
        if (s != null)
        {
            AddSoundIfValid(s.foundSound);
            AddSoundIfValid(s.cancelSound);
            AddSoundIfValid(s.zoomSound);
        }

        // 미리 로드
        loadCts = new CancellationTokenSource();
        _ = PreloadAllAsync(loadCts.Token);
    }

    private void OnDestroy()
    {
        if (loadCts != null)
        {
            try { loadCts.Cancel(); } catch { }
            loadCts.Dispose();
            loadCts = null;
        }
    }

    // ------------------------------------------------------------
    // 퍼블릭 API
    // ------------------------------------------------------------

    /// <summary> 설정에 등록된 key로 사운드 재생 </summary>
    public async UniTaskVoid PlayByKey(string key)
    {
        await UniTask.SwitchToMainThread();
        if (string.IsNullOrEmpty(key)) return;

        if (!soundMap.TryGetValue(key, out SoundSetting ss))
        {
            Debug.LogWarning($"[SoundManager] 미등록 키: {key}");
            return;
        }

        float vol = Mathf.Clamp01(ss.volume <= 0f ? 1f : ss.volume);
        AudioClip clip = await GetOrLoadClipAsync(ss.clipPath, this.GetCancellationTokenOnDestroy());
        if (clip) oneShotSource.PlayOneShot(clip, vol);
    }

    /// <summary> 직접 경로 지정으로 사운드 재생 </summary>
    public async UniTaskVoid PlayByPath(string relativePath, float volume = 1f)
    {
        await UniTask.SwitchToMainThread();
        if (string.IsNullOrEmpty(relativePath)) return;

        float vol = Mathf.Clamp01(volume <= 0f ? 1f : volume);
        AudioClip clip = await GetOrLoadClipAsync(relativePath, this.GetCancellationTokenOnDestroy());
        if (clip) oneShotSource.PlayOneShot(clip, vol);
    }

    // 편의 함수
    public UniTaskVoid PlayFound()  => PlayByPath("Sound/발견할 때.mp3");
    public UniTaskVoid PlayCancel() => PlayByPath("Sound/취소할 때.mp3");
    public UniTaskVoid PlayZoom()   => PlayByPath("Sound/확대할 때.mp3");

    // ------------------------------------------------------------
    // 내부 유틸
    // ------------------------------------------------------------

    /// <summary> 유효한 사운드 설정만 맵에 추가 </summary>
    private void AddSoundIfValid(SoundSetting ss)
    {
        if (ss == null || string.IsNullOrEmpty(ss.key) || string.IsNullOrEmpty(ss.clipPath))
            return;

        soundMap[ss.key] = ss;
    }

    /// <summary> 모든 등록 사운드 미리 로드 </summary>
    private async UniTask PreloadAllAsync(CancellationToken token)
    {
        await UniTask.SwitchToMainThread();

        foreach (SoundSetting s in soundMap.Values)
        {
            try
            {
                await GetOrLoadClipAsync(s.clipPath, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Debug.LogWarning($"[SoundManager] 프리로드 실패: {s.key} -> {e.Message}");
            }
        }
    }

    /// <summary> 캐시 우선, 없으면 로드 </summary>
    private async UniTask<AudioClip> GetOrLoadClipAsync(string relativePath, CancellationToken token)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;

        if (clipCache.TryGetValue(relativePath, out AudioClip cached))
            return cached;

        await UniTask.SwitchToMainThread();
        AudioClip clip = await LoadClipAsync(relativePath, token);

        if (clip) clipCache.TryAdd(relativePath, clip);

        return clip;
    }

    /// <summary> 실제 파일 로드 </summary>
    private async UniTask<AudioClip> LoadClipAsync(string relativePath, CancellationToken token)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[SoundManager] 파일 없음: {fullPath}");
                return null;
            }

            string uri = new Uri(fullPath).AbsoluteUri;
            AudioType audioType = GuessAudioTypeByExtension(fullPath);

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: token);

#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    Debug.LogError($"[SoundManager] 로드 실패: {fullPath} -> {req.error}");
                    return null;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null)
                    Debug.LogError($"[SoundManager] Decode 실패: {fullPath}");

                return clip;
            }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception e) { Debug.LogError(e); return null; }
    }

    private static AudioType GuessAudioTypeByExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".mp3") return AudioType.MPEG;
        if (ext == ".wav") return AudioType.WAV;
        if (ext == ".ogg") return AudioType.OGGVORBIS;
        return AudioType.UNKNOWN;
    }
}
