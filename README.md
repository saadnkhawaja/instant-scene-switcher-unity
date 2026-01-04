# 🎬 Instant Scene Switcher for Unity (Editor Utility)

![Instant Scene Switcher Demo](https://github.com/saadnkhawaja/instant-scene-switcher-unity/blob/main/instant-demo.gif)

**Instant Scene Switcher** is a lightweight Unity Editor utility that lets you jump between scenes instantly—either from your **Build Settings scenes** or from custom **presets**. Use a dedicated **scenes dropdown** or shortcut and switch scenes in seconds without hunting through folders.

## ✨ Features

- **Preset System:** Create your own curated scene lists for different workflows (Gameplay, UI, Levels, Test, etc.)
- **Use Build Scenes:** Quickly select from scenes included in **Build Settings**
- **Fast Scene Switching:** One-click (or one-hotkey) switching directly inside the Editor
- **Editor-Only Utility:** No runtime impact, no builds bloated with extra scripts

## 🧭 How to Use

1. Open Unity
2. Select a scene from the dropdown at the top to instantly switch to it. (based on build settings)
3. Go to: **Tools → Saad Khawaja → Instant Scene Switcher**
4. Edit the list of scenes:
   - **Build Settings Scenes**, or
   - **Your Presets**
5. Click "Apply" to update the dropdown list

> Tip : Use CTRL + ALT + S to quickly open the Scene Switcher window and use keyboard buttons to swtich scenes quickly.

## 📦 Installation

### Option A — Unity Package Manager (Git URL)

1. In Unity, open **Window → Package Manager**
2. Click the **+** button → **Add package from git URL…**
3. Paste the repo url:
   - `https://github.com/saadnkhawaja/instant-scene-switcher-unity.git`

### Option B — Import `.unitypackage`

- Download the latest `.unitypackage` from **Releases**
- Import via: **Assets → Import Package → Custom Package...**

### Folder Note

Ensure the main editor script(s) live under an `Editor/` folder, e.g.
