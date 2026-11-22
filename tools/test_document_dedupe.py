#!/usr/bin/env python3
"""Verify exact-file duplicate blocking and no financial posting from uploads."""
import json,os,http.cookiejar,urllib.request,urllib.error,secrets
BASE="http://127.0.0.1:5091/api"
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
req=urllib.request.Request(BASE+'/auth/login',
 data=json.dumps({'username':'owner','password':os.environ['BADRI_PASS_OWNER']}).encode(),
 headers={'Content-Type':'application/json'},method='POST')
login=json.load(opener.open(req))
csrf=login['csrf']
def state():return json.load(opener.open(BASE+'/state'))
start=state()
# Tiny synthetic PDF bytes are intentionally not a customer's real financial data.
bytes_pdf=b'%PDF-1.4\n1 0 obj<</Type /Catalog>>endobj\n%%EOF\n'
def upload(name):
 boundary='----BadriTest'+secrets.token_hex(12)
 body=(
   ('--'+boundary+'\r\nContent-Disposition: form-data; name="type"\r\n\r\n'
    +'Supplier invoice\r\n--'+boundary+'\r\n'
    +'Content-Disposition: form-data; name="file"; filename="'+name+'"\r\n'
    +'Content-Type: application/pdf\r\n\r\n').encode()
   +bytes_pdf+b'\r\n--'+boundary.encode()+b'--\r\n')
 req=urllib.request.Request(BASE+'/documents/upload',data=body,method='POST',
  headers={'Content-Type':'multipart/form-data; boundary='+boundary,'X-CSRF':csrf})
 with opener.open(req,timeout=20) as response:return response.status,json.load(response)
status,first=upload('source-example.pdf')
assert status==200 and first['status']=='Review required'
try:
 upload('same-bytes-different-filename.pdf')
 raise AssertionError('Duplicate source was uploaded twice')
except urllib.error.HTTPError as e:
 assert e.code==409, (e.code,e.read()[:150])
after=state()
assert len(after['documents'])==len(start['documents'])+1
assert any(x['id']==first['id'] and len(x.get('sha256',''))==64 for x in after['documents'])
for key in ('receivable','supplierPayables','stockUnits'):
 assert str(after['totals'][key])==str(start['totals'][key]),key
print('PASS: uploaded PDF fingerprinted; identical bytes 409; no duplicate record or financial/stock posting')
