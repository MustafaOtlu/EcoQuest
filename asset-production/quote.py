from preflight import *
payload = {'service':'IMAGE_GENERATION','serviceParams':{'IMAGE_GENERATION':{'modelId':'de7d3faf-762f-48e0-b3b7-9d0ac3a3fcf3','imageHeight':768,'imageWidth':768,'numImages':1,'alchemyMode':False,'promptMagic':False}}}
payload['serviceParams']['IMAGE_GENERATION']['inferenceSteps'] = 30
payload['serviceParams']['IMAGE_GENERATION'].update(highResolution=False, isModelCustom=False, isSDXL=True, modelId='1e60896f-3c26-4296-8ecc-53e2afecc132')
req = urllib.request.Request('https://cloud.leonardo.ai/api/rest/v1/pricing-calculator', data=json.dumps(payload).encode(), headers={'Authorization':'Bearer '+os.environ['LEONARDO_API_KEY'],'Content-Type':'application/json'})
try:
    with urllib.request.urlopen(req,timeout=30) as r:
        print(json.dumps(json.load(r)))
except urllib.error.HTTPError as e:
    print('HTTP',e.code)
    msg=e.read().decode()
    for key in ('LEONARDO_API_KEY','MESHY_API_KEY'): msg=msg.replace(os.environ.get(key,'UNSET'),'[REDACTED]')
    print(msg[:2500])
