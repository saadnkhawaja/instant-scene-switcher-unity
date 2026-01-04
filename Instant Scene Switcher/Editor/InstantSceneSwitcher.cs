using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;

namespace SaadKhawaja.InstantSceneSwitcher
{
    [FilePath("ProjectSettings/InstantSceneSwitcherSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class InstantSceneSwitcherSettings : ScriptableSingleton<InstantSceneSwitcherSettings>
    {
        [Serializable]
        public class Preset
        {
            public string id;
            public string name;
            public List<string> scenePaths = new List<string>();
        }

        [SerializeField] private List<Preset> presets = new List<Preset>();
        [SerializeField] private string activePresetId = BuildPresetId;
        [SerializeField] private string selectedPresetId = BuildPresetId;

        public const string BuildPresetId = "preset_build";

        public IReadOnlyList<Preset> Presets => presets;

        public string ActivePresetId
        {
            get => string.IsNullOrEmpty(activePresetId) ? BuildPresetId : activePresetId;
            set { activePresetId = string.IsNullOrEmpty(value) ? BuildPresetId : value; Save(true); }
        }

        public string SelectedPresetId
        {
            get => string.IsNullOrEmpty(selectedPresetId) ? BuildPresetId : selectedPresetId;
            set { selectedPresetId = string.IsNullOrEmpty(value) ? BuildPresetId : value; Save(true); }
        }

        public Preset GetPreset(string id) => presets.FirstOrDefault(p => p.id == id);

        public string GetPresetName(string id)
        {
            if (id == BuildPresetId) return "Build Settings";
            return GetPreset(id)?.name ?? "Build Settings";
        }

        public List<string> GetScenePathsForPreset(string id)
        {
            if (id == BuildPresetId)
            {
                return EditorBuildSettings.scenes
                    .Where(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                    .Select(s => s.path)
                    .Distinct()
                    .ToList();
            }

            var p = GetPreset(id);
            if (p == null) return new List<string>();

            p.scenePaths = p.scenePaths.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            Save(true);
            return p.scenePaths.ToList();
        }

        public Preset CreatePreset(string name, IEnumerable<string> scenePaths = null)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name)) throw new Exception("Preset name cannot be empty.");
            if (presets.Any(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)))
                throw new Exception("A preset with that name already exists.");

            var p = new Preset
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                scenePaths = scenePaths?.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList() ?? new List<string>()
            };

            presets.Add(p);
            Save(true);
            return p;
        }

        public void DeletePreset(string id)
        {
            if (id == BuildPresetId) return;

            presets.RemoveAll(p => p.id == id);

            if (ActivePresetId == id) ActivePresetId = BuildPresetId;
            if (SelectedPresetId == id) SelectedPresetId = BuildPresetId;

            Save(true);
        }

        public void SetScenes(string id, List<string> scenePaths)
        {
            if (id == BuildPresetId) return;
            var p = GetPreset(id);
            if (p == null) return;

            p.scenePaths = scenePaths?.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList() ?? new List<string>();
            Save(true);
        }

        public void AddScene(string id, string scenePath)
        {
            if (id == BuildPresetId) return;
            if (string.IsNullOrEmpty(scenePath)) return;

            var p = GetPreset(id);
            if (p == null) return;

            if (!p.scenePaths.Contains(scenePath))
            {
                p.scenePaths.Add(scenePath);
                Save(true);
            }
        }

        public void RemoveSceneAt(string id, int index)
        {
            if (id == BuildPresetId) return;

            var p = GetPreset(id);
            if (p == null) return;
            if (index < 0 || index >= p.scenePaths.Count) return;

            p.scenePaths.RemoveAt(index);
            Save(true);
        }

        public void RemoveSceneByPath(string presetId, string scenePath)
        {
            if (presetId == BuildPresetId) return;
            if (string.IsNullOrEmpty(scenePath)) return;

            var p = GetPreset(presetId);
            if (p == null) return;

            p.scenePaths.RemoveAll(x => x == scenePath);
            Save(true);
        }

        public bool ContainsScene(string id, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (id == BuildPresetId)
                return GetScenePathsForPreset(BuildPresetId).Contains(path);

            var p = GetPreset(id);
            if (p == null) return false;
            return p.scenePaths.Contains(path);
        }
    }

    public static class SceneSelectorToolbarBridge
    {
        public static List<string> GetActiveScenes()
        {
            var s = InstantSceneSwitcherSettings.instance;
            return s.GetScenePathsForPreset(s.ActivePresetId);
        }

        public static void RequestToolbarRefresh()
        {
            SceneSelectorToolbar.RefreshFromPreset();
            InternalEditorUtility.RepaintAllViews();
        }
    }

    public class InstantSceneSwitcher : EditorWindow
    {
        private const float HEADER_LOGO_HEIGHT = 128f;

        private InstantSceneSwitcherSettings S => InstantSceneSwitcherSettings.instance;

        private Vector2 _presetScroll;
        private Vector2 _sceneScroll;

        private string _newPresetName = "Custom";

        private ReorderableList _sceneList;
        private SceneAsset _sceneToAdd;

        private GUIStyle _headerTitle;
        private GUIStyle _muted;
        private GUIStyle _centerBold;
        private GUIStyle _presetBtn;
        private GUIStyle _presetBtnSelected;

        private bool _dragOverlayActive;

        [MenuItem("Tools/Saad Khawaja/Instant Scene Switcher")]
        public static void Open()
        {
            var w = GetWindow<InstantSceneSwitcher>("Instant Scene Switcher");
            w.minSize = new Vector2(580, 600);
        }

        private void OnEnable()
        {
            BuildSceneListUI();
        }

        private void OnGUI()
        {
            InitStyles();
            DrawLogoHeader();
            DrawGlobalDropOverlay();

            GUILayout.Space(10);

            using (new GUILayout.HorizontalScope())
            {
                DrawPresetsPanel();
                GUILayout.Space(12);
                DrawScenesPanel();
            }

            GUILayout.Space(12);
            DrawApplyButton();

            GUILayout.Space(12);
            DrawFooter();
        }

        private void DrawLogoHeader()
        {
            Texture2D logo = Resources.Load<Texture2D>("logo_iss");
            if (logo != null)
            {
                GUILayout.Label(logo, GUILayout.Height(HEADER_LOGO_HEIGHT));
            }
            else
            {
                GUILayout.Label("🎬 Instant Scene Switcher | Saad Khawaja", _centerBold);
            }
        }

        private void DrawGlobalDropOverlay()
        {
            float y = HEADER_LOGO_HEIGHT + 8f;
            var dropRect = new Rect(10f, y, position.width - 20f, position.height - y - 110f);

            bool allowDrop = S.SelectedPresetId != InstantSceneSwitcherSettings.BuildPresetId;

            var e = Event.current;
            if (e == null) return;

            bool isDraggingScene =
                (e.type == EventType.DragUpdated || e.type == EventType.DragPerform) &&
                DragAndDrop.objectReferences != null &&
                DragAndDrop.objectReferences.Any(o => o is SceneAsset);

            _dragOverlayActive = allowDrop && isDraggingScene;

            if (_dragOverlayActive)
            {
                EditorGUI.DrawRect(dropRect, new Color(0.07f, 0.12f, 0.12f, 0.92f));
                GUI.Label(dropRect, "Drop Scene Here", _centerBold);

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is SceneAsset sa)
                            AddSceneAsset(sa);
                    }
                }

                e.Use();
            }
        }

        private void DrawPresetsPanel()
        {
            using (new GUILayout.VerticalScope(GUILayout.Width(235)))
            {
                EditorGUILayout.LabelField("Presets", _headerTitle);

                using (new GUILayout.VerticalScope("box"))
                {
                    _presetScroll = EditorGUILayout.BeginScrollView(_presetScroll, GUILayout.MinHeight(340));

                    DrawPresetRow(
                        InstantSceneSwitcherSettings.BuildPresetId,
                        "★ Build Settings",
                        lockedDelete: true
                    );

                    foreach (var p in S.Presets.OrderBy(x => x.name))
                        DrawPresetRow(p.id, p.name, lockedDelete: false);

                    EditorGUILayout.EndScrollView();
                }

                GUILayout.Space(10);

                using (new GUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Create", EditorStyles.boldLabel);
                    _newPresetName = EditorGUILayout.TextField(_newPresetName);

                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Create", GUILayout.Height(24)))
                            TryCreatePreset(_newPresetName);

                        if (GUILayout.Button("Duplicate", GUILayout.Height(24)))
                            DuplicateSelected();
                    }
                }
            }
        }

        private void DrawPresetRow(string id, string name, bool lockedDelete)
        {
            bool isSelected = (id == S.SelectedPresetId);

            using (new GUILayout.HorizontalScope())
            {
                var style = isSelected ? _presetBtnSelected : _presetBtn;
                float h = isSelected ? 36f : 28f;
                bool isApplied = (id == S.ActivePresetId);

                Color prev = GUI.backgroundColor;
                if (isApplied) GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);

                if (GUILayout.Button(name, style, GUILayout.Height(h)))
                {
                    S.SelectedPresetId = id;
                    BuildSceneListUI();
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = prev;

                EditorGUI.BeginDisabledGroup(lockedDelete);
                if (GUILayout.Button("–", GUILayout.Width(26), GUILayout.Height(h)))
                {
                    if (EditorUtility.DisplayDialog("Delete Preset", $"Delete preset '{name}'?", "Delete", "Cancel"))
                    {
                        S.DeletePreset(id);
                        BuildSceneListUI();
                        SceneSelectorToolbarBridge.RequestToolbarRefresh();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawScenesPanel()
        {
            using (new GUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField($"Scenes : {S.GetPresetName(S.SelectedPresetId)}", _headerTitle);

                using (new GUILayout.VerticalScope("box"))
                {
                    _sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll, GUILayout.MinHeight(360));
                    _sceneList?.DoLayoutList();
                    EditorGUILayout.EndScrollView();

                    GUILayout.Space(10);
                    DrawAddSceneControlsCompact();
                }
            }
        }

        private void DrawAddSceneControlsCompact()
        {
            EditorGUI.BeginDisabledGroup(S.SelectedPresetId == InstantSceneSwitcherSettings.BuildPresetId);

            using (new GUILayout.HorizontalScope())
            {
                _sceneToAdd = (SceneAsset)EditorGUILayout.ObjectField(_sceneToAdd, typeof(SceneAsset), false);

                if (GUILayout.Button("+ Add", GUILayout.Width(70)))
                {
                    AddSceneAsset(_sceneToAdd);
                    _sceneToAdd = null;
                    GUI.FocusControl(null);
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawApplyButton()
        {
            bool alreadyApplied = (S.SelectedPresetId == S.ActivePresetId);
            string presetName = S.GetPresetName(S.SelectedPresetId);

            string label = alreadyApplied
                ? $"Already Using : {presetName}"
                : $"Apply Preset : {presetName}";

            EditorGUI.BeginDisabledGroup(alreadyApplied);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
            if (GUILayout.Button(label, GUILayout.Height(46)))
                ApplySelectedPreset();
            GUI.backgroundColor = Color.white;

            EditorGUI.EndDisabledGroup();
        }

        private void DrawFooter()
        {
            using (new GUILayout.VerticalScope("box"))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.2f, 1f);

                using (new GUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Shortcut", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Ctrl + Alt + S → Quick Switch Popup (Up/Down selects • Enter loads • Esc closes)", _muted);
                }

                GUI.backgroundColor = prev;

                GUILayout.Space(8);

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("About Me", GUILayout.Height(26)))
                        Application.OpenURL("https://www.saadkhawaja.com/about");

                    if (GUILayout.Button("Follow (X)", GUILayout.Height(26)))
                        Application.OpenURL("https://x.com/saadskhawaja");

                    if (GUILayout.Button("GitHub", GUILayout.Height(26)))
                        Application.OpenURL("https://github.com/saadnkhawaja");
                }
            }
        }

        private void InitStyles()
        {
            if (_headerTitle != null) return;

            _headerTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _centerBold = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            _muted = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            _presetBtn = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
                fontStyle = FontStyle.Normal
            };

            _presetBtnSelected = new GUIStyle(_presetBtn)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(14, 10, 10, 10)
            };
        }

        private void BuildSceneListUI()
        {
            var scenes = S.GetScenePathsForPreset(S.SelectedPresetId);
            bool editable = (S.SelectedPresetId != InstantSceneSwitcherSettings.BuildPresetId);

            _sceneList = new ReorderableList(scenes, typeof(string), editable, true, false, editable);

            _sceneList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, " ");
            };

            _sceneList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= scenes.Count) return;

                rect.y += 3;
                string path = scenes[index];
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                string inline = $"{sceneName} ({path})";

                if (GUI.Button(rect, inline, EditorStyles.label))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }

                var e = Event.current;
                if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
                {
                    var menu = new GenericMenu();

                    menu.AddItem(new GUIContent("Show in Explorer/Finder"), false, () =>
                    {
                        EditorUtility.RevealInFinder(path);
                    });

                    if (editable)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove from Preset"), false, () =>
                        {
                            S.RemoveSceneAt(S.SelectedPresetId, index);
                            BuildSceneListUI();
                            SceneSelectorToolbarBridge.RequestToolbarRefresh();
                            Repaint();
                        });
                    }
                    else
                    {
                        menu.AddSeparator("");
                        menu.AddDisabledItem(new GUIContent("Remove from Preset"));
                    }

                    menu.ShowAsContext();
                    e.Use();
                }
            };

            _sceneList.onReorderCallback = list =>
            {
                if (!editable) return;
                S.SetScenes(S.SelectedPresetId, scenes);
                SceneSelectorToolbarBridge.RequestToolbarRefresh();
            };

            _sceneList.onRemoveCallback = list =>
            {
                if (!editable) return;
                if (list.index >= 0 && list.index < scenes.Count)
                {
                    S.RemoveSceneAt(S.SelectedPresetId, list.index);
                    BuildSceneListUI();
                    SceneSelectorToolbarBridge.RequestToolbarRefresh();
                    Repaint();
                }
            };
        }

        private void AddSceneAsset(SceneAsset sceneAsset)
        {
            if (sceneAsset == null) return;
            if (S.SelectedPresetId == InstantSceneSwitcherSettings.BuildPresetId) return;

            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return;

            S.AddScene(S.SelectedPresetId, path);
            BuildSceneListUI();
            SceneSelectorToolbarBridge.RequestToolbarRefresh();
            Repaint();
        }

        private void TryCreatePreset(string name)
        {
            try
            {
                var p = S.CreatePreset(name);
                S.SelectedPresetId = p.id;
                BuildSceneListUI();
                SceneSelectorToolbarBridge.RequestToolbarRefresh();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Instant Scene Switcher", ex.Message, "OK");
            }
        }

        private void DuplicateSelected()
        {
            var currentScenes = S.GetScenePathsForPreset(S.SelectedPresetId);
            string baseName = S.GetPresetName(S.SelectedPresetId) + " Copy";

            string name = baseName;
            int i = 1;
            while (S.Presets.Any(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName} {i}";
                i++;
            }

            try
            {
                var p = S.CreatePreset(name, currentScenes);
                S.SelectedPresetId = p.id;
                BuildSceneListUI();
                SceneSelectorToolbarBridge.RequestToolbarRefresh();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Instant Scene Switcher", ex.Message, "OK");
            }
        }

        private void ApplySelectedPreset()
        {
            var scenes = S.GetScenePathsForPreset(S.SelectedPresetId);
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Instant Scene Switcher", "This preset has no scenes.", "OK");
                return;
            }

            S.ActivePresetId = S.SelectedPresetId;
            SceneSelectorToolbarBridge.RequestToolbarRefresh();
            ShowNotification(new GUIContent($"Applied: {S.GetPresetName(S.ActivePresetId)}"));
        }
    }

    // =========================
    // QUICK POPUP (Ctrl+Alt+S) 
    // =========================

    public class QuickSceneSwitchPopup : EditorWindow
    {
        private List<string> _scenePaths;
        private int _index;
        private Vector2 _scroll;

        private const float W = 520;
        private const float H = 420;

        [Shortcut("Saad Khawaja/Instant Scene Switcher/Quick Switch", KeyCode.S, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
        private static void QuickSwitchShortcut() => OpenPopup();

        public static void OpenPopup()
        {
            var scenes = SceneSelectorToolbarBridge.GetActiveScenes();
            if (scenes == null || scenes.Count == 0)
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();

            var win = CreateInstance<QuickSceneSwitchPopup>();
            win._scenePaths = scenes.Distinct().Where(p => !string.IsNullOrEmpty(p)).ToList();
            win._index = 0;

            var mp = GUIUtility.GUIToScreenPoint(Event.current != null
                ? Event.current.mousePosition
                : new Vector2(Screen.currentResolution.width / 2f, Screen.currentResolution.height / 2f));

            win.position = new Rect(mp.x - W * 0.5f, mp.y - 24, W, H);
            win.titleContent = new GUIContent("Scene Switch");
            win.ShowUtility();
            win.Focus();
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void OnGUI()
        {
            if (_scenePaths == null || _scenePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes available.", MessageType.Info);
                if (GUILayout.Button("Close")) Close();
                return;
            }

            HandleKeys();

            GUILayout.Space(8);

            using (new GUILayout.VerticalScope("box"))
            {
                Texture2D logo = Resources.Load<Texture2D>("logo_iss");
                if (logo != null)
                    GUILayout.Label(logo, GUILayout.Height(128));

                EditorGUILayout.LabelField("Quick Scene Switch", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Up/Down selects • Enter loads • Esc closes", EditorStyles.miniLabel);
            }

            GUILayout.Space(8);

            using (new GUILayout.VerticalScope("box"))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                for (int i = 0; i < _scenePaths.Count; i++)
                {
                    string p = _scenePaths[i];
                    string name = System.IO.Path.GetFileNameWithoutExtension(p);
                    string label = $"{name} ({p})";

                    Rect r = GUILayoutUtility.GetRect(10, 28, GUILayout.ExpandWidth(true));
                    if (i == _index)
                        EditorGUI.DrawRect(r, new Color(0.2f, 0.6f, 1f, 0.20f));

                    GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height), label, EditorStyles.label);

                    var e = Event.current;

                    if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                    {
                        _index = i;
                        if (e.clickCount == 2)
                            LoadSelected();
                        e.Use();
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(8);

            using (new GUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
                if (GUILayout.Button("Load", GUILayout.Height(32)))
                    LoadSelected();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Close", GUILayout.Height(32)))
                    Close();
            }

            GUILayout.Space(6);
        }

        private void HandleKeys()
        {
            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.DownArrow)
                {
                    _index = Mathf.Clamp(_index + 1, 0, _scenePaths.Count - 1);
                    AutoScroll();
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.UpArrow)
                {
                    _index = Mathf.Clamp(_index - 1, 0, _scenePaths.Count - 1);
                    AutoScroll();
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    LoadSelected();
                    e.Use();
                    return;
                }
            }
        }

        private void AutoScroll()
        {
            float rowH = 28f;
            float targetY = _index * rowH;
            _scroll.y = Mathf.Lerp(_scroll.y, targetY, 0.35f);
            Repaint();
        }

        private void LoadSelected()
        {
            if (_index < 0 || _index >= _scenePaths.Count) return;

            string path = _scenePaths[_index];
            if (string.IsNullOrEmpty(path)) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(path);
            Close();
        }
    }
}
