using UnityEngine;
using UnityEditor;
using System.Reflection;

public class KancolleWindow: ScriptableObject {
    static BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;

    [MenuItem("Window/Kancolle")]
    static void Open() {
        var type = Types.GetType("UnityEditor.Web.WebViewEditorWindow", "UnityEditor.dll");
        var methodInfo = type.GetMethod("Create", Flags);
        methodInfo = methodInfo.MakeGenericMethod(typeof(KancolleWindow));
        methodInfo.Invoke(null, new object[]{
            "Kancolle",
            "http://www.dmm.com/netgame/social/-/gadgets/=/app_id=854854/",
            200, 530, 800, 600
        });
    }
}