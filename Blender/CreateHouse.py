"""
CreateHouse.py - GINEBRO GAMES - Visita Virtual 3D
Genera l'estructura arquitectonica (4 habitacions + passadis, portes i llindes)
i l'exporta a FBX per a Unity.

Us des de Blender (GUI): obre la pestanya Scripting, carrega aquest fitxer i prem Run.
Us en mode consola (el que fa servir l'editor de Unity automaticament):
    blender -b --factory-startup -P CreateHouse.py -- <sortida.fbx> [<sortida.blend>]

Si s'executa amb Python normal (sense bpy) genera un .obj equivalent (fallback).

Coordenades: l'esquema esta pensat en coordenades Unity (X dreta, Y amunt, Z endavant).
A Blender es converteix a (X, -Z, Y) i l'exportador FBX ho torna a Unity.
La planta es simetrica respecte als dos eixos, aixi que la conversio d'eixos no pot
desquadrar res.
"""
import os
import sys

# ---------------------------------------------------------------- Mides (metres)
WallHeight = 3.0
WallThickness = 0.2
DoorWidth = 1.2
DoorHeight = 2.2
HalfWidth = 7.0          # X: -7 .. 7
HalfDepth = 7.2          # Z: -7.2 .. 7.2
CorridorHalf = 1.2       # passadis Z: -1.2 .. 1.2
DoorX = 3.5              # portes a X = -3.5 i X = 3.5

Boxes = []  # (name, material, (minx, miny, minz), (maxx, maxy, maxz))


def AddBox(name, material, mn, mx):
    Boxes.append((name, material, mn, mx))


def WallAlongX(name, z, x0, x1, openings, material="Wall"):
    """Paret paral.lela a l'eix X amb obertures [(centreX, amplada)]."""
    t = WallThickness / 2
    cuts = sorted(openings)
    cursor = x0
    idx = 0
    for cx, w in cuts:
        a, b = cx - w / 2, cx + w / 2
        if a > cursor:
            AddBox(f"{name}Segment{idx}", material, (cursor, 0, z - t), (a, WallHeight, z + t)); idx += 1
        AddBox(f"{name}Lintel{idx}", material, (a, DoorHeight, z - t), (b, WallHeight, z + t)); idx += 1
        cursor = b
    if cursor < x1:
        AddBox(f"{name}Segment{idx}", material, (cursor, 0, z - t), (x1, WallHeight, z + t))


def WallAlongZ(name, x, z0, z1, material="Wall"):
    t = WallThickness / 2
    AddBox(name, material, (x - t, 0, z0), (x + t, WallHeight, z1))


def BuildLayout():
    Boxes.clear()
    hw, hd, ch, t = HalfWidth, HalfDepth, CorridorHalf, WallThickness / 2
    # Terres (un per estanca, per poder posar materials diferents)
    AddBox("FloorLivingRoom", "FloorWood", (-hw, -0.1, -hd), (0, 0, -ch))
    AddBox("FloorKitchen", "FloorTile", (0, -0.1, -hd), (hw, 0, -ch))
    AddBox("FloorBedroom", "FloorWood", (-hw, -0.1, ch), (0, 0, hd))
    AddBox("FloorStudy", "FloorTile", (0, -0.1, ch), (hw, 0, hd))
    AddBox("FloorHallway", "FloorHallway", (-hw, -0.1, -ch), (hw, 0, ch))
    # Sostre
    AddBox("CeilingMain", "Ceiling", (-hw - t, WallHeight, -hd - t), (hw + t, WallHeight + 0.1, hd + t))
    # Parets exteriors (recinte tancat)
    WallAlongX("WallOuterNorth", hd, -hw - t, hw + t, [])
    WallAlongX("WallOuterSouth", -hd, -hw - t, hw + t, [])
    WallAlongZ("WallOuterEast", hw, -hd, hd)
    WallAlongZ("WallOuterWest", -hw, -hd, hd)
    # Parets del passadis amb portes cap a les 4 habitacions
    WallAlongX("WallHallwayNorth", ch, -hw, hw, [(-DoorX, DoorWidth), (DoorX, DoorWidth)])
    WallAlongX("WallHallwaySouth", -ch, -hw, hw, [(-DoorX, DoorWidth), (DoorX, DoorWidth)])
    # Envans entre habitacions
    WallAlongZ("WallDividerNorth", 0, ch + t, hd - t)
    WallAlongZ("WallDividerSouth", 0, -hd + t, -ch - t)
    # Socols (detall)
    return Boxes


# ---------------------------------------------------------------- Geometria
def BoxFaces(mn, mx):
    """Retorna llista de cares (4 vertexs, normal, eixos uv) amb UV en metres."""
    x0, y0, z0 = mn
    x1, y1, z1 = mx
    return [
        ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1), (0, 1)),   # +Z
        ([(x1, y0, z0), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0)], (0, 0, -1), (0, 1)),  # -Z
        ([(x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1)], (1, 0, 0), (2, 1)),   # +X
        ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0), (2, 1)),  # -X
        ([(x0, y1, z1), (x1, y1, z1), (x1, y1, z0), (x0, y1, z0)], (0, 1, 0), (0, 2)),   # +Y
        ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0), (0, 2)),  # -Y
    ]


def Uv(v, axes):
    return (v[axes[0]], v[axes[1]])


# ---------------------------------------------------------------- Exportacio OBJ (fallback)
def ExportObj(path):
    lines = ["# GINEBRO GAMES - House (generat per CreateHouse.py)", "mtllib House.mtl"]
    vi = 1
    for name, mat, mn, mx in BuildLayout():
        lines.append(f"o {name}")
        lines.append(f"usemtl {mat}")
        for quad, n, axes in BoxFaces(mn, mx):
            # Unity inverteix X en importar OBJ: escrivim -x i invertim l'ordre per conservar normals
            for v in quad:
                lines.append(f"v {-v[0]:.4f} {v[1]:.4f} {v[2]:.4f}")
            for v in quad:
                u, w = Uv(v, axes)
                lines.append(f"vt {u:.4f} {w:.4f}")
            lines.append(f"vn {-n[0]} {n[1]} {n[2]}")
            k = (vi - 1) // 4 + 1
            a, b, c, d = vi, vi + 1, vi + 2, vi + 3
            lines.append(f"f {a}/{a}/{k} {d}/{d}/{k} {c}/{c}/{k} {b}/{b}/{k}")
            vi += 4
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    mtl = os.path.join(os.path.dirname(path), "House.mtl")
    with open(mtl, "w", encoding="utf-8") as f:
        for m in ["Wall", "FloorWood", "FloorTile", "FloorHallway", "Ceiling"]:
            f.write(f"newmtl {m}\nKd 0.8 0.8 0.8\n\n")


# ---------------------------------------------------------------- Blender
def BuildInBlender(fbxPath, blendPath=None):
    import bpy
    import bmesh

    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)

    root = bpy.data.objects.new("House", None)
    bpy.context.scene.collection.objects.link(root)

    materials = {}

    def GetMaterial(name):
        if name not in materials:
            m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            materials[name] = m
        return materials[name]

    def ToBlender(v):
        # Unity (x, y, z) -> Blender (x, -z, y)
        return (v[0], -v[2], v[1])

    for name, matName, mn, mx in BuildLayout():
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        uvLayer = bm.loops.layers.uv.new("UVMap")
        for quad, n, axes in BoxFaces(mn, mx):
            verts = [bm.verts.new(ToBlender(v)) for v in quad]
            face = bm.faces.new(verts)
            for loop, v in zip(face.loops, quad):
                loop[uvLayer].uv = Uv(v, axes)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        bm.normal_update()
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(GetMaterial(matName))
        obj = bpy.data.objects.new(name, mesh)
        obj.parent = root
        bpy.context.scene.collection.objects.link(obj)

    # Les normals de cada cara ja surten cap enfora (ordre antihorari en Blender)
    os.makedirs(os.path.dirname(fbxPath), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=fbxPath,
        use_selection=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        object_types={"EMPTY", "MESH"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=blendPath)
    print("CreateHouse: exportat", fbxPath)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    try:
        import bpy  # noqa: F401
        insideBlender = True
    except ImportError:
        insideBlender = False

    here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
    projectRoot = os.path.dirname(here)
    if insideBlender:
        fbx = args[0] if len(args) > 0 else os.path.join(projectRoot, "Assets", "Models", "House.fbx")
        blend = args[1] if len(args) > 1 else os.path.join(here, "House.blend")
        BuildInBlender(fbx, blend)
    else:
        out = args[0] if args else os.path.join(projectRoot, "Assets", "Models", "Fallback", "HouseFallback.obj")
        ExportObj(out)
        print("CreateHouse: OBJ fallback a", out)
