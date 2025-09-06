import os, json

songs = [
  "一龠","グッパイ宣言","シャルル","ベノム","ロウワー","曖昧劣情Lover",
  "脳内ぼっち","Catchy !?","エゴイスト","ルシファー","メズマライザー","少女A",
  "ECHO","強風オールバック","愛して愛して愛して","熱異常","Iなんです","独りんぼエンヴィー",
  "D.N.A","バグ","化けの花","ラグトレイン","25時の情熱","神っぽいな","カナデトモスソラ",
  "六兆年と一夜物語","ヒバナ -Reloaded-","アスノヨゾラ哨戒班","カゲロウデイズ","アサガオの散る頃に",
  "あいしていたのに","異種","一千光年","ヴァンパイア","ラビットホール",
  "腐れ外道とチョコレゐト","メモリア","ÅMARA(大未来電脳)","初音ミクの消失","生命性シンドロウム"
]

root = "Assets/StreamingAssets/charts"
os.makedirs(root, exist_ok=True)
for s in songs:
  d = os.path.join(root, s)
  os.makedirs(d, exist_ok=True)
  meta = {
    "title": s, "artist": "", "bpm": 160, "offset": 0.0,
    "levels": { "easy": -1, "normal": -1, "hard": -1, "expert": -1, "master": -1 }
  }
  with open(os.path.join(d,"metadata.json"),"w",encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
  # 難易度ファイル（必要なものだけ後で中身を作ればOK）
  for name in ["chart.json","easy.json","normal.json","hard.json","expert.json","master.json"]:
    p = os.path.join(d, name)
    if not os.path.exists(p):
      with open(p,"w",encoding="utf-8") as f: f.write("[]")
print("Scaffold done.")