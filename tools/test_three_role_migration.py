#!/usr/bin/env python3
"""Checks old six-login XML migration and portal password rotation on test data."""
import http.cookiejar,json,os,sys,urllib.request,urllib.error
from xml.etree import ElementTree as ET
PATH=os.environ['BADRI_XML_PATH']
BASE='http://127.0.0.1:5091'
ROLE_NAMES={'owner':('owner.badri','Owner'),'salesperson':('salesman.santhosh','Salesman'),
            'retailer':('retailer.demo','Retailer')}
def login(user,password):
 req=urllib.request.Request(BASE+'/api/auth/login',method='POST',
  headers={'Content-Type':'application/json'},
  data=json.dumps({'username':user,'password':password}).encode())
 try:
  with urllib.request.urlopen(req,timeout=15) as response:return response.status
 except urllib.error.HTTPError as error:return error.code

command=sys.argv[1]
if command=='prepare':
 tree=ET.parse(PATH);root=tree.getroot()
 users=root.find('users');assert users is not None
 found={}
 for user in list(users):
  name=user.get('name')
  if name in ROLE_NAMES:
   old,role=ROLE_NAMES[name]
   user.set('name',old);user.set('role',role)
   found[name]=user
 assert len(found)==3
 for legacy,role in [('accounts.badri','Accounts'),('warehouse.badri','Warehouse'),('operator.demo','Operator')]:
  clone=ET.SubElement(users,'user',{'name':legacy,'role':role,'tenant':'badri-dharmapuri-pilot',
                                   'hash':found['owner'].get('hash')})
 orders=root.find('orders')
 if orders is not None:root.remove(orders)
 for node in root.iter():
  for key in ('by','preparedBy','approvedBy','reviewedBy','placedBy'):
   if node.get(key)=='salesperson':node.set(key,'salesman.santhosh')
   if node.get(key)=='owner':node.set(key,'owner.badri')
 tree.write(PATH,encoding='utf-8',xml_declaration=True)
 print('PASS: Created synthetic legacy six-login XML with historical receipt attribution')
elif command=='migrated':
 tree=ET.parse(PATH);root=tree.getroot()
 users=root.find('users')
 names={x.get('name') for x in users}
 assert names==set(ROLE_NAMES),names
 assert root.find('orders') is not None
 for node in root.iter():
  for key in ('by','preparedBy','approvedBy','reviewedBy','placedBy'):
   assert node.get(key) not in ('salesman.santhosh','owner.badri')
 for key,username in [('OWNER','owner'),('SALESMAN','salesperson'),('RETAILER','retailer')]:
  assert login(username,os.environ['BADRI_PASS_'+key])==200,username
 for key,user in [('OWNER','owner.badri'),('SALESMAN','salesman.santhosh'),('RETAILER','retailer.demo')]:
  assert login(user,os.environ['BADRI_PASS_'+key])==401,user
 print('PASS: 6→3 role migration preserved XML, remapped actors and rejected legacy login names')
elif command=='rotated':
 assert login('owner',os.environ['BADRI_PASS_OWNER'])==200
 assert login('owner',os.environ['PREVIOUS_BADRI_PASS_OWNER'])==401
 assert login('salesperson',os.environ['BADRI_PASS_SALESMAN'])==200
 assert login('retailer',os.environ['BADRI_PASS_RETAILER'])==200
 print('PASS: Azure-style Owner password change accepted after restart; old password revoked')
else:raise SystemExit('Unknown test command')
