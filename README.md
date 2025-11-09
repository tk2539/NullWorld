# NullWorld

## Rhythm game with custom charts

### About

NullWorldはUnityで開発された好きな曲で自分の作った譜面で遊べるリズムゲームです。

フォーマットに沿って作成された **譜面・音源・ジャケット画像をひとつの ZIP にまとめてインポート**するだけで、  
誰でも簡単にプレイ可能なステージを追加できます。

カジュアルなプレイから自作譜面のテストまで、幅広く活用できます。

### Gameplay

1. 起動後、「Import」ボタンを押して ZIP ファイルを選択します。  
2. ZIP 内の譜面データ (`chart.json`)、音源 (`song.mp3`)、ジャケット (`cover.png`) が自動で読み込まれます。  
3. 難易度を選んでプレイ！  
4. ノーツが判定ラインに重なったタイミングでキーを押します。  
   - 4レーンで右からd,f,j,kでフリックノーツ(赤いもの)は一段上のe,r,u,i
5. 結果画面でスコア・判定精度を確認できます。

### Format

ZIP ファイルは以下の構成でまとめます：

Mychart.zip
├── easy.json        # 譜面データ
├── normal.json
├── hard.json
├── expert.json
├── master.json
├── metadata.json    # メタデータ
├── song.mp3         # 音源ファイル
└── cover.png        # ジャケット画像

譜面のJSONファイルのフォーマットは以下です：

[
  {
    "time": 4.0,
    "lane": 1,
    "width": 3,
    "type": "critical"
  },
  {
    "time": 8.0,
    "lane": 4,
    "width": 3,
    "type": "normal"
  },
  {
    "time": 12.0,
    "lane": 7,
    "width": 3,
    "type": "flick"
  },
  {
    "time": 16.0,
    "lane": 10,
    "long": 4
    "width": 3,
    "type": "long"
  }
]

time:ノーツがくる時間です
lane:1~12でノーツの左端のレーンです
long:ロングノーツ特有でノーツの長さです
width:ノーツの幅です
type:normal,critical,flick,longの4つでノーツの種類です

metadataの書き方:

{
  "title": "songname",
  "bpm": 120.0,
  "speedScaleMarkers": [
    {
      "beat": 0.0,
      "forward": 1.0,
      "reverse": 0.16666666666666666
    }
  ],
  "rootoffset": 1200,
  "rootoffsetMs": 1200
}

title:曲名です
bpm:その曲のBPMです
speedScaleMarkers:いわゆるソフランです
rootoffset:曲と譜面両方に適用されるオフセットです
rootoffsetMs:上に同じです

### 譜面作成支援

このゲームは **MikuMikuWorld for ChartCyanvas (MMW4CC)** の譜面フォーマットに対応しています。  
MMW4CCで作成した `.usc` ファイルを、付属の `mmw4cc_to_mygame.py` を使って  
本ゲーム用の `.chart.json` 形式に変換できます。

👉 [MikuMikuWorld for ChartCyanvas](https://github.com/sevenc-nanashi/MikuMikuWorld4CC)
+
+> MMW4CC は MIT License に基づくオープンソースプロジェクトです。本ゲームは MMW4CC のソースコードを直接含まず、  
+> 互換フォーマット変換スクリプトを利用しています。