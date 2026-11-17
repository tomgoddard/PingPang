using UnityEditor;
using UnityEditor.SceneManagement;

// Opens the game scene when the editor starts with an empty untitled scene,
// which happens on a fresh clone or after deleting Library/, so pressing
// Play does not show an empty world.
[InitializeOnLoad]
public static class OpenMainScene {

    const string scene = "Assets/scenes.unity";

    static OpenMainScene() {
        EditorApplication.delayCall += open_if_untitled;
    }

    static void open_if_untitled() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Application.isBatchMode)
            return;
        var active = EditorSceneManager.GetActiveScene();
        if (EditorSceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty)
            EditorSceneManager.OpenScene(scene);
    }
}
