"""Validate the exported files, not just the Blender source scenes."""
import bpy, json, sys, importlib.util
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('rps_build',root/'build_rps_characters.py')
build=importlib.util.module_from_spec(spec);spec.loader.exec_module(build)
reports=[]
for sex in ('Male','Female'):
    source=root.parents[1]/'UnityProject'/'Assets'/'Characters'/sex/f'Rps{sex}.fbx'
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
    required={'Idle','CountIn','Rock','Paper','Scissors','LeftCountIn','LeftRock','LeftPaper','LeftScissors'}
    assert required==set(actions),(sex,set(actions))
    fingers=[s+'Hand'+f+str(i) for s in ('Right','Left') for f in ('Thumb','Index','Middle','Ring','Pinky') for i in range(1,4)]
    counts={n:0 for n in fingers}
    for v in mesh.data.vertices:
        assert sum(g.weight for g in v.groups)>0,(sex,'unweighted vertex',v.index)
        for g in v.groups:
            name=mesh.vertex_groups[g.group].name
            if name in counts and g.weight>.01:counts[name]+=1
    assert all(counts.values()),counts
    reports.append({'character':sex,'fbx':str(source),'bytes':source.stat().st_size,'bones':len(arm.data.bones),'finger_bones':len(fingers),
        'finger_weighted_vertices':counts,'vertices':len(mesh.data.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),
        'clips':{n:{'frames':list(a.frame_range)} for n,a in actions.items()},'checks':'All 9 clips present; all 30 finger bones weighted; no unweighted vertices.'})
    scene=bpy.context.scene;cam=build.studio(scene)
    for side in ('Right','Left'):
        for gesture in ('Rock','Paper','Scissors'):
            action=actions[('Left' if side=='Left' else '')+gesture]
            arm.animation_data.action=action
            if action.slots:arm.animation_data.action_slot=action.slots[0]
            scene.frame_set(30);bpy.context.view_layer.update()
            hand=arm.pose.bones[side+'Hand'];target=arm.matrix_world@((hand.head+hand.tail)/2)
            cam.location=target+Vector((-.06 if side=='Right' else .06,-1.5,.04))
            cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.29
            scene.render.resolution_x=512;scene.render.resolution_y=512
            scene.render.filepath=str(root/f'Validated_{sex}_{side}_{gesture}.png');bpy.ops.render.render(write_still=True)
(root/'validation.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('FBX_VALIDATION_PASSED: 2 files, 18 actions, 60 weighted finger joints',flush=True)
