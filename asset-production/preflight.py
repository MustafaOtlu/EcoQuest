import json, os, urllib.request, urllib.error
from pathlib import Path

for line in Path('C:/Görsel Klasörü/.env').read_text(encoding='utf-8-sig').splitlines():
    key, sep, value = line.partition('=')
    if sep and key.strip() in ('LEONARDO_API_KEY', 'MESHY_API_KEY'):
        os.environ[key.strip()] = value.strip().strip('"').strip("'")

def get(url, provider=None):
    headers = {'Accept': 'application/json'}
    if provider: headers['Authorization'] = 'Bearer ' + os.environ[provider + '_API_KEY']
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        return {'http_error': e.code}

if __name__ == '__main__':
    info = get('https://cloud.leonardo.ai/api/rest/v1/me', 'LEONARDO')
    def safe_fields(v):
        if isinstance(v, dict):
            return {k: (safe_fields(x) if isinstance(x,(dict,list)) else x) for k,x in v.items() if isinstance(x,(dict,list)) or any(w in k.lower() for w in ('token','credit','balance','plan','cost','error'))}
        if isinstance(v,list): return [safe_fields(x) for x in v]
        return v
    print('Leonardo billing:', json.dumps(safe_fields(info)))
    models = get('https://cloud.leonardo.ai/api/rest/v1/platformModels','LEONARDO')
    if 'custom_models' in models:
        print('Models:', json.dumps([{k:m.get(k) for k in ('id','name','sdVersion')} for m in models['custom_models']]))
    else: print('Model response keys:', list(models))
