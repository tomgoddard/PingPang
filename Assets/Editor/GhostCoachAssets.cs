using System.IO;
using UnityEditor;
using UnityEngine;

// Creates the semi-transparent materials used by the coach ghost.  They
// live in Assets/Resources/GhostCoach so they can be loaded at run time
// and are included in the Quest build.  Run once from the GhostCoach menu
// or with -executeMethod GhostCoachAssets.create_materials, then commit
// the .mat files.  Colors can be tuned afterwards in the Inspector.
public static class GhostCoachAssets {

    const string folder = "Assets/Resources/GhostCoach";

    [MenuItem("GhostCoach/Create ghost materials")]
    public static void create_materials() {
        Directory.CreateDirectory(folder);
        create_fade_material("ghost", new Color(0.45f, 0.85f, 1f, 0.35f));
        create_fade_material("ghost_ball", new Color(1f, 1f, 1f, 0.8f));
        create_fade_material("stand_marker", new Color(0.3f, 1f, 0.45f, 0.5f));
        create_fade_material("player_path", new Color(1f, 0.55f, 0.15f, 0.85f));
        AssetDatabase.SaveAssets();
        Debug.Log("GhostCoach materials created in " + folder);
    }

    // Standard shader in Fade mode, set up the same way as choosing
    // Rendering Mode: Fade in the material Inspector.
    static void create_fade_material(string name, Color color) {
        string path = folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Mode", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetFloat("_Glossiness", 0.2f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }
}
