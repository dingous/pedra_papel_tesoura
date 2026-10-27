import bpy, sys, json, math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
for sex in ('Male','Female'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(root/'Meshy'/f'Rps{sex}_Meshy.glb'))
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    arm.animation_data_clear()
    for pb in arm.pose.bones: pb.matrix_basis.identity()
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    for o in list(bpy.context.scene.objects):
        if o.type=='MESH' and not any(m.type=='ARMATURE' for m in o.modifiers): bpy.data.objects.remove(o,do_unlink=True)
    mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    arm.name=f'Rps{sex}_Rig'; mesh.name=f'Rps{sex}_Body'
    for poly in mesh.data.polygons: poly.use_smooth=True
    bpy.context.view_layer.update()
    points=[list(mesh.matrix_world@v.co) for v in mesh.data.vertices]
    (root/'Meshy'/f'Rps{sex}_vertices.json').write_text(json.dumps(points))
    bpy.ops.wm.save_as_mainfile(filepath=str(root/f'Rps{sex}_Base.blend'))
    scene=bpy.context.scene
    scene.render.engine='CYCLES'; scene.cycles.samples=24
    scene.world=bpy.data.worlds.new('StudioWorld');scene.world.color=(0.3,0.3,0.3)
    scene.view_settings.view_transform='Standard'
    for loc,energy,size in [((-3,-4,5),500,5),((3,-2,3),300,4)]:
        bpy.ops.object.light_add(type='AREA',location=loc); ob=bpy.context.object
        ob.data.energy=energy; ob.data.shape='DISK'; ob.data.size=size
        ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(0,-4,0.87))
    cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,0.87))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=2.05;scene.camera=cam
    scene.render.resolution_x=1024;scene.render.resolution_y=1024;scene.render.resolution_percentage=100
    scene.render.filepath=str(root/f'Rps{sex}_Tpose.png');bpy.ops.render.render(write_still=True)
    # Right hand, front view, enough resolution for finger placement.
    cam.location=(-0.66,-3,1.285);cam.rotation_euler=(Vector((-0.66,0,1.285))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=0.40
    scene.render.filepath=str(root/f'Rps{sex}_Hand.png');bpy.ops.render.render(write_still=True)
