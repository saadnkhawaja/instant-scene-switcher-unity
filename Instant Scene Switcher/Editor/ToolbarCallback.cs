using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SaadKhawaja.InstantSceneSwitcher
{
    [InitializeOnLoad]
    public static class ToolbarCallback
    {
        static Type toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        static ScriptableObject currentToolbar;

        public static Action OnToolbarGUI;

        static ToolbarCallback()
        {
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (currentToolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                if (toolbars.Length > 0)
                {
                    currentToolbar = (ScriptableObject)toolbars[0];
                    EditorApplication.update -= OnUpdate;
                    SceneView.duringSceneGui += OnGUI;
                }
            }
        }

        static void OnGUI(SceneView sceneView)
        {
            if (OnToolbarGUI != null)
            {
                Handles.BeginGUI();
                GUILayout.BeginArea(new Rect(0, 0, Screen.width, 20));
                GUILayout.BeginHorizontal();
                OnToolbarGUI();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                Handles.EndGUI();
            }
        }
    }
}
