#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class OpenChartsFolder : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Open Charts Folder")]
    static void OpenChartsDirectory()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "charts");
        Debug.Log($"Opening Finder: {path}");
        EditorUtility.RevealInFinder(path);
    }
#endif
}