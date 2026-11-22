#!/usr/bin/env python3
"""R2 three-role XML pilot integration tests. Synthetic data only."""
import http.cookiejar,json,os,urllib.request,urllib.error,uuid
BASE='http://127.0.0.1:5091'

class User:
 def __init__(self,username,key):
  self.name=username
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  self.csrf=''
  self.role=self.post('/auth/login',{'username':username,'password':os.environ['BADRI_PASS_'+key]})['role']
  self.csrf=self.get('/auth/session')['csrf']
 def request(self,path,method='GET',body=None):
  headers={'Accept':'application/json'}
  if method!='GET':
   headers.update({'Content-Type':'application/json','X-CSRF':self.csrf})
  req=urllib.request.Request(BASE+'/api'+path,method=method,headers=headers,
       data=json.dumps(body or {}).encode() if method!='GET' else None)
  with self.opener.open(req) as response:
   return json.load(response)
 def get(self,path):return self.request(path)
 def post(self,path,data=None):return self.request(path,'POST',data)
 def blocked(self,path,data=None,code=403):
  try:self.post(path,data);raise AssertionError(path+' unexpectedly allowed')
  except urllib.error.HTTPError as e:
   assert e.code==code, (path,e.code,e.read()[:250])

owner=User('owner','OWNER')
sales=User('salesperson','SALESMAN')
retailer=User('retailer','RETAILER')
assert [owner.role,sales.role,retailer.role]==['Owner','Salesperson','Retailer']
assert owner.get('/state')['role']=='Owner'
assert sales.get('/state')['role']=='Salesperson'
assert retailer.get('/state')['role']=='Retailer'
data=owner.get('/state')
assert len(data['documents'])==7 and len(data['purchases'])==4
assert len(data['bills'])==0 and len(data['orders'])==0
assert len(retailer.get('/state')['retailers'])==1
assert retailer.get('/state')['retailers'][0]['name']=='Kumar Maligai'
assert 'purchases' not in retailer.get('/state') and 'stock' not in retailer.get('/state')
assert 'suppliers' not in sales.get('/state') and 'payments' not in sales.get('/state')

# Retired users no longer authenticate or authorize.
for legacy,key in [('owner.badri','OWNER'),('salesman.santhosh','SALESMAN'),('retailer.demo','RETAILER')]:
 try:
  owner.post('/auth/login',{'username':legacy,'password':os.environ['BADRI_PASS_'+key]})
  raise AssertionError('Retired username accepted: '+legacy)
 except urllib.error.HTTPError as err:assert err.code==401

shop=next(x for x in data['retailers'] if x['name']=='Kumar Maligai')
other=next(x for x in data['retailers'] if x['name']!='Kumar Maligai')
products={x['name']:x['id'] for x in data['products']}
sku=products['HARPIC 450ML']
retailer.blocked('/sales-bills',{'retailerId':shop['id'],'billNo':'X','amount':1,'dueDate':'2026-11-22'})
sales.blocked('/grn',{'purchaseId':'bad','productId':sku,'quantity':1,'damaged':0,'batch':'Q'})
retailer.blocked('/orders',{'retailerId':other['id'],'productId':sku,'quantity':-3,'note':''},400)

# Retailer cannot spoof retailerId. Requests always belong to Kumar Maligai.
my_order=retailer.post('/orders',{'retailerId':other['id'],'productId':sku,'quantity':2,'note':'Need 2 packs'})
assert my_order['retailer']=='Kumar Maligai'
field_order=sales.post('/orders',{'retailerId':other['id'],'productId':sku,'quantity':1,'note':'Visit tomorrow'})
assert field_order['retailer']!=shop['name']
assert len(retailer.get('/state')['orders'])==1
assert len(sales.get('/state')['orders'])==2
owner.post('/orders/'+my_order['id']+'/decision',{'approve':True,'note':'Reviewed'})
owner.post('/orders/'+field_order['id']+'/decision',{'approve':False,'note':'No stock today'})
retailer.blocked('/orders/'+field_order['id']+'/decision',{'approve':True,'note':'No'},403)
assert next(x for x in retailer.get('/state')['orders'] if x['id']==my_order['id'])['status']=='Accepted - pending billing'

# Supplier source review -> GRN -> stock (Owner does every owner-level task).
data=owner.get('/state')
purchase=next(x for x in data['purchases'] if x['invoice']=='ME0398')
missing=next(x for x in data['purchases'] if x['invoice']=='5066110913')
owner.blocked('/purchases/'+missing['id']+'/approve',{},400)
owner.blocked('/purchases/'+purchase['id']+'/approve',{},400)
doc=next(x for x in data['documents'] if 'ME0398' in x['title'])
owner.post('/documents/'+doc['id']+'/review',{'approve':True,'note':'Source compared to scanned invoice demo'})
owner.post('/purchases/'+purchase['id']+'/approve')
for name,qty in [('DET ASL 60ML',200),('DET ASL 125ML',96),('HARPIC 450ML',120),('LYSOL DFC FLORAL 500ML',18)]:
 owner.post('/grn',{'purchaseId':purchase['id'],'productId':products[name],
                   'quantity':qty,'damaged':0,'batch':'B-202611','expiry':'2028-09-01'})
owner.post('/purchases/'+purchase['id']+'/complete-grn')
assert float(owner.get('/state')['totals']['supplierPayables'])==26059
payment=owner.post('/supplierpayments',{'purchaseId':purchase['id'],'amount':1000,'externalReference':'SYNTHETIC-BANK-REF'})
owner.blocked('/supplierpayments/'+payment['id']+'/approve',{'reason':'short'},400)
owner.post('/supplierpayments/'+payment['id']+'/approve',{'reason':'External bank statement reviewed, pilot only'})
assert float(owner.get('/state')['totals']['supplierPayables'])==25059

# Accepted order -> owner sales bill -> dispatch -> ack -> sales collection -> cash handover.
bill=owner.post('/sales-bills',{'retailerId':shop['id'],'billNo':'PILOT-3ROLE-001','amount':1000,
                              'dueDate':'2026-11-25','orderId':my_order['id']})
assert next(x for x in owner.get('/state')['orders'] if x['id']==my_order['id'])['status']=='Billed - pending dispatch'
delivery=owner.post('/deliveries',{'billId':bill['id'],'productId':sku,'quantity':2})
retailer.post('/deliveries/'+delivery['id']+'/confirm',{'receiver':'Kumar Maligai','accepted':2})
assert any(x['status']=='Delivered' for x in retailer.get('/state')['deliveries'])

# Other retailer delivery cannot be acknowledged by Kumar's account.
bill2=owner.post('/sales-bills',{'retailerId':other['id'],'billNo':'PILOT-3ROLE-OTHER',
                               'amount':100,'dueDate':'2026-11-25'})
delivery2=owner.post('/deliveries',{'billId':bill2['id'],'productId':sku,'quantity':1})
retailer.blocked('/deliveries/'+delivery2['id']+'/confirm',{'receiver':'Wrong shop','accepted':1},400)
assert all(x['retailer']=='Kumar Maligai' for x in retailer.get('/state')['deliveries'])

key=uuid.uuid4().hex
cash=sales.post('/collections',{'billId':bill['id'],'amount':400,'mode':'Cash','idempotencyKey':key})
same=sales.post('/collections',{'billId':bill['id'],'amount':400,'mode':'Cash','idempotencyKey':key})
assert same['id']==cash['id']
upi=sales.post('/collections',{'billId':bill['id'],'amount':200,'mode':'UPI','idempotencyKey':uuid.uuid4().hex})
assert upi['status']=='Pending bank verification'
assert float(owner.get('/state')['totals']['receivable'])==1100
close=sales.post('/cashclose',{'date':'2026-11-22','counted':400,'reason':'','notes':{'200':2}})
retailer.blocked('/cashclose/'+close['id']+'/approve',{'reason':'No'},403)
owner.post('/cashclose/'+close['id']+'/approve',{'reason':'Counted and accepted by owner'})
assert float(owner.get('/state')['totals']['receivable'])==700
owner.post('/collections/'+upi['id']+'/bank-verify',{'reference':'SYNTHETIC-BANK-REF','reviewerNote':'Verified on pilot external bank report'})
assert float(owner.get('/state')['totals']['receivable'])==500
owner.post('/returns',{'productId':sku,'qty':1,'reason':'Damaged, quarantine'})
assert len(owner.get('/state')['returns'])==1
assert 'purchases' not in retailer.get('/state') and 'suppliers' not in retailer.get('/state')
assert all(x['retailer']=='Kumar Maligai' for x in retailer.get('/state')['bills'])
print('PASS: 3 roles, old logins disabled, retailer isolation, orders, owner GRN/AP, billing, dispatch, POD, cash/UPI, close, returns')
