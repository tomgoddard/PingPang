# Blender tools

`export_coach_clip.py` turns a Qualisys take in Blender into a coach clip
for GhostCoach.  See the comment at the top of the script for options.

```
/Applications/Blender.app/Contents/MacOS/Blender -b take.blend \
    --python Tools/blender/export_coach_clip.py -- \
    Assets/Resources/GhostCoach/clips/coach_take0006.json --min-speed 2
```

It writes one file in `Assets/Resources/GhostCoach/clips/`: `NAME.json`, the
coach's motion (racket, head, body joints and strokes, in table
coordinates).  Numbers only, no video or names.  It goes in git with the
consent of the person recorded.

The coach is drawn as a simple figure of capsules between the joints, so no
character mesh or Mixamo rig is needed.  The Blender file needs an armature
with the joint names listed in the script (Hips, Spine2, Neck, Head,
HeadTop_End, LeftArm, ...; a prefix such as `mixamorig:` is ignored), the
racket markers `racket:racket0..2`, the table corner markers `bord0` and
`bord1` and the wrist markers `x_RWristIn` and `x_RWristOut`.
