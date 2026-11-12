using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace RealityDefender.Editor
{
    public static class ShaderGraphEvidence
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static double frameAt;

        [MenuItem("Reality Defender/Open Shader Graph for Evidence")]
        public static void Open()
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(EnergyShaderBuilder.GraphPath));
            frameAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update -= Frame;
            EditorApplication.update += Frame;
        }

        static void Frame()
        {
            if (EditorApplication.timeSinceStartup < frameAt) return;
            EditorApplication.update -= Frame;
            var window = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => w.GetType().Name == "MaterialGraphEditWindow");
            if (!window) { Debug.LogError("Shader Graph editor did not open."); return; }
            window.maximized = true;
            window.Focus();
            var view = window.GetType().GetProperty("graphEditorView", Flags)?.GetValue(window);
            var graph = view?.GetType().GetProperty("graphView", Flags)?.GetValue(view) as GraphView;
            graph?.FrameAll();
            window.Repaint();
            Debug.Log("REALITY_DEFENDER_SHADER_GRAPH_OPEN");
        }
    }
}
