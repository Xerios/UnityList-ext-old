using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Threading;
using System.Collections;
using WaitForSeconds = FreeEditorCoroutines.WaitForSeconds;
using System.IO;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

public class UnityListWindow: ScriptableObject {
    static string url = "http://localhost:5400/project/unity-roll-a-ball-2TbC";
    static BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    static Type webViewEditorWindowType, webViewType, webScriptType;
    static object webWindow, webView;
    static ScriptableObject scriptObject;
    static float m_time = 0.0f;
    static UnityListWindow unityListWindow;

    public static Action<object, string> callback;

    [MenuItem("Tools/UnityList &a")]
    static void Open() {
        var editor = typeof(Editor).Assembly;

        webWindow = webView = null;

        if (unityListWindow == null) {
            unityListWindow = new UnityListWindow();
            unityListWindow.hideFlags = HideFlags.HideAndDontSave;
        }

        webViewType = editor.GetType("UnityEditor.WebView");
        webScriptType = editor.GetType("UnityEditor.Web.WebScriptObject");
        
        var cbMethod = editor.GetType("UnityEditor.WebViewV8CallbackCSharp").GetMethod("Callback");
        callback = (inst, message) => cbMethod.Invoke(inst, new object[] { message });

#if (UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0)
        webViewEditorWindowType = editor.GetType ("UnityEditor.Web.WebViewEditorWindow");
        var methodInfo = webViewEditorWindowType.GetMethod("Create", Flags).MakeGenericMethod(webViewEditorWindowType);
#elif UNITY_5_4
        webViewEditorWindowType = editor.GetType("UnityEditor.Web.WebViewEditorWindowTabs");
        var methodInfo = webViewEditorWindowType.GetMethod("Create", Flags).MakeGenericMethod(webViewEditorWindowType);
#endif
        webWindow = methodInfo.Invoke(null, new object[] {
            "UnityList",
            url,
            200, 530, 800, 600
        });
        webViewEditorWindowType.GetProperty("hideFlags").SetValue(webWindow, HideFlags.HideAndDontSave, null);

        FreeEditorCoroutines.EditorCoroutine.StartCoroutine(connect(unityListWindow));
    }

    static IEnumerator connect(UnityListWindow win) {
        //Debug.Log("Step 1");
        //Debug.Log("test.." + (webView != null ? "exists" : "not found"));
        while (webView == null) {
            webView = webViewEditorWindowType.GetProperty("webView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(webWindow, null);
            yield return new WaitForSeconds(0.5f);
        }

        webViewEditorWindowType.GetProperty("titleContent").SetValue(webWindow, new GUIContent("UnityListX",EditorGUIUtility.FindTexture("UnityEditor.InspectorWindow")), null);

        //Debug.Log("Step 2");
        //Debug.Log("test.." + (webView != null ? "exists" : "not found"));
        webViewType.GetMethod("DefineScriptObject").Invoke(webView, new object[] { "window.unityScriptObject", win });
    }

    public void download(string url, object callback) {
        Debug.LogFormat("message from js: {0}", url);

        webViewEditorWindowType.GetMethod("ShowNotification").Invoke(webWindow, new object[] { new GUIContent("message from js: " + url) });
        
        string path = EditorUtility.SaveFolderPanel("Save Project", "C:\\temp", "");
        if (path.Length != 0) {
            new CallbackWrapper(callback).Send("success");
            FreeEditorCoroutines.EditorCoroutine.StartCoroutine(downloading(url, path));
        }
    }
    static IEnumerator downloading(string url, string path) {
        /*window.unityAsync({
            className:"window.unityScriptObject", 
            funcName:"download", 
            funcArgs:[t.href],
            onSuccess:(a) => {
                //t.href=''
                t.innerHTML = 'Downloading...'
            }
        });
        console.log("DOWNLOAD CLICKED", e.target.href)*/
        WWW download = new WWW(url);
        string file = Path.GetFileNameWithoutExtension(url.Substring(0, url.Length - 19));

        EditorUtility.DisplayProgressBar(file, "Requesting...", 0f);

        while (!download.isDone) {
            if (download.progress>0.0f) EditorUtility.DisplayProgressBar(file, "Downloading...", download.progress*0.9f);
            yield return null;
        }

        EditorUtility.DisplayProgressBar(file, "Extracting...", 0.9f);

        ZipFile zf = null;
        string defaultFolder = null;
        string assetsFolder = null;
        
        try {
            using (MemoryStream stream = new MemoryStream(download.bytes)) {
                zf = new ZipFile(stream);

                foreach (ZipEntry zipEntry in zf) {
                    if (!zipEntry.IsFile) {
                        if (zipEntry.IsDirectory) {
                            if (defaultFolder == null) defaultFolder = zipEntry.Name;
                            DirectoryInfo dirName = new DirectoryInfo(zipEntry.Name);
                            if (assetsFolder == null && dirName.Name == "ProjectSettings") assetsFolder = zipEntry.Name.Substring(0, zipEntry.Name.Length - 1 - "ProjectSettings".Length);
                        }
                        if (zipEntry.IsDirectory && assetsFolder == null) assetsFolder = zipEntry.Name;
                        continue; // Ignore directories
                    }
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    //Debug.Log(Path.Combine(path,assetsFolder));

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(path, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0) Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath)) {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
        } finally {
            if (zf != null) {
                zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                zf.Close(); // Ensure we release resources
            }
        }

        EditorUtility.DisplayProgressBar(file, "Opening...", 1f);

        Debug.Log(Path.Combine(path, assetsFolder!=null ? assetsFolder : defaultFolder));

        EditorApplication.OpenProject(Path.Combine(path,assetsFolder));

        EditorUtility.ClearProgressBar();
    }
}

public class CallbackWrapper {
    public object callback;

    public CallbackWrapper(object callback) {
        this.callback = callback;
    }

    public void Send(string message) {
        UnityListWindow.callback(this.callback, message);
    }
}