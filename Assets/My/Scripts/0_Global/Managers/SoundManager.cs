using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary> 프로젝트 전역 효과음/BGM 재생 매니저 </summary>
public sealed class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("AudioSource")]
    [Tooltip("효과음 재생에 사용할 오디오 소스. 지정하지 않으면 자동 생성.")]
    [SerializeField] private AudioSource oneShotSource;

    [Tooltip("BGM 재생에 사용할 오디오 소스. 지정하지 않으면 자동 생성.")]
    [SerializeField] private AudioSource bgmSource;

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

        // 효과음용 오디오 소스
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f; // 2D
        }

        // BGM용 오디오 소스
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;          // 기본적으로 BGM은 루프
            bgmSource.spatialBlend = 0f;    // 2D
        }

        // Setting.json의 sounds 배열 등록
        Settings s = JsonLoader.Instance?.settings;
        if (s != null && s.sounds != null)
        {
            foreach (SoundSetting ss in s.sounds)
            {
                AddSoundIfValid(ss);
            }
        }

        // 미리 로드
        loadCts = new CancellationTokenSource();
        _ = PreloadAllAsync(loadCts.Token);
    }

    private void OnDestroy()
    {
        if (loadCts != null)
        {
            try
            {
                loadCts.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SoundManager] OnDestroy-> loadCts.Cancel 예외: {e.Message}");
            }

            loadCts.Dispose();
            loadCts = null;
        }
    }

    // ------------------------------------------------------------
    // 퍼블릭 API (효과음)
    // ------------------------------------------------------------

    /// <summary> 설정에 등록된 key로 사운드 재생 (효과음용) </summary>
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

    /// <summary> 직접 경로 지정으로 사운드 재생 (효과음용) </summary>
    public async UniTaskVoid PlayByPath(string relativePath, float volume = 1f)
    {
        await UniTask.SwitchToMainThread();
        if (string.IsNullOrEmpty(relativePath)) return;

        float vol = Mathf.Clamp01(volume <= 0f ? 1f : volume);
        AudioClip clip = await GetOrLoadClipAsync(relativePath, this.GetCancellationTokenOnDestroy());
        if (clip) oneShotSource.PlayOneShot(clip, vol);
    }

    // 편의 함수 (Settings.sounds의 key 기준)
    public UniTaskVoid PlayFound()  => PlayByKey("found");
    public UniTaskVoid PlayCancel() => PlayByKey("cancel");
    public UniTaskVoid PlayZoom()   => PlayByKey("zoomSound");
    public UniTaskVoid PlayBGM()    => PlayBgmByKey("BGM");

    // ------------------------------------------------------------
    // 퍼블릭 API (BGM)
    // ------------------------------------------------------------

    /// <summary>
    /// BGM 전용 오디오 소스로 재생.
    /// - 기존 BGM을 멈추고 새 클립으로 교체 후 재생
    /// - relativePath는 StreamingAssets 기준 경로
    /// </summary>
    public async UniTaskVoid PlayBgmByPath(string relativePath, float volume = 1f, bool loop = true)
    {
        await UniTask.SwitchToMainThread();

        if (string.IsNullOrEmpty(relativePath)) return;
        if (bgmSource == null) return;

        float vol = Mathf.Clamp01(volume <= 0f ? 1f : volume);
        AudioClip clip = await GetOrLoadClipAsync(relativePath, this.GetCancellationTokenOnDestroy());
        if (clip == null) return;

        // 같은 클립이 이미 재생 중이면 볼륨/루프만 갱신
        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            bgmSource.volume = vol;
            bgmSource.loop = loop;
            return;
        }

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.volume = vol;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    /// <summary> Setting.json에 등록된 key로 BGM 재생 </summary>
    public async UniTaskVoid PlayBgmByKey(string key, bool loop = true)
    {
        await UniTask.SwitchToMainThread();
        if (string.IsNullOrEmpty(key)) return;

        if (!soundMap.TryGetValue(key, out SoundSetting ss))
        {
            Debug.LogWarning($"[SoundManager] BGM 미등록 키: {key}");
            return;
        }

        float vol = Mathf.Clamp01(ss.volume <= 0f ? 1f : ss.volume);
        PlayBgmByPath(ss.clipPath, vol, loop).Forget();
    }

    public void PauseBgm()
    {
        if (bgmSource == null) return;
        if (bgmSource.isPlaying) bgmSource.Pause();
    }

    public void ResumeBgm()
    {
        if (bgmSource == null) return;
        if (bgmSource.clip != null && !bgmSource.isPlaying) bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
    }

    public void ClearBgm()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    // ------------------------------------------------------------
    // 내부 유틸
    // ------------------------------------------------------------

    private void AddSoundIfValid(SoundSetting ss)
    {
        if (ss == null || string.IsNullOrEmpty(ss.key) || string.IsNullOrEmpty(ss.clipPath))
            return;

        soundMap[ss.key] = ss;
    }

    private async UniTask PreloadAllAsync(CancellationToken token)
    {
        await UniTask.SwitchToMainThread();

        foreach (SoundSetting s in soundMap.Values)
        {
            try
            {
                await GetOrLoadClipAsync(s.clipPath, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SoundManager] 프리로드 실패: {s.key} -> {e.Message}");
            }
        }
    }

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

                if (req.result != UnityWebRequest.Result.Success)
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
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return null;
        }
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
