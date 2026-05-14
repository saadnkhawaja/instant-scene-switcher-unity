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
        private static bool _injected;
        private static bool _treeDumped;
        private static bool _onGuiLogged;

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
                if (!_injected)
                {
                    var editorAssembly = typeof(Editor).Assembly;
                    var toolbarType = editorAssembly.GetType("UnityEditor.Toolbar");
                    if (toolbarType == null)
                    {
                        if (!_treeDumped) { _treeDumped = true; Debug.LogWarning("[InstantSceneSwitcher] UnityEditor.Toolbar type not found."); }
                        return;
                    }

                    var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                    if (toolbars.Length == 0) return;

                    var toolbarObj = toolbars[0];
                    var root = GetToolbarRoot(toolbarType, toolbarObj);

                    if (root == null)
                    {
                        if (!_treeDumped) { _treeDumped = true; Debug.LogWarning("[InstantSceneSwitcher] Could not get toolbar root VisualElement."); }
                        return;
                    }

                    if (!_treeDumped)
                    {
                        _treeDumped = true;
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("[InstantSceneSwitcher] Toolbar element tree:");
                        DumpElements(root, sb, 0);
                        Debug.Log(sb.ToString());
                    }

                    var rightZone = root.Q("ToolbarZoneRightAlign")
                                 ?? root.Q("ToolbarZoneRight")
                                 ?? root.Q("unity-right-toolbar-zone");

                    if (rightZone == null)
                    {
                        Debug.LogWarning("[InstantSceneSwitcher] Could not find right toolbar zone. Check the element tree log above for the correct name.");
                        return;
                    }

                    if (rightZone.Q("SceneSelectorContainer") == null)
                    {
                        var parent = new VisualElement { name = "SceneSelectorContainer" };

                        parent.style.flexDirection = FlexDirection.Row;
                        parent.style.alignItems = Align.Center;
                        parent.style.justifyContent = Justify.Center;
                        parent.style.marginTop = 0;
                        parent.style.marginBottom = 0;
                        parent.style.paddingTop = 0;
                        parent.style.paddingBottom = 0;
                        parent.style.flexGrow = 0;

                        var container = new IMGUIContainer(() =>
                        {
                            try { OnGUI(); }
                            catch { }
                        });

                        container.style.alignSelf = Align.Center;
                        container.style.marginTop = 0;
                        container.style.marginBottom = 0;
                        container.style.paddingTop = 0;
                        container.style.paddingBottom = 0;
                        container.style.flexGrow = 0;
                        container.style.width = 170;
                        container.style.height = 22;

                        parent.Add(container);
                        rightZone.Insert(0, parent);
                        _injected = true;
                        Debug.Log("[InstantSceneSwitcher] Toolbar injected successfully.");
                    }
                }

                RefreshFromPresetIfNeeded();
            }
            catch (Exception ex)
            {
                if (!_injected)
                    Debug.LogError($"[InstantSceneSwitcher] Injection error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static VisualElement GetToolbarRoot(Type toolbarType, UnityEngine.Object toolbarObj)
        {
            // Primary: m_Root field (Unity 2021.3 – 2023.x and most Unity 6.x builds)
            var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootField != null)
            {
                var root = rootField.GetValue(toolbarObj) as VisualElement;
                if (root != null) return root;
            }

            // Fallback: windowBackend.visualTree (Unity 6.3+ if m_Root was removed)
            var guiViewType = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
            if (guiViewType != null)
            {
                var backendProp = guiViewType.GetProperty("windowBackend",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backendProp != null)
                {
                    var backend = backendProp.GetValue(toolbarObj);
                    if (backend != null)
                    {
                        var vtProp = backend.GetType().GetProperty("visualTree",
                            BindingFlags.Public | BindingFlags.Instance);
                        var root = vtProp?.GetValue(backend) as VisualElement;
                        if (root != null) return root;
                    }
                }
            }

            // Last resort: scan all VisualElement-typed fields on the toolbar type
            foreach (var field in toolbarType.GetFields(
                         BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(VisualElement).IsAssignableFrom(field.FieldType))
                {
                    var root = field.GetValue(toolbarObj) as VisualElement;
                    if (root != null) return root;
                }
            }

            return null;
        }

        private static void DumpElements(VisualElement el, System.Text.StringBuilder sb, int depth)
        {
            if (depth > 6) return;
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}{el.GetType().Name} name='{el.name}' class='{string.Join(" ", el.GetClasses())}'");
            foreach (var child in el.Children())
                DumpElements(child, sb, depth + 1);
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
            if (!_onGuiLogged) { _onGuiLogged = true; Debug.Log("[InstantSceneSwitcher] OnGUI called."); }
            RefreshFromPresetIfNeeded();

            if (_sceneNames == null || _sceneNames.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    var noSceneContent = new GUIContent(
                        "No Scenes",
                        "No scenes in the active preset. Open Instant Scene Switcher via Tools > Saad Khawaja > Instant Scene Switcher to add scenes or switch presets."
                    );
                    GUI.Button(new Rect(10, 2, 160, 18), noSceneContent, EditorStyles.toolbarPopup);
                }
                return;
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var currentScenePath = SceneManager.GetActiveScene().path;
                var currentSceneName = Path.GetFileNameWithoutExtension(currentScenePath);
                int sceneIndex = Array.IndexOf(_sceneNames, currentSceneName);

                // Explicit rect: x=10 left padding, y=2 centers 18px popup in 22px container, width=160, height=18
                int newIndex = EditorGUI.Popup(new Rect(10, 2, 160, 18), sceneIndex, _sceneNames, EditorStyles.toolbarPopup);

                if (newIndex != sceneIndex && newIndex >= 0 && newIndex < _scenes.Length)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(_scenes[newIndex]);
                }
            }
        }


    }
}
