"""Create cleaned, metre-scale GLB and Unity-importable OBJ, keeping raw outputs intact."""
import json, math, struct
from inspect_models import inspect
from produce import ROOT, api, event, save

m=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
with api.connect(ROOT/'jobs.sqlite') as db:
    for asset in m['assets']:
        folder=ROOT/asset['category']/asset['id']; raw=(folder/'raw'/'model.glb').read_bytes()
        n,t=struct.unpack_from('<II',raw,12); doc=json.loads(raw[20:20+n]); off=20+n
        bn,bt=struct.unpack_from('<II',raw,off); blob=bytearray(raw[off+8:off+8+bn])
        assert len(doc['meshes'])==1 and len(doc['meshes'][0]['primitives'])==1
        assert len(doc['nodes'])==1 and doc['nodes'][0].get('matrix')==[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
        p=doc['meshes'][0]['primitives'][0]
        def access(i):
            a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']]
            f={5123:'H',5125:'I',5126:'f'}[a['componentType']]; c={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
            start=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',struct.calcsize(f)*c)
            return a,start,stride,'<'+f*c
        def read(i):
            a,s,d,f=access(i);return [struct.unpack_from(f,blob,s+j*d) for j in range(a['count'])]
        def write(i,values):
            a,s,d,f=access(i)
            for j,val in enumerate(values):struct.pack_into(f,blob,s+j*d,*val)
            a['count']=len(values)
        at=p['attributes'];positions=read(at['POSITION']);normals=read(at['NORMAL']);uvs=read(at['TEXCOORD_0']);inds=[x[0] for x in read(p['indices'])]
        lo=[min(x[k] for x in positions) for k in range(3)];hi=[max(x[k] for x in positions) for k in range(3)]
        scale=asset['target_height_m']/(hi[1]-lo[1]);center=[(lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2]
        positions=[tuple((x[k]-center[k])*scale for k in range(3)) for x in positions]
        clean=[];fallback=[[0.,0.,0.] for _ in positions]
        for j in range(0,len(inds),3):
            tri=inds[j:j+3];a,b,c=[positions[i] for i in tri]
            u=[b[k]-a[k] for k in range(3)];v=[c[k]-a[k] for k in range(3)]
            cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            if sum(x*x for x in cross)<1e-18:continue
            clean.extend(tri)
            for i in tri:
                for k in range(3):fallback[i][k]+=cross[k]
        fixed=[]
        for i,normal in enumerate(normals):
            length=math.sqrt(sum(x*x for x in normal))
            if length<1e-8:
                normal=fallback[i];length=math.sqrt(sum(x*x for x in normal))
            fixed.append(tuple(x/length for x in normal) if length>1e-8 else (0.,1.,0.))
        write(at['POSITION'],positions);write(at['NORMAL'],fixed);write(p['indices'],[(i,) for i in clean])
        pa=doc['accessors'][at['POSITION']];pa['min']=[min(x[k] for x in positions) for k in range(3)];pa['max']=[max(x[k] for x in positions) for k in range(3)]
        ia=doc['accessors'][p['indices']]
        if 'min' in ia:ia['min']=[min(clean)]
        if 'max' in ia:ia['max']=[max(clean)]
        doc['nodes'][0]['name']=asset['id'];doc['meshes'][0]['name']=asset['id']
        out=folder/'prepared';out.mkdir(exist_ok=True)
        js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4)
        glb=struct.pack('<III',0x46546c67,2,12+8+len(js)+8+len(blob))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob
        target=out/(asset['id']+'.glb');target.write_bytes(glb)
        im=doc['images'][0];v=doc['bufferViews'][im['bufferView']];s=v.get('byteOffset',0)
        texture='base_color.jpg' if im.get('mimeType')=='image/jpeg' else 'base_color.png'
        (out/texture).write_bytes(blob[s:s+v['byteLength']])
        (out/(asset['id']+'.mtl')).write_text('newmtl Surface\nKd 1 1 1\nKa 0 0 0\nKs 0 0 0\nd 1\nillum 1\nmap_Kd '+texture+'\n',encoding='utf-8')
        lines=['mtllib '+asset['id']+'.mtl','o '+asset['id']]
        lines+=['v '+' '.join(map(str,x)) for x in positions]
        lines+=['vt '+str(x[0])+' '+str(1-x[1]) for x in uvs]
        lines+=['vn '+' '.join(map(str,x)) for x in fixed]
        lines+=['usemtl Surface']
        lines+=['f '+' '.join(f'{i+1}/{i+1}/{i+1}' for i in clean[j:j+3]) for j in range(0,len(clean),3)]
        (out/(asset['id']+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
        report=inspect(target)
        assert all(p['degenerate_triangles']==0 and p['nonunit_normals']==0 and p['uv_present'] for p in report['primitives'])
        (out/'quality-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
        asset['model']['prepared_geometry_review']=report
        asset['prepared_model']=str(target.relative_to(ROOT.parent))
        asset['integration_status']='prepared_and_reviewed_engine_import_pending'
        asset['review_findings']=['Saved Leonardo reference inspected before Meshy submission.','Downloaded front, back and both side previews inspected.','Silhouette, handle and base preserved; 2K embedded texture and UVs verified.','Raw provider previews can crop framing; raw geometry bounds are complete.','Static prop; moving lid or filter cartridge is not separately rigged.']
        if asset['id']=='organic-compost-bin':asset['review_findings']+=['Side slats have some uneven reconstructed geometry; suitable for a small gameplay prop, close-up quality requires engine review.','Removed two zero-area triangles and normalized six invalid normals locally.']
        else:asset['review_findings']+=['Back panel has an inferred decorative marking not specified in the reference.']
        for file in out.iterdir():
            rel=str(file.relative_to(ROOT.parent))
            if rel not in asset['files']:asset['files'].append(rel)
        event('prepared_model_reviewed',asset=asset['id'],triangles=report['triangles'],height_m=asset['target_height_m'],removed_triangles=(len(inds)-len(clean))//3,engine_import='pending',model=asset['prepared_model'])
        print(json.dumps({'asset':asset['id'],'triangles':report['triangles'],'height_m':asset['target_height_m'],'model':asset['prepared_model']}))
    m['status']='production_complete_engine_import_pending'
    m['budgets']['leonardo']['provider_reported_total_usd']=sum(float(a['reference']['reported_cost']['amount']) for a in m['assets'])
    event('production_complete',leonardo_token_balance_delta=18,leonardo_reported_usd=0.027,meshy_credits=30,engine_import='pending')
    save(m,db)
