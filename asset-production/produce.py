"""Project production driver; no automatic paid retries. Never print credentials."""
import argparse, datetime, importlib.util, json, os, sqlite3, subprocess, sys, urllib.request
from pathlib import Path
from preflight import get

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('asset_api', 'C:/Görsel Klasörü/.agents/skills/game-assets/scripts/asset_api.py')
api = importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)

def event(kind, **data):
    with (ROOT/'journal.jsonl').open('a',encoding='utf-8') as f:
        f.write(json.dumps({'time':datetime.datetime.now(datetime.timezone.utc).isoformat(),'event':kind,**data},ensure_ascii=False)+'\n')

def save(m, db):
    for provider,b in m['budgets'].items():
        actual=0; reserved=0
        for a in m['assets']:
            s=a['reference' if provider=='leonardo' else 'model']
            if s.get('actual_cost') is not None: actual+=s['actual_cost']
            elif s.get('request_key'):
                row=db.execute('SELECT cost FROM jobs WHERE key=?',(s['request_key'],)).fetchone()
                if row: reserved+=row[0]
        b.update(spent_this_task=actual,reserved=reserved,remaining=b['limit']-actual-reserved)
    tmp=ROOT/'manifest.tmp'
    tmp.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    tmp.replace(ROOT/'manifest.json')

def download(url, path):
    if path.exists(): return
    assert url.startswith('https://')
    path.parent.mkdir(parents=True,exist_ok=True)
    req=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'})
    with urllib.request.urlopen(req,timeout=60) as r, path.with_suffix(path.suffix+'.part').open('wb') as f:
        while chunk:=r.read(1024*1024): f.write(chunk)
    path.with_suffix(path.suffix+'.part').replace(path)

def main():
    p=argparse.ArgumentParser(); p.add_argument('action',choices=['submit','poll','review']); p.add_argument('asset'); p.add_argument('provider',choices=['leonardo','meshy']); p.add_argument('--note')
    args=p.parse_args()
    m=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
    a=next(a for a in m['assets'] if a['id']==args.asset)
    stage=a['reference' if args.provider=='leonardo' else 'model']
    folder=ROOT/a['category']/a['id']; folder.mkdir(parents=True,exist_ok=True)
    key=a['id']+':'+args.provider+':1'
    with api.connect(ROOT/'jobs.sqlite') as db:
        if not db.execute('SELECT COUNT(*) FROM budgets').fetchone()[0]:
            db.executemany('INSERT INTO budgets VALUES(?,?,?)',[('leonardo',200,'API tokens'),('meshy',100,'credits')]); db.commit()
        if args.action=='review':
            assert args.provider=='leonardo' and stage.get('file') and args.note
            stage.update(review=args.note,review_status='accepted'); event('saved_reference_visually_accepted',asset=a['id'],file=stage['file'],note=args.note); save(m,db); return
        if args.action=='submit':
            if args.provider=='leonardo':
                payload={'modelId':'de7d3faf-762f-48e0-b3b7-9d0ac3a3fcf3','prompt':a['prompt'],'num_images':1,'width':768,'height':768,'public':False,'alchemy':False,'enhancePrompt':False,'contrast':3,'ultra':False}
                cost=100; review=''
            else:
                assert a['reference'].get('review_status')=='accepted', 'Saved reference must be visually reviewed.'
                payload={'image_url':a['reference']['url'],'model_type':'smart-topology','ai_model':'meshy-t2','target_polycount':a['target_faces'],'should_texture':True,'texture_resolution':'2k','target_formats':['glb','fbx'],'multi_view_thumbnails':True}
                cost=15; review=a['reference']['review']
            if db.execute('SELECT 1 FROM jobs WHERE key=?',(key,)).fetchone(): raise ValueError('Already recorded; poll instead.')
            api.reserve(db,key,args.provider,a['id'],cost,payload,review)
            stage.update(request_key=key,status='uncertain',reservation=cost,payload=payload)
            m.update(status='in_production',blockers=[])
            event('reserved_before_submission',asset=a['id'],provider=args.provider,amount=cost,unit=m['budgets'][args.provider]['unit'],basis='Conservative 100-token contingency reservation for one economical Phoenix fast image; exact quote unavailable, not a verified charge.' if args.provider=='leonardo' else 'Official T2 + 2K price: 15 credits')
            save(m,db)
            result=api.api(args.provider,payload=payload)
            (folder/(args.provider+'-submission.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
            db.execute('UPDATE jobs SET response=? WHERE key=?',(json.dumps(result),key)); db.commit()
            body=result.get('sdGenerationJob',{}) if args.provider=='leonardo' else result
            task=body.get('generationId') if args.provider=='leonardo' else result.get('result')
            if not isinstance(task,str) or not task: raise ValueError('Ambiguous submission; preserve reservation and inspect response.')
            stage.update(request_id=task,status='submitted',submission_response=str((folder/(args.provider+'-submission.json')).relative_to(ROOT.parent)))
            actual=body.get('apiCreditCost') if args.provider=='leonardo' else None
            if actual is not None:
                stage['actual_cost']=float(actual)
                db.execute('UPDATE jobs SET cost=? WHERE key=?',(float(actual),key))
            db.execute('UPDATE jobs SET state=?,task=? WHERE key=?',('submitted',task,key)); db.commit()
            event('submitted',asset=a['id'],provider=args.provider,request_id=task,actual_cost=stage.get('actual_cost'),billing=body.get('cost')); save(m,db)
            print(json.dumps({'asset':a['id'],'provider':args.provider,'status':'submitted','actual_cost':stage.get('actual_cost'),'request_id':task})); return
        row=db.execute('SELECT task FROM jobs WHERE key=?',(key,)).fetchone()
        assert row and row[0], 'Missing job ID; reconcile without retry.'
        subprocess.run(['powershell.exe','-NoProfile','-File',str(ROOT/'fetch-status.ps1'),'-AssetId',a['id'],'-Provider',args.provider,'-EnvPath','C:/Görsel Klasörü/.env'],check=True)
        result=json.loads((folder/(args.provider+'-fetched.json')).read_text(encoding='utf-8-sig'))
        body=result.get('generations_by_pk',{}) if args.provider=='leonardo' else result
        status=body.get('status','unknown')
        db.execute('UPDATE jobs SET state=?,response=? WHERE key=?',(status,json.dumps(result),key)); db.commit()
        (folder/(args.provider+'-status.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
        stage['status']=status
        actual=body.get('apiCreditCost') if args.provider=='leonardo' else body.get('consumed_credits')
        if actual is not None:
            stage['actual_cost']=float(actual); db.execute('UPDATE jobs SET cost=? WHERE key=?',(float(actual),key)); db.commit()
        if status in ('COMPLETE','SUCCEEDED'):
            if args.provider=='leonardo':
                images=body.get('generated_images',[]); assert len(images)==1
                url=images[0]['url']; path=folder/'references'/'reference.jpg'
                download(url,path); stage.update(url=url,file=str(path.relative_to(ROOT.parent)))
                if stage['file'] not in a['files']: a['files'].append(stage['file'])
            else:
                outputs={}
                for fmt,url in body.get('model_urls',{}).items():
                    if url and fmt in ('glb','fbx'): outputs['model.'+fmt]=url
                if body.get('thumbnail_url'): outputs['preview.png']=body['thumbnail_url']
                for view,url in body.get('thumbnail_urls',{}).items():
                    if url: outputs['view-'+view+'.png']=url
                for i,tex in enumerate(body.get('texture_urls',[])):
                    for kind,url in tex.items():
                        if url: outputs[f'texture-{i}-{kind}.png']=url
                for filename,url in outputs.items():
                    path=folder/'raw'/filename; download(url,path)
                    rel=str(path.relative_to(ROOT.parent))
                    if rel not in a['files']: a['files'].append(rel)
                a['integration_status']='generated_pending_review_and_import'
        event('status',asset=a['id'],provider=args.provider,status=status,actual_cost=stage.get('actual_cost')); save(m,db)
        print(json.dumps({'asset':a['id'],'provider':args.provider,'status':status,'progress':body.get('progress'),'actual_cost':stage.get('actual_cost'),'files':a['files']}))

if __name__=='__main__':
    try: main()
    except Exception as e:
        event('driver_error',error_type=type(e).__name__,note='Inspect journal and job state; never blindly resubmit.')
        if hasattr(e,'read'):
            msg=e.read().decode('utf-8',errors='replace')
            for key in ('LEONARDO_API_KEY','MESHY_API_KEY'):
                if os.environ.get(key): msg=msg.replace(os.environ[key],'[REDACTED]')
            print(msg[:1500])
        print('Stopped:',type(e).__name__,getattr(e,'code',''),'; no automatic retry.'); sys.exit(1)
