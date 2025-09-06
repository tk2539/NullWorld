using System.IO;
using UnityEngine;

public static class RuntimeSelection
{
    public static string Folder
    {
        get
        {
            if (!PlayerPrefs.HasKey("SongFolder"))
            {
                throw new System.Exception("[RuntimeSelection] SongFolder is not set in PlayerPrefs.");
            }
            return PlayerPrefs.GetString("SongFolder");
        }
    }
    public static string Chart       => PlayerPrefs.GetString("ChartFile",  "chart.json");
    public static string ChartsRoot  => Path.Combine(Application.streamingAssetsPath, "charts");

    public static string ChartJsonPath => Path.Combine(ChartsRoot, Folder, Chart);
    public static string MetadataPath  => Path.Combine(ChartsRoot, Folder, "metadata.json");
    public static string SongPath      => Path.Combine(ChartsRoot, Folder, "song.mp3"); // wav/mp3等に合わせて拡張子は調整
}