using System.Collections.Generic;
using UnityEngine;

// Shared helpers for building the semi-transparent ghost visuals used in
// both training phases.
public static class GhostVisuals {

    // The game's paddle blade and rubbers are flat boxes.  The ghost racket
    // is drawn round by default, like a real blade.  Only how the ghost
    // looks: the hit area of the player's own paddle in the game is still
    // the box.
    public static bool round_racket = true;
    static Mesh cylinder_mesh;

    // The green disc on the floor showing where to stand.  Off by default:
    // it is only needed when instructing a new tester.
    public static bool show_stand_marker = false;

    // Renderers hidden by the player's "hide racket and ball" toggle.  The
    // ghost racket is copied from the player's paddle and must still
    // include them.
    public static HashSet<Renderer> hidden_by_toggle = new HashSet<Renderer>();

    static Mesh cylinder() {
        if (cylinder_mesh == null) {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder_mesh = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
        }
        return cylinder_mesh;
    }

    // Material from Assets/Resources/GhostCoach (see GhostCoachAssets).
    public static Material material(string name) {
        return Resources.Load<Material>("GhostCoach/" + name);
    }

    public static Transform primitive(PrimitiveType type, string name, Material m, Transform parent) {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());   // Must not touch the ball or menus.
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    // Flat green disc on the floor showing the player where to stand.
    public static Transform stand_marker(string name, Transform parent) {
        Transform t = primitive(PrimitiveType.Cylinder, name, material("stand_marker"), parent);
        t.localScale = new Vector3(0.5f, 0.003f, 0.5f);
        // Hidden but still there: desktop mode uses its position.
        t.GetComponent<MeshRenderer>().enabled = show_stand_marker;
        return t;
    }

    // Place an object on the floor at a table coordinates position.
    public static void place_on_floor(Transform t, Transform table, Vector3 table_position) {
        table_position.y = 0.005f;              // Table origin is on the floor.
        t.position = TableSpace.to_world(table, table_position);
        t.rotation = table.rotation;
    }

    // Average coach head position and facing over a time range of a clip,
    // in table coordinates.  Right is the coach's right hand side.
    public static Vector3 coach_stance(MotionClip clip, float start, float end,
                                       out Vector3 forward, out Vector3 right) {
        Vector3 sum = Vector3.zero, forward_sum = Vector3.zero;
        int n = 0;
        foreach (MotionFrame f in clip.frames) {
            if (f.t < start || f.t > end)
                continue;
            sum += f.head_position;
            forward_sum += f.head_rotation * Vector3.forward;
            n += 1;
        }
        forward = Vector3.ProjectOnPlane(forward_sum, Vector3.up).normalized;
        if (forward == Vector3.zero)
            forward = Vector3.forward;
        right = Vector3.Cross(Vector3.up, forward);
        return (n > 0 ? sum / n : Vector3.zero);
    }

    // Copy only the visible meshes of a paddle, not its colliders or
    // scripts, so the ghost cannot hit the ball.
    public static Transform copy_meshes(Transform source, string name, Material m, Transform parent) {
        Transform copy = new GameObject(name).transform;
        copy.SetParent(parent, false);
        Quaternion inverse = Quaternion.Inverse(source.rotation);
        foreach (MeshRenderer r in source.GetComponentsInChildren<MeshRenderer>()) {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if ((!r.enabled && !hidden_by_toggle.Contains(r)) || mf == null || mf.sharedMesh == null)
                continue;
            GameObject g = new GameObject(r.name);
            g.transform.SetParent(copy, false);
            g.transform.localPosition = inverse * (r.transform.position - source.position);
            g.transform.localRotation = inverse * r.transform.rotation;
            g.transform.localScale = r.transform.lossyScale;    // Copy root is not scaled.
            Mesh mesh = mf.sharedMesh;
            if (round_racket && mesh.name == "Cube") {
                // A cylinder lying along the box's thin axis (z): turn its
                // axis (y) to z, so its scale x, y, z becomes diameter in x,
                // half the thickness, and diameter in y.
                Vector3 s = r.transform.lossyScale;
                g.transform.localRotation *= Quaternion.Euler(90f, 0f, 0f);
                g.transform.localScale = new Vector3(s.x, 0.5f * s.z, s.y);
                mesh = cylinder();
            }
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        return copy;
    }

    // Draw a paddle round: for each box shaped blade, rubber and sponge add
    // a disc as a child and hide the box.  The boxes themselves are not
    // touched, since Bouncer reads their position and size for the hit area.
    public static void make_racket_round(Transform paddle) {
        if (!round_racket)
            return;
        foreach (MeshRenderer r in paddle.GetComponentsInChildren<MeshRenderer>(true)) {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (r.name.StartsWith("round ") || mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube")
                continue;
            GameObject g = new GameObject("round " + r.name);
            g.layer = r.gameObject.layer;
            g.transform.SetParent(r.transform, false);
            // Cylinder axis (y) turned to the box's thin axis (z).  In the
            // box's own scale, x and z are the diameters and y is half the
            // height, so 1, 0.5, 1 gives a disc as thick as the box.
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = new Vector3(1f, 0.5f, 1f);
            g.AddComponent<MeshFilter>().sharedMesh = cylinder();
            MeshRenderer round = g.AddComponent<MeshRenderer>();
            round.sharedMaterials = r.sharedMaterials;
            round.enabled = r.enabled;
            r.enabled = false;
        }
    }

    // Thin line through points, e.g. a paddle path.
    public static LineRenderer line(string name, Material m, float width, Transform parent) {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.sharedMaterial = m;
        lr.widthMultiplier = width;
        lr.useWorldSpace = true;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 0;
        return lr;
    }
}
