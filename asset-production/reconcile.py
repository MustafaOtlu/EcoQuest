from produce import ROOT, api, save, event, get
import json

m=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
info=get('https://cloud.leonardo.ai/api/rest/v1/me','LEONARDO')
balance=info['user_details'][0]['apiPaidTokens']
previous=m['budgets']['leonardo'].get('last_reconciled_balance',m['budgets']['leonardo']['account_api_paid_tokens_observed'])
pending=[a for a in m['assets'] if a['reference'].get('request_id') and a['reference'].get('actual_cost') is None]
with api.connect(ROOT/'jobs.sqlite') as db:
    assert len(pending)==1, 'Require one outstanding Leonardo submission for balance attribution.'
    a=pending[0]; s=a['reference']; delta=previous-balance
    assert 0<=delta<=s['reservation'], 'Unexpected balance change: retain reservation.'
    body=json.loads((ROOT/a['category']/a['id']/'leonardo-submission.json').read_text())['sdGenerationJob']
    s.update(actual_cost=delta,cost_basis='Observed API-token balance delta across the only known outstanding submission; concurrent external account activity cannot be excluded.',reported_cost=body.get('cost'))
    db.execute('UPDATE jobs SET cost=? WHERE key=?',(delta,s['request_key'])); db.commit()
    m['budgets']['leonardo'].update(last_reconciled_balance=balance,quote_status='Live quote unavailable; provider USD charge and observed API-token balance deltas recorded separately.')
    event('billing_reconciled',asset=a['id'],tokens_balance_delta=delta,balance_before=previous,balance_after=balance,provider_reported_cost=body.get('cost'),attribution='Only known outstanding submission; external concurrent activity not independently ruled out.')
    save(m,db); print(json.dumps({'asset':a['id'],'tokens_balance_delta':delta,'provider_reported_cost':body.get('cost')}))
