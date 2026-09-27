"""Inspect downloaded GLB geometry and textures without changing source models."""
import collections, io, json, math, struct
from pathlib import Path
from PIL import Image
from produce import ROOT, api, save, event

def inspect(path):
    raw=path.read_bytes()
    magic,version,size=struct.unpack_from('<III',raw)
    assert magic==0x46546c67 and version==2 and size==len(raw)
    off=12; doc=None; blob=None
    while off<len(raw):
        n,t=struct.unpack_from('<II',raw,off); off+=8; data=raw[off:off+n]; off+=n
        if t==0x4e4f534a: doc=json.loads(data)
        if t==0x004e4942: blob=data
    assert doc and blob
    def values(i):
        a=doc['accessors'][i]; v=doc['bufferViews'][a['bufferView']]
        fmt={5120:'b',5121:'B',5122:'h',5123:'H',5125:'I',5126:'f'}[a['componentType']]
        count={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        stride=v.get('byteStride',struct.calcsize(fmt)*count)
        start=v.get('byteOffset',0)+a.get('byteOffset',0)
        return [struct.unpack_from('<'+fmt*count,blob,start+j*stride) for j in range(a['count'])]
    primitives=[]; positions=[]
    for mesh in doc.get('meshes',[]):
        for p in mesh['primitives']:
            assert p.get('mode',4)==4
            at=p['attributes']; pos=values(at['POSITION']); positions.extend(pos)
            inds=[x[0] for x in values(p['indices'])] if 'indices' in p else list(range(len(pos)))
            assert len(inds)%3==0 and all(0<=i<len(pos) for i in inds)
            norm=values(at['NORMAL']) if 'NORMAL' in at else []
            uv=values(at['TEXCOORD_0']) if 'TEXCOORD_0' in at else []
            assert all(math.isfinite(x) for vector in pos+norm+uv for x in vector)
            degenerate=0
            for j in range(0,len(inds),3):
                a,b,c=[pos[i] for i in inds[j:j+3]]
                u=[b[k]-a[k] for k in range(3)];v=[c[k]-a[k] for k in range(3)]
                cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
                if sum(x*x for x in cross)<1e-18: degenerate+=1
            primitives.append({'vertices':len(pos),'triangles':len(inds)//3,'material_index':p.get('material'),'normals_present':len(norm)==len(pos),'uv_present':len(uv)==len(pos),'degenerate_triangles':degenerate,'nonunit_normals':sum(abs(sum(x*x for x in n)-1)>.05 for n in norm)})
    images=[]
    for im in doc.get('images',[]):
        if 'bufferView' in im:
            v=doc['bufferViews'][im['bufferView']]; start=v.get('byteOffset',0)
            image=Image.open(io.BytesIO(blob[start:start+v['byteLength']]))
            images.append({'width':image.width,'height':image.height,'format':image.format})
        else: images.append({'uri':im.get('uri')})
    return {'triangles':sum(p['triangles'] for p in primitives),'vertices':sum(p['vertices'] for p in primitives),'mesh_count':len(doc.get('meshes',[])),'material_count':len(doc.get('materials',[])),'primitives':primitives,'embedded_images':images,'raw_bounds_min':[min(p[k] for p in positions) for k in range(3)],'raw_bounds_max':[max(p[k] for p in positions) for k in range(3)],'nodes':[{k:n[k] for k in ('name','mesh','translation','rotation','scale','matrix') if k in n} for n in doc.get('nodes',[])],'engine_import_verified':False}

if __name__=='__main__':
    m=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
    with api.connect(ROOT/'jobs.sqlite') as db:
        for a in m['assets']:
            folder=ROOT/a['category']/a['id']; path=folder/'raw'/'model.glb'
            if not path.exists(): continue
            report=inspect(path)
            report['external_textures']=[{'file':p.name,'size':list(Image.open(p).size)} for p in (folder/'raw').glob('texture*.png')]
            out=folder/'quality-report.json'; out.write_text(json.dumps(report,indent=2),encoding='utf-8')
            a['model']['geometry_review']=report
            a['model']['quality_report']=str(out.relative_to(ROOT.parent))
            event('geometry_texture_inspection',asset=a['id'],triangles=report['triangles'],materials=report['material_count'],embedded_images=report['embedded_images'])
            print(json.dumps({'asset':a['id'],'triangles':report['triangles'],'materials':report['material_count'],'images':report['embedded_images'],'primitives':report['primitives']}))
        save(m,db)
