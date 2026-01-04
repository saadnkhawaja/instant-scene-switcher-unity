using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SaadKhawaja.InstantSceneSwitcher
{
    [InitializeOnLoad]
    public static class SceneSelectorToolbar
    {
        private static ScriptableObject _toolbar;

        private static string[] _scenes = Array.Empty<string>();
        private static string[] _sceneNames = Array.Empty<string>();

        private static string _lastActivePresetId;
        private static int _lastSceneHash;

        static SceneSelectorToolbar()
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.update -= Update;
                EditorApplication.update += Update;
            };
        }

        private static void Update()
        {
            try
            {
                if (_toolbar == null)
                {
                    var editorAssembly = typeof(Editor).Assembly;
                    var toolbarType = editorAssembly.GetType("UnityEditor.Toolbar");
                    var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                    _toolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;

                    if (_toolbar != null)
                    {
                        var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
                        var root = rootField.GetValue(_toolbar) as VisualElement;

                        var rightZone = root?.Q("ToolbarZoneRightAlign");
                        if (rightZone != null && rightZone.Q("SceneSelectorContainer") == null)
                        {
                            var parent = new VisualElement { name = "SceneSelectorContainer" };

                            parent.style.flexDirection = FlexDirection.Row;
                            parent.style.alignItems = Align.Center;
                            parent.style.justifyContent = Justify.Center;
                            parent.style.marginTop = -20;
                            parent.style.marginBottom = 0;
                            parent.style.paddingTop = 0;
                            parent.style.paddingBottom = 0;
                            parent.style.height = 22;
                            parent.style.flexGrow = 0;

                            var container = new IMGUIContainer(() =>
                            {
                                try { OnGUI(); }
                                catch {  }
                            });

                            container.style.alignSelf = Align.Center;
                            container.style.marginTop = 0;
                            container.style.marginBottom = 0;
                            container.style.paddingTop = 0;
                            container.style.paddingBottom = 0;
                            container.style.flexGrow = 0;
                            container.style.height = 22;

                            parent.Add(container);
                            rightZone.Add(parent);
                        }
                    }
                }

                RefreshFromPresetIfNeeded();
            }
            catch
            {
            }
        }

        private static void RefreshFromPresetIfNeeded()
        {
            var settings = InstantSceneSwitcherSettings.instance;
            string activeId = settings.ActivePresetId;

            var list = SceneSelectorToolbarBridge.GetActiveScenes();
            int hash = ComputeHash(list);

            if (_scenes == null || activeId != _lastActivePresetId || hash != _lastSceneHash)
            {
                _lastActivePresetId = activeId;
                _lastSceneHash = hash;

                _scenes = list?
                            .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                            .Distinct()
                            .ToArray()
                         ?? Array.Empty<string>();

                _sceneNames = _scenes.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            }

            if (_scenes == null) _scenes = Array.Empty<string>();
            if (_sceneNames == null) _sceneNames = Array.Empty<string>();
        }

        public static void RefreshFromPreset()
        {
            _lastActivePresetId = null;
            _lastSceneHash = 0;
            RefreshFromPresetIfNeeded();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static int ComputeHash(System.Collections.Generic.List<string> scenes)
        {
            if (scenes == null || scenes.Count == 0) return 0;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < scenes.Count; i++)
                    h = h * 31 + (scenes[i]?.GetHashCode() ?? 0);
                return h;
            }
        }

        private static void OnGUI()
        {
            RefreshFromPresetIfNeeded();

            if (_sceneNames == null || _sceneNames.Length == 0)
                return;

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var currentScenePath = SceneManager.GetActiveScene().path;
                var currentSceneName = Path.GetFileNameWithoutExtension(currentScenePath);
                int sceneIndex = Array.IndexOf(_sceneNames, currentSceneName);

                EditorGUILayout.Space(10);

                int newIndex = EditorGUILayout.Popup(sceneIndex, _sceneNames, GUILayout.Width(160));
                if (newIndex != sceneIndex && newIndex >= 0 && newIndex < _scenes.Length)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(_scenes[newIndex]);
                }

                var settings = InstantSceneSwitcherSettings.instance;
                bool isBuild = settings.ActivePresetId == InstantSceneSwitcherSettings.BuildPresetId;

                if (isBuild)
                {
                    bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == currentScenePath);

                    if (inBuild)
                    {
                        if (GUILayout.Button("-", GUILayout.Width(20)))
                            RemoveSceneFromBuild(currentScenePath);
                    }
                    else
                    {
                        if (GUILayout.Button("+", GUILayout.Width(20)))
                            AddSceneToBuild(currentScenePath);
                    }
                }
                else
                {
                    bool inPreset = settings.ContainsScene(settings.ActivePresetId, currentScenePath);

                    if (inPreset)
                    {
                        if (GUILayout.Button("-", GUILayout.Width(20)))
                        {
                            settings.RemoveSceneByPath(settings.ActivePresetId, currentScenePath);
                            RefreshFromPreset();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("+", GUILayout.Width(20)))
                        {
                            settings.AddScene(settings.ActivePresetId, currentScenePath);
                            RefreshFromPreset();
                        }
                    }
                }
            }
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            RefreshFromPreset();
        }

        private static void RemoveSceneFromBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToArray();
            EditorBuildSettings.scenes = scenes;
            RefreshFromPreset();
        }
    }
}
