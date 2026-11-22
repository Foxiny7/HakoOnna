"""Round 5: reduce the triangle count of a downloaded FBX (Blender, headless) and write it back as FBX.
usage: blender -b --factory-startup --python-exit-code 1 --python decimate_fbx.py -- <in.fbx> <out.fbx> <ratio>

Object names, materials and UVs are kept (Collapse decimation, UV seams respected). Prints triangles per object.
"""
import sys
import bpy

src, dst, ratio = sys.argv[sys.argv.index("--") + 1:][:3]
ratio = float(ratio)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    before = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = ratio
    mod.delimit = {"UV"}
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    after = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    for p in ob.data.polygons:
        p.use_smooth = True
    d = ob.dimensions
    print(f"DEC {ob.name}: {before} -> {after} tris, size {d.x:.3f} x {d.y:.3f} x {d.z:.3f}, mats {[m.name for m in ob.data.materials]}")
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"MESH", "EMPTY"}, mesh_smooth_type="OFF",
                         add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
print("wrote", dst)
