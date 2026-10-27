"""Run with Blender --background --python inspect_character.py -- model.fbx report.json."""
import bpy
import json
import sys
from pathlib import Path

args = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
source = Path(args[0])
if source.suffix.lower() == '.fbx':
    bpy.ops.import_scene.fbx(filepath=str(source))
else:
    bpy.ops.import_scene.gltf(filepath=str(source))
report = {'source': str(source), 'objects': [], 'actions': [a.name for a in bpy.data.actions]}
for obj in bpy.context.scene.objects:
    item = {'name': obj.name, 'type': obj.type, 'location': list(obj.location),
            'rotation': list(obj.rotation_euler), 'scale': list(obj.scale),
            'dimensions': list(obj.dimensions)}
    if obj.type == 'ARMATURE':
        item['bones'] = [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                          'head': list(b.head_local), 'tail': list(b.tail_local),
                          'matrix': [list(row) for row in b.matrix_local]} for b in obj.data.bones]
    if obj.type == 'MESH':
        item.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                    materials=[m.name for m in obj.data.materials],
                    vertex_groups=[g.name for g in obj.vertex_groups])
    report['objects'].append(item)
Path(args[1]).write_text(json.dumps(report, indent=2), encoding='utf-8')
print('CHARACTER_REPORT', args[1])
