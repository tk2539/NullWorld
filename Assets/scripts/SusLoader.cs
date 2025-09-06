using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// SUS を読み込んで、あなたのゲームの NoteData 相当（lane/width/time/type）に変換する軽量ローダ
/// ・time は「四分音符=1」の拍単位（いまの実装に合わせる）
/// ・デフォは単ノーツ（#MMM12..1C）を normal にする
/// ・lane マッピングは Inspector で編集可能
/// </summary>
[CreateAssetMenu(fileName = "SusLoader", menuName = "Charts/SusLoader")]
public class SusLoader : ScriptableObject
{
    [Serializable]
    public class ChannelLane
    {
        [Tooltip("SUS Channel (hex 2桁, 例: 12, 1A, 1C)")] public string channel = "12";
        [Tooltip("ゲーム内 lane (1..12)")]            public int lane = 1;
        [Tooltip("このチャンネルの既定 width")]        public int width = 3;
        [Tooltip("type (normal / flick / critical)")]   public string type = "normal";
    }

    [Header("Lane map (編集可)")]
    public List<ChannelLane> laneMap = new List<ChannelLane>()
    {
        new ChannelLane{ channel="12", lane=1,  width=3, type="normal"},
        new ChannelLane{ channel="13", lane=2,  width=3, type="normal"},
        new ChannelLane{ channel="14", lane=3,  width=3, type="normal"},
        new ChannelLane{ channel="15", lane=4,  width=3, type="normal"},
        new ChannelLane{ channel="16", lane=5,  width=3, type="normal"},
        new ChannelLane{ channel="17", lane=6,  width=3, type="normal"},
        new ChannelLane{ channel="18", lane=7,  width=3, type="normal"},
        new ChannelLane{ channel="19", lane=8,  width=3, type="normal"},
        new ChannelLane{ channel="1A", lane=9,  width=3, type="normal"},
        new ChannelLane{ channel="1B", lane=10, width=3, type="normal"},
        new ChannelLane{ channel="1C", lane=11, width=3, type="normal"},
        // 必要なら 1D などを追加して 12レーンに合わせてもOK
    };

    [Header("Parsing")]
    [Tooltip("4/4想定。小節あたりの拍数（四分音符=1）")] public float beatsPerMeasure = 4f;
    [Tooltip("デフォ BPM（#BPMxx: が無い時）")]           public float defaultBpm = 120f;

    // 内部: channel→設定の辞書
    Dictionary<string, ChannelLane> _map;

    void BuildMap()
    {
        _map = new Dictionary<string, ChannelLane>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in laneMap)
        {
            if (string.IsNullOrEmpty(e.channel)) continue;
            var key = e.channel.Trim();
            _map[key] = e; // 後勝ち
        }
    }

    // あなたのゲームの NoteData に相当（必要なら自分の型へ差し替え）
    [Serializable]
    public class NoteData
    {
        public int lane;
        public int width;
        public float time;   // 四分=1
        public string type;  // normal / flick / critical / longstart 等
        public bool fake;
    }

    // 主要API: SUSファイルから NoteData の配列を得る
    public List<NoteData> LoadFromSusText(string susText, out float bpm)
    {
        if (_map == null) BuildMap();
        bpm = ParseBpm(susText, defaultBpm);

        // #MMMCC:DATA を拾う
        var events = new List<(int meas, string ch, string data)>();
        foreach (var line in susText.Split('\n'))
        {
            var s = line.Trim();
            var m = Regex.Match(s, @"^#(\d{3})([0-9A-Fa-f]{2}):([0-9A-Za-z]+)$");
            if (!m.Success) continue;
            int meas = int.Parse(m.Groups[1].Value);
            string ch = m.Groups[2].Value.ToUpperInvariant();
            string data = m.Groups[3].Value;
            events.Add((meas, ch, data));
        }

        var notes = new List<NoteData>();

        foreach (var (meas, ch, data) in events)
        {
            if (!_map.ContainsKey(ch)) continue; // 未対応チャンネルは無視
            var cfg = _map[ch];

            int L = data.Length;
            for (int i = 0; i < L; i++)
            {
                char sym = data[i];
                if (sym == '0') continue;

                // このグリッド位置の beat を計算（四分=1）
                float frac = (float)i / L;
                float beat = meas * beatsPerMeasure + frac * beatsPerMeasure;

                notes.Add(new NoteData {
                    lane  = Mathf.Clamp(cfg.lane, 1, 12),
                    width = Mathf.Clamp(cfg.width, 1, 12),
                    time  = beat,
                    type  = string.IsNullOrEmpty(cfg.type) ? "normal" : cfg.type,
                    fake  = false
                });
            }
        }

        // 時間順
        notes.Sort((a,b)=> a.time.CompareTo(b.time));
        return notes;
    }

    float ParseBpm(string susText, float fallback)
    {
        // #BPMxx: 123.45 を優先。なければ #BPM: も見る
        foreach (var line in susText.Split('\n'))
        {
            var s = line.Trim();
            var m = Regex.Match(s, @"^#BPM[0-9A-Fa-f]{2}:\s*([0-9.]+)$");
            if (m.Success && float.TryParse(m.Groups[1].Value, out var v)) return v;
            var m2 = Regex.Match(s, @"^#BPM:\s*([0-9.]+)$");
            if (m2.Success && float.TryParse(m2.Groups[1].Value, out var v2)) return v2;
        }
        return fallback;
    }
}