using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンを跨いでBGMを鳴らし続ける常駐プレイヤー（シングルトン）
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("Check to mute all audio for demo builds.")]
    public bool muteAll = false;

    private AudioSource _source;
    private string _currentClipPath;
    private bool _idle = true;
    public bool IsIdle => _idle;

    private string _queuedFilePath = null;
    private bool _queuedLoop = true; // 使わないが互換のため残す
    private float _queuedStartTime = 0f;
    private float _queuedDelaySec = 0f;

    public string ResolveAudioPathFromPrefs()
    {
        string folder = PlayerPrefs.GetString("SongFolder", string.Empty);
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogWarning("[MusicPlayer] ResolveAudioPathFromPrefs: SongFolder not set.");
            return null;
        }

        string resolved = ChartPaths.ResolveAudioPath(folder);
        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogWarning($"[MusicPlayer] audio not found in folder '{folder}' (checked persistent & streaming)");
            return null;
        }
        Debug.Log($"[MusicPlayer] resolved audio: {resolved}");
        return resolved;
    }

    [System.Serializable]
    private class MetaLite {
        public float offset = 0f;
        public float rootoffsetMs = 0f;
    }

    private float ReadOffsetSecondsFromMetadata(string audioFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(audioFilePath);
            if (string.IsNullOrEmpty(dir)) return ObjCloneOffsetFallback();
            var metaPath = Path.Combine(dir, "metadata.json");
            if (!File.Exists(metaPath)) return ObjCloneOffsetFallback();
            string json = File.ReadAllText(metaPath);
            var meta = JsonUtility.FromJson<MetaLite>(json);
            if (meta != null)
            {
                if (meta.rootoffsetMs != 0f) return meta.rootoffsetMs / 1000f;
                if (meta.offset != 0f) return meta.offset;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MusicPlayer] metadata offset read failed: {e.Message}");
        }
        return ObjCloneOffsetFallback();
    }

    private float ObjCloneOffsetFallback()
    {
        try { return ObjClone.GlobalRootOffsetSec; }
        catch { return 0f; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = gameObject.GetComponent<AudioSource>();
        if (_source == null) _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false; // 全曲ループなし

        if (muteAll)
        {
            AudioListener.volume = 0f;
            _source.volume = 0f;
        }
        else
        {
            AudioListener.volume = 1f;
            _source.volume = 1f;
        }
        _source.spatialBlend = 0f;
    }

    public void QueueForGame(string filePath, bool loop = true, float startTime = 0f)
    {
        _idle = false;
        _queuedFilePath = filePath;
        _queuedLoop = loop;
        _queuedStartTime = startTime;
        _queuedDelaySec = ReadOffsetSecondsFromMetadata(filePath);
        Debug.Log($"[MusicPlayer] Queued: {filePath} (delay {_queuedDelaySec}s) for game scene");

        Stop();
        StartCoroutine(LoadAndPlayCoroutine(filePath, loop, startTime, _queuedDelaySec));
    }

    public void PlayFromFile(string filePath, bool loop = true, float startTime = 0f)
    {
        Debug.LogWarning("[MusicPlayer] PlayFromFile is obsolete. Use QueueForGame.");
        Stop();
        float delay = ReadOffsetSecondsFromMetadata(filePath);
        StartCoroutine(LoadAndPlayCoroutine(filePath, loop, startTime, delay));
    }

    private IEnumerator LoadAndPlayCoroutine(string filePath, bool loop, float startTime, float initialDelaySec = 0f)
    {
        _currentClipPath = filePath;
        string url = "file://" + filePath.Replace("\\", "/");

        AudioType type = AudioType.UNKNOWN;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".mp3") type = AudioType.MPEG;
        else if (ext == ".ogg") type = AudioType.OGGVORBIS;
        else if (ext == ".wav") type = AudioType.WAV;

        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, type))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"[MusicPlayer] Audio load error: {req.error} ({filePath})");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                Debug.LogError("[MusicPlayer] Clip is null.");
                yield break;
            }

            _source.clip = clip;
            _source.loop = false; // 常にループなし

            float delaySec = initialDelaySec > 0f ? initialDelaySec : 0f;
            float seekAdd = initialDelaySec < 0f ? -initialDelaySec : 0f;

            _source.time = Mathf.Clamp(startTime + seekAdd, 0f, clip.length - 0.001f);

            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "game");

            if (delaySec > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySec);
            }

            Debug.Log($"[MusicPlayer] Play clip with offset={initialDelaySec:F3}s (delay={delaySec:F3}, seek+={seekAdd:F3}), startTime={_source.time:F3}");
            _source.Play();
            if (SceneManager.GetActiveScene().name == "game")
                StartCoroutine(WatchSongEndCoroutine());
        }
    }

    public void Stop()
    {
        if (_source != null && _source.isPlaying)
        {
            _source.Stop();
            _idle = true;
        }
        _currentClipPath = null;
        _queuedFilePath = null;
    }

    public bool IsPlaying => _source != null && _source.isPlaying;
    public float TimeSeconds => _source != null && _source.clip != null ? _source.time : 0f;
    public void SetVolume(float v) { if (_source != null) _source.volume = muteAll ? 0f : Mathf.Clamp01(v); }
    public void Pause() { if (_source != null && _source.isPlaying) _source.Pause(); }
    public void UnPause() { if (_source != null && _source.clip != null && !_source.isPlaying) _source.UnPause(); }
    public bool IsPaused => _source != null && !_source.isPlaying && _source.clip != null;

    public void FadeOut(float duration = 0.5f)
    {
        if (_source == null) return;
        StartCoroutine(FadeCoroutine(_source.volume, 0f, duration));
    }
    public void FadeIn(float duration = 0.5f, float target = 1.0f)
    {
        if (_source == null) return;
        if (muteAll) { _source.volume = 0f; return; }
        StartCoroutine(FadeCoroutine(_source.volume, Mathf.Clamp01(target), duration));
    }
    private IEnumerator FadeCoroutine(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float v = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            _source.volume = muteAll ? 0f : v;
            yield return null;
        }
        _source.volume = to;
    }

    private IEnumerator WatchSongEndCoroutine()
    {
        while (_source != null && _source.clip != null && SceneManager.GetActiveScene().name == "game")
        {
            if (!_source.loop && _source.time >= _source.clip.length - 0.02f)
            {
                Debug.Log("[MusicPlayer] Song end -> stop & idle -> load result");
                Stop();
                yield return new WaitForSecondsRealtime(0.2f);
                SceneManager.LoadScene("result");
                yield break;
            }
            yield return null;
        }
    }
}