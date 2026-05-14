using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
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

        private static ToolbarMenu _toolbarMenu;
        private static string _lastMenuText;

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

                    var root = GetToolbarRoot(toolbarType, toolbars[0]);

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
                        Debug.LogWarning("[InstantSceneSwitcher] Could not find right toolbar zone.");
                        return;
                    }

                    if (rightZone.Q("SceneSelectorToolbarMenu") == null)
                    {
                        _toolbarMenu = CreateToolbarMenu();
                        rightZone.Insert(0, _toolbarMenu);
                        _injected = true;
                    }
                }

                RefreshFromPresetIfNeeded();
                UpdateMenuText();
            }
            catch (Exception ex)
            {
                if (!_injected)
                    Debug.LogError($"[InstantSceneSwitcher] Injection error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static ToolbarMenu CreateToolbarMenu()
        {
            var menu = new ToolbarMenu { name = "SceneSelectorToolbarMenu" };
            menu.style.minWidth = 130;
            menu.style.maxWidth = 200;
            RebuildMenuItems(menu);
            return menu;
        }

        private static void RebuildMenuItems(ToolbarMenu menu)
        {
            if (menu == null) return;

            var items = menu.menu.MenuItems();
            for (int i = items.Count - 1; i >= 0; i--)
                menu.menu.RemoveItemAt(i);

            if (_sceneNames == null || _sceneNames.Length == 0)
            {
                menu.text = "No Scenes";
                menu.SetEnabled(false);
                return;
            }

            menu.SetEnabled(true);

            for (int i = 0; i < _sceneNames.Length; i++)
            {
                int idx = i;
                string sceneName = _sceneNames[i];
                menu.menu.AppendAction(
                    sceneName,
                    _ =>
                    {
                        if (!Application.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(_scenes[idx]);
                    },
                    _ =>
                    {
                        var current = Path.GetFileNameWithoutExtension(SceneManager.GetActiveScene().path);
                        return current == sceneName
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal;
                    }
                );
            }
        }

        private static void UpdateMenuText()
        {
            if (_toolbarMenu == null) return;

            string text;
            if (_sceneNames == null || _sceneNames.Length == 0)
            {
                text = "No Scenes";
            }
            else
            {
                var currentName = Path.GetFileNameWithoutExtension(SceneManager.GetActiveScene().path);
                text = Array.IndexOf(_sceneNames, currentName) >= 0 ? currentName : $"({currentName})";
            }

            if (text == _lastMenuText) return;
            _lastMenuText = text;
            _toolbarMenu.text = text;
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

                _lastMenuText = null; // force text refresh
                RebuildMenuItems(_toolbarMenu);
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

        [MenuItem("Tools/Saad Khawaja/Instant Scene Switcher/Show Toolbar")]
        public static void ForceShowToolbar()
        {
            _injected = false;
            _treeDumped = false;
            _lastMenuText = null;

            if (_toolbarMenu != null)
            {
                _toolbarMenu.RemoveFromHierarchy();
                _toolbarMenu = null;
            }

            Debug.Log("[ISS] ForceShowToolbar: reset injection flags, will re-inject on next Update.");
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
    }
}
