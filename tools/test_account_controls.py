#!/usr/bin/env python3
"""Security regression tests for R2.1 individual shop/beat accounts on synthetic XML."""
import http.cookiejar,json,os,urllib.request,urllib.error,secrets
ROOT="http://127.0.0.1:5091/api"
class Client:
 def __init__(self,username,password):
  self.name=username
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  self.csrf=''
  info=self.post('/auth/login',{"username":username,"password":password})
  self.role=info["role"]
  self.csrf=info["csrf"]
 def request(self,path,method='GET',body=None):
  headers={'Accept':'application/json'}
  if method!='GET':headers.update({'Content-Type':'application/json','X-CSRF':self.csrf})
  q=urllib.request.Request(ROOT+path,data=json.dumps(body or {}).encode() if method!='GET' else None,
                           headers=headers,method=method)
  with self.opener.open(q,timeout=20) as result:return json.load(result)
 def post(self,path,body=None):return self.request(path,'POST',body)
 def get(self,path):return self.request(path)
 def blocked(self,path,body=None,expected=400):
  try:self.post(path,body);raise AssertionError('Unauthorized/invalid operation succeeded '+path)
  except urllib.error.HTTPError as e:assert e.code==expected,(path,e.code,e.read()[:180])
owner=Client('owner',os.environ['BADRI_PASS_OWNER'])
sales=Client('salesperson',os.environ['BADRI_PASS_SALESMAN'])
retailer=Client('retailer',os.environ['BADRI_PASS_RETAILER'])
shops=owner.get('/state')['retailers']
kumar=next(s for s in shops if s['name']=='Kumar Maligai')
kuriji=next(s for s in shops if s['name']=='Kuriji Store')
gk=next(s for s in shops if s['beat']=='GK Road')
prod=owner.get('/state')['products']
harpic=next(p for p in prod if p['name']=='HARPIC 450ML')
det=next(p for p in prod if p['name']=='DET ASL 60ML')

shop_pwd=secrets.token_urlsafe(24)
retail=owner.post('/accounts',{'username':'kuriji.test','role':'Retailer',
    'password':shop_pwd,'retailerId':kuriji['id']})
assert retail['role']=='Retailer' and retail['retailerId']==kuriji['id']
assert 'password' not in retail and 'hash' not in retail
owner.blocked('/accounts',{'username':'kuriji.test','role':'Retailer',
    'password':secrets.token_urlsafe(24),'retailerId':kuriji['id']})
sales.blocked('/accounts',{'username':'fake.team','role':'Salesperson','password':shop_pwd,'beat':'All'},403)
shop=Client('kuriji.test',shop_pwd)
assert [s['name'] for s in shop.get('/state')['retailers']]==['Kuriji Store']
assert not any(x['retailerId']==kumar['id'] for x in shop.get('/state')['bills'])
order=shop.post('/orders',{'retailerId':kumar['id'],'productId':harpic['id'],
      'quantity':2,'note':'Tomorrow morning'})
assert order['retailerId']==kuriji['id']
assert all(x['retailerId']==kuriji['id'] for x in shop.get('/state')['orders'])
assert not any('hash' in str(x) or 'password' in str(x) for x in owner.get('/state')['accounts'])

gkpass=secrets.token_urlsafe(24)
bompass=secrets.token_urlsafe(24)
owner.post('/accounts',{'username':'gk.runner','role':'Salesperson','beat':'GK Road','password':gkpass})
owner.post('/accounts',{'username':'bom.runner','role':'Salesperson','beat':'Bommidi','password':bompass})
gkstaff=Client('gk.runner',gkpass)
bom=Client('bom.runner',bompass)
assert all(x['beat']=='GK Road' for x in gkstaff.get('/state')['retailers'])
assert all(x['beat']=='Bommidi' for x in bom.get('/state')['retailers'])
assert all(x['retailerId']!=kumar['id'] for x in gkstaff.get('/state')['bills'])
gkstaff.blocked('/orders',{'retailerId':kumar['id'],'productId':harpic['id'],'quantity':1},400)
ok=gkstaff.post('/orders',{'retailerId':gk['id'],'productId':harpic['id'],'quantity':1})
assert ok['retailerId']==gk['id']
old_bill=next(x for x in owner.get('/state')['bills'] if x['retailerId']==kumar['id'])
gkstaff.blocked('/collections',{'billId':old_bill['id'],'amount':10,
                                'mode':'Cash','idempotencyKey':secrets.token_hex(12)},400)
old_delivery=next(x for x in owner.get('/state')['deliveries'] if x['retailerId']==kumar['id'])
gkstaff.blocked('/deliveries/'+old_delivery['id']+'/confirm',
                {'receiver':'Not our beat','accepted':0},400)

# The order-linked bill cannot dispatch a different SKU or excess quantity.
pending=owner.post('/orders',{'retailerId':kumar['id'],'productId':harpic['id'],
                             'quantity':2,'note':'dispatch check'})
owner.post('/orders/'+pending['id']+'/decision',{'approve':True,'note':'Ready'})
bill=owner.post('/sales-bills',{'retailerId':kumar['id'],'billNo':'TEST-BEAT-ORDER-101',
                               'amount':100,'dueDate':'2026-11-25','orderId':pending['id']})
owner.blocked('/deliveries',{'billId':bill['id'],'productId':det['id'],'quantity':2})
owner.blocked('/deliveries',{'billId':bill['id'],'productId':harpic['id'],'quantity':3})

# Aging groups are computed exclusively from posted invoices and cleared receipts.
state=owner.get('/state')
assert all(x['bucket'] in ('Current','1–7 days','8–30 days','31–60 days','60+ days') for x in state['aging'])
assert abs(sum(float(x['outstanding']) for x in state['aging'])-float(state['totals']['receivable']))<0.01
assert any(x['billNo']=='TEST-BEAT-ORDER-101' for x in state['aging'])

owner.post('/accounts/kuriji.test/disable',{})
try:shop.get('/state');raise AssertionError('Disabled login retained valid session')
except urllib.error.HTTPError as e:assert e.code==401,e.code
# Revocation + reset must not restore the old password.
new_pwd=secrets.token_urlsafe(24)
owner.post('/accounts/kuriji.test/password',{'password':new_pwd})
try:Client('kuriji.test',shop_pwd);raise AssertionError('Old password still works')
except urllib.error.HTTPError as e:assert e.code==401
shop=Client('kuriji.test',new_pwd)
assert shop.get('/state')['retailers'][0]['id']==kuriji['id']
print('PASS: multiple retailer/shop logins, beat-scoped sales staff, session revocation, reset, dispatch guards, AR aging')
