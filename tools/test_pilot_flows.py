#!/usr/bin/env python3
"""Six-role XML pilot integration smoke test. Synthetic transactions only."""
import http.cookiejar,json,os,urllib.request,urllib.error,uuid
BASE='http://127.0.0.1:5091'
class U:
 def __init__(self,n,k):
  self.n=n
  self.o=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  self.csrf=''
  self.role=self.post('/auth/login',{'username':n,'password':os.environ['BADRI_PASS_'+k]})['role']
  self.csrf=self.get('/auth/session')['csrf']
 def call(self,path,method='GET',body=None):
  hdr={'Accept':'application/json'}
  if method!='GET':hdr.update({'Content-Type':'application/json','X-CSRF':self.csrf})
  req=urllib.request.Request(BASE+'/api'+path,method=method,headers=hdr,
    data=json.dumps(body or {}).encode() if method!='GET' else None)
  with self.o.open(req) as response:return json.load(response)
 def get(self,path):return self.call(path)
 def post(self,path,b=None):return self.call(path,'POST',b)
 def blocked(self,path,b=None,status=400):
  try:self.post(path,b);raise AssertionError(path+' unexpectedly succeeded')
  except urllib.error.HTTPError as e:assert e.code==status,(path,e.code,e.read()[:200])
owner=U('owner.badri','OWNER');accounts=U('accounts.badri','ACCOUNTS')
warehouse=U('warehouse.badri','WAREHOUSE');salesman=U('salesman.santhosh','SALESMAN')
retailer=U('retailer.demo','RETAILER');operator=U('operator.demo','OPERATOR')
assert [owner.role,accounts.role,warehouse.role,salesman.role,retailer.role,operator.role]==['Owner','Accounts','Warehouse','Salesman','Retailer','Operator']
data=accounts.get('/state')
assert len(data['documents'])==7 and len(data['purchases'])==4 and len(data['bills'])==0
purchase=next(x for x in data['purchases'] if x['invoice']=='ME0398')
partial=next(x for x in data['purchases'] if x['invoice']=='5066110913')
accounts.blocked('/purchases/'+partial['id']+'/approve')
accounts.blocked('/purchases/'+purchase['id']+'/approve')
doc=next(x for x in data['documents'] if 'ME0398' in x['title'])
accounts.post('/documents/'+doc['id']+'/review',{'approve':True,'note':'Four invoice lines and printed total reviewed (demo only)'})
accounts.post('/purchases/'+purchase['id']+'/approve')
products={x['name']:x['id'] for x in warehouse.get('/state')['products']}
for sku,qty in [('DET ASL 60ML',200),('DET ASL 125ML',96),('HARPIC 450ML',120),('LYSOL DFC FLORAL 500ML',18)]:
 warehouse.post('/grn',{'purchaseId':purchase['id'],'productId':products[sku],'quantity':qty,'damaged':0,'batch':'B-202611','expiry':'2028-09-01'})
warehouse.post('/purchases/'+purchase['id']+'/complete-grn')
assert float(owner.get('/state')['totals']['supplierPayables'])==26059
payment=accounts.post('/supplierpayments',{'purchaseId':purchase['id'],'amount':1000,'externalReference':'SYNTHETIC-REF'})
accounts.blocked('/supplierpayments/'+payment['id']+'/approve',status=403)
owner.post('/supplierpayments/'+payment['id']+'/approve')
assert float(owner.get('/state')['totals']['supplierPayables'])==25059
shop=next(x for x in owner.get('/state')['retailers'] if x['name']=='Kumar Maligai')
bill=owner.post('/sales-bills',{'retailerId':shop['id'],'billNo':'E2E-PILOT-001','amount':1000,'dueDate':'2026-11-25'})
delivery=warehouse.post('/deliveries',{'billId':bill['id'],'productId':products['HARPIC 450ML'],'quantity':2})
retailer.post('/deliveries/'+delivery['id']+'/confirm',{'receiver':'Shop receiver','accepted':2})
assert any(x['status']=='Delivered' for x in retailer.get('/state')['deliveries'])
key=uuid.uuid4().hex
cash=salesman.post('/collections',{'billId':bill['id'],'amount':400,'mode':'Cash','idempotencyKey':key})
same=salesman.post('/collections',{'billId':bill['id'],'amount':400,'mode':'Cash','idempotencyKey':key})
assert cash['id']==same['id']
upi=salesman.post('/collections',{'billId':bill['id'],'amount':200,'mode':'UPI','idempotencyKey':uuid.uuid4().hex})
assert upi['status']=='Pending bank verification'
assert float(owner.get('/state')['totals']['receivable'])==1000
close=salesman.post('/cashclose',{'date':'2026-11-21','counted':400,'reason':'','notes':{'200':2}})
accounts.blocked('/cashclose/'+close['id']+'/approve',{'reason':'Not owner'},403)
owner.post('/cashclose/'+close['id']+'/approve',{'reason':'Count matched'})
assert float(owner.get('/state')['totals']['receivable'])==600
accounts.post('/collections/'+upi['id']+'/bank-verify',{'reference':'SYNTHETIC-REF','reviewerNote':'Manually reviewed statement demo'})
assert float(owner.get('/state')['totals']['receivable'])==400
warehouse.post('/returns',{'productId':products['HARPIC 450ML'],'qty':1,'reason':'Damaged return for quarantine'})
assert len(owner.get('/state')['returns'])==1
assert 'retailers' not in operator.get('/state')
assert all(x['retailer']=='Kumar Maligai' for x in retailer.get('/state')['bills'])
print('PASS: six roles, invoice page gate, manual review, four-line GRN, AP, dispatch, retailer POD, cash/UPI, cashclose, AR, returns, operator isolation')
