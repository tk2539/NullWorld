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

    private AudioSource _source;
    private string _currentClipPath;

    // --- Queued play for game scene ---
    private string _queuedFilePath = null;
    private bool _queuedLoop = true;
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
        string chartsDir = Path.Combine(Application.streamingAssetsPath, "charts", folder);
        string mp3 = Path.Combine(chartsDir, "song.mp3");
        string ogg = Path.Combine(chartsDir, "song.ogg");
        Debug.Log($"[MusicPlayer] candidates: mp3={File.Exists(mp3)} {mp3} | ogg={File.Exists(ogg)} {ogg}");
        if (File.Exists(mp3)) return mp3;
        if (File.Exists(ogg)) return ogg;
        return null;
    }

    [System.Serializable]
    private class MetaLite { public float offset = 0f; }

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
        _source.loop = true;
        _source.volume = 1.0f;
        _source.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            // Nothing to do
        }
    }

    void Start()
    {
        // Nothing to do
    }
    
    private float ReadOffsetSecondsFromMetadata(string audioFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(audioFilePath);
            if (string.IsNullOrEmpty(dir)) return 0f;
            var metaPath = Path.Combine(dir, "metadata.json");
            if (!File.Exists(metaPath)) return 0f;
            string json = File.ReadAllText(metaPath);
            var meta = JsonUtility.FromJson<MetaLite>(json);
            if (meta != null) return meta.offset;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MusicPlayer] metadata offset read failed: {e.Message}");
        }
        return 0f;
    }

    /// <summary>
    /// ゲームシーン再生のためにオーディオをキューイング＆即ロード開始する
    /// </summary>
    public void QueueForGame(string filePath, bool loop = true, float startTime = 0f)
    {
        _queuedFilePath = filePath;
        _queuedLoop = loop;
        _queuedStartTime = startTime;
        _queuedDelaySec = ReadOffsetSecondsFromMetadata(filePath);
        Debug.Log($"[MusicPlayer] Queued: {filePath} (delay { _queuedDelaySec }s) for game scene");
        
        // ★ 重要な変更点：キューイングされたらすぐにロードを開始する ★
        Stop();
        StartCoroutine(LoadAndPlayCoroutine(filePath, loop, startTime, _queuedDelaySec));
    }

    public void PlayFromFile(string filePath, bool loop = true, float startTime = 0f)
    {
        // OnClickPlay() からは QueueForGame() を呼ぶので、このメソッドは廃止します
        // 旧バージョンとの互換性のため残しますが、将来的には削除しても良いです
        Debug.LogWarning("[MusicPlayer] PlayFromFile is obsolete. Use QueueForGame from SongDetailUI.");
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
            _source.loop = loop;
            // In game scene we want to detect song end, so force loop off
            if (SceneManager.GetActiveScene().name == "game")
                _source.loop = false;

            float delaySec = initialDelaySec > 0f ? initialDelaySec : 0f;
            float seekAdd  = initialDelaySec < 0f ? -initialDelaySec : 0f;

            _source.time = Mathf.Clamp(startTime + seekAdd, 0f, clip.length - 0.001f);
            
            // シーンが完全にロードされるまで再生を遅延させる
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "game");

            if (delaySec > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySec);
            }

            Debug.Log($"[MusicPlayer] Play clip with offset={initialDelaySec:F3}s (delay={delaySec:F3}, seek+={seekAdd:F3}), startTime={_source.time:F3}");
            _source.Play();
            // Start watcher that jumps to result when song finishes
            if (SceneManager.GetActiveScene().name == "game")
                StartCoroutine(WatchSongEndCoroutine());
        }
    }

    public void Stop()
    {
        if (_source != null && _source.isPlaying)
        {
            _source.Stop();
        }
        _currentClipPath = null;
    }

    public bool IsPlaying => _source != null && _source.isPlaying;
    public float TimeSeconds => _source != null && _source.clip != null ? _source.time : 0f;
    public void SetVolume(float v) { if (_source != null) _source.volume = Mathf.Clamp01(v); }
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
        StartCoroutine(FadeCoroutine(_source.volume, Mathf.Clamp01(target), duration));
    }
    private System.Collections.IEnumerator FadeCoroutine(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float v = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            _source.volume = v;
            yield return null;
        }
        _source.volume = to;
    }

    private IEnumerator WatchSongEndCoroutine()
    {
        // Poll until the clip reaches end, then go to result scene.
        while (_source != null && _source.clip != null && SceneManager.GetActiveScene().name == "game")
        {
            if (!_source.loop && _source.time >= _source.clip.length - 0.02f)
            {
                Debug.Log("[MusicPlayer] Song end -> load result");
                yield return new WaitForSecondsRealtime(0.2f);
                SceneManager.LoadScene("result");
                yield break;
            }
            yield return null;
        }
    }
}