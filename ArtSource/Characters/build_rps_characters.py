"""Blender 4.5: add weighted finger joints, author RPS actions, export Unity FBX."""
import bpy, bmesh, math, json, sys
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(__file__).resolve().parent
ASSETS=ROOT.parents[1]/'UnityProject'/'Assets'/'Characters'
POINTS={
 'Male':{
  'Thumb':[(617,390),(609,302),(586,240),(566,179)],
  'Index':[(484,335),(420,309),(369,286),(331,269)],
  'Middle':[(470,393),(398,384),(330,375),(265,367)],
  'Ring':[(464,466),(395,466),(329,466),(285,466)],
  'Pinky':[(489,511),(434,538),(388,559),(355,574)]},
 'Female':{
  'Thumb':[(795,461),(774,397),(760,336),(772,286)],
  'Index':[(678,425),(629,393),(584,367),(549,343)],
  'Middle':[(645,464),(590,451),(539,441),(502,433)],
  'Ring':[(641,518),(582,519),(536,520),(502,520)],
  'Pinky':[(655,558),(611,580),(571,599),(543,611)]}}

def smooth(v):
    v=max(0,min(1,v));return v*v*(3-2*v)

def frame(direction, normal):
    y=Vector(direction).normalized();z=Vector(normal);z=(z-y*y.dot(z)).normalized()
    x=y.cross(z).normalized()
    return Matrix((x,y,z)).transposed()

def orient(arm,name,direction,normal=(0,-1,0)):
    pb=arm.pose.bones[name];b=pb.bone
    source=frame(b.tail_local-b.head_local,(0,-1,0))
    rot=frame(direction,normal)@source.inverted()@b.matrix_local.to_3x3()
    mat=rot.to_4x4();mat.translation=pb.head
    pb.matrix=mat;bpy.context.view_layer.update()

def create_fingers(arm,mesh,sex):
    points={};verts=[mesh.matrix_world@v.co for v in mesh.data.vertices]
    for side,sign in [('Right',1),('Left',-1)]:
        for finger,pixels in POINTS[sex].items():
            chain=[]
            for px,pz in pixels:
                x=sign*(-.86+.4*px/1024);z=1.485-.4*pz/1024
                near=sorted(verts,key=lambda v:(v.x-x)**2+(v.z-z)**2)[:24]
                y=(min(v.y for v in near)+max(v.y for v in near))/2
                chain.append(Vector((x,y,z)))
            # Keep each chain in one bend plane to avoid twisting at tiny joints.
            avg=sum(v.y for v in chain)/len(chain)
            for v in chain:v.y=avg
            points[(side,finger)]=chain
    bpy.context.view_layer.objects.active=arm;arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for (side,finger),chain in points.items():
        parent=arm.data.edit_bones[side+'Hand']
        for i in range(3):
            b=arm.data.edit_bones.new(side+'Hand'+finger+str(i+1))
            b.head=chain[i];b.tail=chain[i+1];b.parent=parent
            b.align_roll(Vector((0,-1,0)));b.use_connect=i>0
            parent=b
    bpy.ops.object.mode_set(mode='OBJECT')
    return points

def fingers(arm,side,gesture,amount=1):
    for finger in ('Thumb','Index','Middle','Ring','Pinky'):
        curled=gesture=='Rock' or (gesture=='Scissors' and finger in ('Thumb','Ring','Pinky'))
        if gesture=='Relax': factor=.16
        else: factor=1 if curled else 0
        factor*=amount
        for i in range(3):
            pb=arm.pose.bones[side+'Hand'+finger+str(i+1)]
            d=(pb.bone.tail_local-pb.bone.head_local).normalized()
            local=pb.bone.matrix_local.to_quaternion()
            axis=d.cross(Vector((0,-1,0))).normalized()
            curl=[62,82,52][i] if finger!='Thumb' else [30,55,50][i]
            q=Quaternion(axis,math.radians(curl)*factor)
            if i==0 and finger!='Thumb' and factor:
                target=Vector((-1 if side=='Right' else 1,0,0))
                align=d.rotation_difference(target)
                q=Quaternion(d.cross(Vector((0,-1,0))),math.radians(curl)*factor)@Quaternion().slerp(align,factor*.82)
            if i==0 and finger=='Thumb' and factor:
                target=Vector((-.85 if side=='Right' else .85,-.15,.28)).normalized()
                q=Quaternion().slerp(d.rotation_difference(target),factor*.75)
            pb.rotation_quaternion=local.inverted()@q@local

def rebuild_hands(arm,mesh,sex,points):
    """Replace generated finger topology with regular rings for stable skinning."""
    cut=.596 if sex=='Male' else .530
    # Sample the generated skin texture so the new hand material matches the body.
    image=next(i for i in bpy.data.images if i.name=='texture_0')
    pixels=np.asarray(image.pixels[:],dtype=np.float32).reshape(image.size[1],image.size[0],4)
    colors=[];uv=mesh.data.uv_layers.active.data
    for loop in mesh.data.loops:
        v=mesh.data.vertices[loop.vertex_index]
        if abs(v.co.x)>cut and abs(v.co.x)<cut+.06:
            u,w=uv[loop.index].uv;colors.append(pixels[int(w*(image.size[1]-1))%image.size[1],int(u*(image.size[0]-1))%image.size[0],:3])
    color=np.median(colors,axis=0)
    color=np.where(color<=.04045,color/12.92,((color+.055)/1.055)**2.4)
    color_dir=ASSETS/sex;color_dir.mkdir(parents=True,exist_ok=True)
    (color_dir/'HandColor.json').write_text(json.dumps({'r':float(color[0]),'g':float(color[1]),'b':float(color[2]),'a':1.0}))
    skin=bpy.data.materials.new('Rps'+sex+'_Hands');skin.use_nodes=True
    bsdf=skin.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*[float(c) for c in color],1)
    bsdf.inputs['Roughness'].default_value=.64
    wrist={};wrist_radii={};wrist_weights={}
    for side,sign in [('Right',-1),('Left',1)]:
        wrist_z=arm.data.bones[side+'Hand'].head_local.z
        band_verts=[v for v in mesh.data.vertices if abs(v.co.x-sign*cut)<.004 and (sex=='Male' or abs(v.co.z-wrist_z)<.028)]
        # Intersect edges with the wrist plane: remeshed triangles can leave
        # only one original vertex near a plane, which gives a zero radius.
        band=[]
        for edge in mesh.data.edges:
            a,b=(mesh.data.vertices[i].co for i in edge.vertices)
            if (a.x-sign*cut)*(b.x-sign*cut)<0:
                p=a.lerp(b,(sign*cut-a.x)/(b.x-a.x))
                if sex=='Male' or abs(p.z-wrist_z)<.028:band.append(p)
        assert len(band)>=8, (sex,side,'Insufficient wrist cross section',len(band))
        wrist[side]=Vector((sign*cut,(min(v.y for v in band)+max(v.y for v in band))/2,(min(v.z for v in band)+max(v.z for v in band))/2))
        wrist_radii[side]=((max(v.y for v in band)-min(v.y for v in band))/2,(max(v.z for v in band)-min(v.z for v in band))/2)
        values={}
        for v in band_verts:
            for g in v.groups:
                name=mesh.vertex_groups[g.group].name;values[name]=values.get(name,0)+g.weight/len(band_verts)
        total=sum(values.values());wrist_weights[side]={n:w/total for n,w in values.items()}
    bm=bmesh.new();bm.from_mesh(mesh.data)
    for sign in (-1,1):
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,
            plane_co=(sign*cut,0,0),plane_no=(sign,0,0),clear_outer=True)
    bm.to_mesh(mesh.data);bm.free()
    objects=[]
    for side,sign in [('Right',-1),('Left',1)]:
        roots=[points[(side,f)][0] for f in ('Index','Middle','Ring','Pinky')]
        knuckle=sum(roots,Vector())/4
        center=wrist[side].lerp(knuckle,.54)
        center.z=(max(p.z for p in roots)+min(p.z for p in roots))/2
        center.y=(center.y+sum(p.y for p in roots)/4)/2
        rx=abs(knuckle.x-wrist[side].x)*.58+.008
        rz=(max(p.z for p in roots)-min(p.z for p in roots))*.5+.015
        ry=.025 if sex=='Male' else .018
        verts=[];faces=[];around=32;rings=16
        for ring in range(rings+1):
            t=ring/rings
            c=wrist[side].lerp(knuckle,t*1.33-.28)
            swelling=math.sin(min(1,t/.8)*math.pi/2)
            yr=wrist_radii[side][0]*(1-swelling)+ry*swelling
            zr=wrist_radii[side][1]*(1-swelling)+rz*swelling
            if t>.84:
                cap=math.sqrt(max(.005,1-((t-.84)/.16)**2));yr*=cap;zr*=cap
            for k in range(around):
                angle=2*math.pi*k/around;verts.append(c+Vector((0,math.cos(angle)*yr,math.sin(angle)*zr)))
            if ring:
                for k in range(around):
                    a=(ring-1)*around+k;b=(ring-1)*around+(k+1)%around;faces.append((a,b,b+around,a+around))
        faces.append(tuple(reversed(range(around))));faces.append(tuple(rings*around+k for k in range(around)))
        data=bpy.data.meshes.new(side+'Palm');data.from_pydata(verts,[],faces);data.update()
        palm=bpy.data.objects.new(side+'Palm',data);bpy.context.collection.objects.link(palm)
        palm.data.materials.append(skin)
        for name in set(wrist_weights[side])|{side+'Hand'}:palm.vertex_groups.new(name=name)
        for ring in range(rings+1):
            blend=smooth((ring/rings)/.5)
            w={n:value*(1-blend) for n,value in wrist_weights[side].items()}
            w[side+'Hand']=w.get(side+'Hand',0)+blend
            for name,value in w.items():
                if value>.00001:palm.vertex_groups[name].add(list(range(ring*around,(ring+1)*around)),value,'REPLACE')
        objects.append(palm)
        for finger in ('Thumb','Index','Middle','Ring','Pinky'):
            chain=points[(side,finger)]
            lengths=[(chain[j+1]-chain[j]).length for j in range(3)]
            total=sum(lengths);count=32;around=12
            radius=(.013 if finger in ('Index','Middle') else .011 if finger=='Ring' else .0095) if sex=='Male' else (.0085 if finger in ('Index','Middle') else .0078 if finger=='Ring' else .007)
            if finger=='Thumb':radius=.016 if sex=='Male' else .0105
            verts=[];faces=[];weights=[]
            for ring in range(count+1):
                u=-radius*1.2+(total+radius*1.2)*ring/count
                j=0 if u<lengths[0] else 1 if u<sum(lengths[:2]) else 2
                t=(u-sum(lengths[:j]))/lengths[j]
                center=chain[j].lerp(chain[j+1],t);direction=(chain[j+1]-chain[j]).normalized()
                normal=Vector((0,-1,0));other=direction.cross(normal).normalized()
                cap=math.sqrt(max(.012,1-max(0,(u-(total-radius))/radius)**2))
                r=radius*(1-.18*u/total)*cap
                w={side+'Hand':1.0}
                for bone,threshold in enumerate((0,lengths[0],sum(lengths[:2]))):
                    width=radius*.85
                    f=smooth((u-threshold+width)/(2*width))
                    for key in list(w):w[key]*=1-f
                    w[side+'Hand'+finger+str(bone+1)]=f
                for k in range(around):
                    angle=2*math.pi*k/around
                    verts.append(center+normal*math.cos(angle)*r+other*math.sin(angle)*r)
                    weights.append(w.copy())
                if ring:
                    for k in range(around):
                        a=(ring-1)*around+k;b=(ring-1)*around+(k+1)%around
                        faces.append((a,b,b+around,a+around))
            faces.append(tuple(reversed(range(around))))
            faces.append(tuple(count*around+k for k in range(around)))
            data=bpy.data.meshes.new(side+finger);data.from_pydata(verts,[],faces);data.update()
            obj=bpy.data.objects.new(side+finger,data);bpy.context.collection.objects.link(obj);data.materials.append(skin)
            for name in {n for w in weights for n in w}:obj.vertex_groups.new(name=name)
            for i,w in enumerate(weights):
                for name,value in w.items():
                    if value>.00001:obj.vertex_groups[name].add([i],value,'REPLACE')
            objects.append(obj)
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True)
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=mesh;bpy.ops.object.join()
    for poly in mesh.data.polygons:poly.use_smooth=True
    # Recalculate outward normals on the newly constructed closed surfaces.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')

def pose(arm,side='Right',gesture='Relax',raised=1,pump=0):
    for pb in arm.pose.bones:
        pb.rotation_mode='QUATERNION';pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    for s,sign in [('Right',-1),('Left',1)]:
        active=s==side
        k=raised if active else 0
        upper=Vector((sign*.28,-.05,-.96)).lerp(Vector((sign*.72,-.12,-.68)),k)
        lower=Vector((sign*.08,-.1,-.99)).lerp(Vector((sign*.05,-.62,.78-pump*.45)),k)
        hand=Vector((sign*.08,-.1,-.99)).lerp(Vector((sign*.12,-.08,.99)),k)
        orient(arm,s+'Arm',upper)
        orient(arm,s+'ForeArm',lower)
        orient(arm,s+'Hand',hand)
        fingers(arm,s,gesture if active else 'Relax')
    bpy.context.view_layer.update()

def key_pose(arm,frame_no):
    for pb in arm.pose.bones:
        pb.keyframe_insert('location',frame=frame_no,group=pb.name)
        pb.keyframe_insert('rotation_quaternion',frame=frame_no,group=pb.name)
        pb.keyframe_insert('scale',frame=frame_no,group=pb.name)

def actions(arm):
    arm.animation_data_create()
    for side in ('Right','Left'):
        for gesture in ('Rock','Paper','Scissors'):
            name=gesture if side=='Right' else 'Left'+gesture
            action=bpy.data.actions.new(name);action.use_fake_user=True;arm.animation_data.action=action
            for f,g,raised in [(1,'Rock',1),(7,'Rock',1),(15,gesture,1),(42,gesture,1)]:
                pose(arm,side,g,raised);key_pose(arm,f)
    for side in ('Right','Left'):
        name='CountIn' if side=='Right' else 'LeftCountIn'
        action=bpy.data.actions.new(name);action.use_fake_user=True;arm.animation_data.action=action
        for f,p in [(1,0),(8,1),(16,0),(23,1),(31,0),(38,1),(46,0)]:
            pose(arm,side,'Rock',1,p);key_pose(arm,f)
    action=bpy.data.actions.new('Idle');action.use_fake_user=True;arm.animation_data.action=action
    for f,k in [(1,0),(31,.015),(61,0)]:
        pose(arm,'Right','Relax',k)
        arm.pose.bones['Spine02'].rotation_quaternion=Quaternion((1,0,0),k)
        key_pose(arm,f)
    arm.animation_data.action=None
    for pb in arm.pose.bones:pb.matrix_basis.identity()

def studio(scene):
    scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.color=(.22,.22,.22)
    scene.view_settings.view_transform='Standard'
    for loc,power,size in [((-3,-4,5),350,5),((3,-2,3),200,4)]:
        bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object
        ob.data.energy=power;ob.data.size=size;ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(0,-4,.87));cam=bpy.context.object
    cam.data.type='ORTHO';cam.data.ortho_scale=1.95;scene.camera=cam
    scene.render.resolution_x=768;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    return cam

def main(sex):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'Rps{sex}_Base.blend'))
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=61
    arm=next(o for o in scene.objects if o.type=='ARMATURE');mesh=next(o for o in scene.objects if o.type=='MESH')
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.00008)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for mat in mesh.data.materials:
        if mat and mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type=='BSDF_PRINCIPLED':
                    for link in list(node.inputs['Normal'].links):mat.node_tree.links.remove(link)
    points=create_fingers(arm,mesh,sex)
    rebuild_hands(arm,mesh,sex,points)
    for b in arm.data.bones:
        if 'end' in b.name.lower() or b.name=='headfront':b.use_deform=False
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);mesh.select_set(True)
    bpy.context.view_layer.objects.active=arm
    actions(arm)
    out=ASSETS/sex;out.mkdir(parents=True,exist_ok=True)
    tex=out/'Textures';tex.mkdir(exist_ok=True)
    for image in bpy.data.images:
        if image.size[0]>0 and image.type=='IMAGE':
            image.filepath_raw=str(tex/(image.name+'.png'));image.file_format='PNG'
            if not Path(image.filepath_raw).exists():image.save()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/f'Rps{sex}_Animated.blend'))
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=str(out/f'Rps{sex}.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
       axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
       bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0,path_mode='COPY',embed_textures=True)
    cam=studio(scene)
    for gesture in ('Rock','Paper','Scissors'):
        arm.animation_data.action=bpy.data.actions[gesture];scene.frame_set(30)
        cam.location=(0,-4,.87);cam.rotation_euler=(Vector((0,0,.87))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.95
        scene.render.filepath=str(ROOT/f'Rps{sex}_{gesture}.png');bpy.ops.render.render(write_still=True)
        hand=arm.pose.bones['RightHand'];target=(hand.head+hand.tail)/2
        cam.location=target+Vector((-.08,-1.5,.06));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.29
        scene.render.resolution_x=768;scene.render.resolution_y=768
        scene.render.filepath=str(ROOT/f'Rps{sex}_{gesture}_Hand.png');bpy.ops.render.render(write_still=True)
        scene.render.resolution_y=900
    print('EXPORTED',sex,flush=True)

if __name__=='__main__':
    for sex in (sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Male','Female']):main(sex)
