/* Badri TradeFlow R1 XML pilot. No external CDN; business transactions run in C# backend. */
const root=document.getElementById('app');
const session={username:'',role:'',csrf:''};let state={},view='home',flashTimeout;
const money=n=>'₹'+new Intl.NumberFormat('en-IN',{maximumFractionDigits:2,minimumFractionDigits:0}).format(Number(n||0));
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const val=(r,k)=>r?.[k]??'';const total=(rows,k='amount')=>(rows||[]).reduce((a,b)=>a+Number(b[k]||0),0);
const nice=s=>String(s||'').replace(/-/g,' ');
const pill=s=>`<span class="pill ${/approved|cleared|delivered|verified|received/i.test(s)?'green':/missing|exception|reject|short|blocked/i.test(s)?'red':'blue'}">${esc(s)}</span>`;
const opt=(rows,k='name',id='id')=>(rows||[]).map(r=>`<option value="${esc(r[id])}">${esc(r[k])}</option>`).join('');
const field=(name,title,type='text',value='',required=true)=>`<label>${title}<input name="${name}" type="${type}" ${required?'required':''} value="${esc(value)}" /></label>`;
const select=(name,title,rows,k='name',id='id')=>`<label>${title}<select name="${name}" required><option value="">Choose / தேர்வு</option>${opt(rows,k,id)}</select></label>`;
const submit=text=>`<div class="form-full"><button class="btn" type="submit">${text}</button></div>`;
const box=(title,subtitle,inner)=>`<section class="panel"><h2>${title}</h2><p class="sub">${subtitle}</p>${inner}</section>`;
const form=(action,fields,button)=>`<form data-form="${action}" class="forms">${fields}${submit(button)}</form>`;
const arr=k=>Array.isArray(state[k])?state[k]:[];
function warn(msg,ok=false){const old=document.querySelector('#toast');if(old)old.remove();let e=document.createElement('div');e.id='toast';e.className='alert-float '+(ok?'ok':'');e.setAttribute('role','status');e.textContent=msg;document.body.appendChild(e);clearTimeout(flashTimeout);flashTimeout=setTimeout(()=>e.remove(),7000);}
async function api(path,method='GET',body=null){let headers={'Accept':'application/json'};if(method!=='GET'){headers['X-CSRF']=session.csrf;if(!(body instanceof FormData))headers['Content-Type']='application/json'}const r=await fetch('/api'+path,{method,headers,credentials:'same-origin',body:body==null?undefined:body instanceof FormData?body:JSON.stringify(body)});let out;try{out=await r.json()}catch{out={error:'Could not read server response'}}if(!r.ok)throw Error(out.error||out.title||`Request failed (${r.status})`);return out}
async function startup(){try{let response=await fetch('/api/auth/session',{credentials:'same-origin'});if(response.status===401){showLogin();return}const account=await response.json();Object.assign(session,account);await refresh();}catch(e){showLogin(String(e.message||e));}}
async function refresh(){state=await api('/state');if(!allowedView(view))view='home';render();}
function showLogin(error=''){root.innerHTML=`<div class="login-stage"><div class="login-copy"><div class="logo" style="color:#fff;font-size:34px">BADRI <b style="color:#ffe8bb">TradeFlow</b><small style="color:#ffe8bb">DHARMAPURI · TAMIL NADU</small></div><h1>ஒரே இடத்தில்<br/>உங்கள் வியாபாரம்</h1><p>Stock, orders, delivery, collections and supplier payments — simple enough to use every day.<br/><b>கடை இருப்பு · பில் · டெலிவரி · வசூல்</b></p><div style="margin-top:20px"><span class="badge">தமிழ் + ENGLISH</span> <span class="badge">Real sample document references</span></div></div><div class="login-card"><div class="login-big">🔐</div><h2>Sign in / உள்நுழைக</h2><p class="muted">Choose owner, salesperson or retailer. Your password is managed privately in Azure.</p>${error?`<div class="notice danger">${esc(error)}</div>`:''}<form id="loginForm"><label>Username / பயனர் பெயர்<input name="username" autocomplete="username" required placeholder="owner" /></label><label style="margin-top:13px">Password / கடவுச்சொல்<input name="password" type="password" autocomplete="current-password" required /></label><button class="btn" type="submit">Login / உள்நுழையவும் →</button></form><p class="muted">This is a secure-cookie, single-trader demonstration. All original PDF amounts remain unverified until approved. No real bank transfers happen.</p></div></div>`;
document.getElementById('loginForm').onsubmit=async e=>{e.preventDefault();const fd=new FormData(e.target);try{const user=await api('/auth/login','POST',{username:fd.get('username'),password:fd.get('password')});Object.assign(session,user);view='home';await refresh();}catch(err){warn(err.message)}};}
const NAV={
 Owner:[['home','🏠','Today / இன்று'],['orders','🧺','Orders / ஆர்டர்'],['inventory','📦','Stock / இருப்பு'],['accounts','💰','Accounts / கணக்கு'],['tools','📁','More / மேலும்']],
 Salesperson:[['home','🏠','My day / இன்று'],['orders','🧺','Take orders / ஆர்டர்'],['delivery','🚚','Deliver / டெலிவரி'],['collections','💵','Collections / வசூல்'],['cash','🪙','Cash handover']],
 Retailer:[['home','🏠','My shop / என் கடை'],['orders','🛒','Place order / ஆர்டர்'],['delivery','🚚','My deliveries']]
};
const OWNER_EXTRA=['documents','purchases','stock','sales','delivery','collections','cash','dues','payables','returns','reports','audit'];
function menuFor(){return NAV[session.role]||[];}
function allowedView(id){return menuFor().some(x=>x[0]===id)||(session.role==='Owner'&&OWNER_EXTRA.includes(id));}
function render(){
 if(!session.role){showLogin();return;}
 const back=menuFor().some(x=>x[0]===view)?'':'<button class="back-home" data-nav="home">← Back to today / முகப்பு</button>';
 root.innerHTML=`<header class="shell-header"><div class="row"><div class="logo">BADRI <b>TradeFlow</b><small>தர்மபுரி · TAMIL NADU</small></div>
 <div class="meta"><span class="badge">${esc(session.role)} · ${esc(session.username)}</span><button class="lang-button" id="logoutBtn">Sign out / வெளியேறு</button></div></div></header>
 <div class="layout app-shell"><aside class="side main-nav"><h4>DAILY WORK / உங்கள் வேலை</h4>
 ${menuFor().map(([id,emoji,title])=>`<button class="nav ${view===id?'active':''}" data-nav="${id}"><span class="nav-symbol">${emoji}</span> ${title}</button>`).join('')}
 <div class="nav-tip">Simple daily work<br/><small>Single trader · XML test pilot</small></div></aside>
 <main class="content-space">${back}<div class="primary-info"><div><h1>${pageTitle()}</h1><p>${pageSub()}</p></div><div class="icon">${pageIcon()}</div></div>
 ${pageContent()}<p class="foot">BADRI TradeFlow · Pilot testing only. Historical paper amounts are not verified balances.</p></main></div>`;
 document.querySelectorAll('[data-nav]').forEach(e=>e.onclick=()=>{if(allowedView(e.dataset.nav)){view=e.dataset.nav;render();window.scrollTo({top:0,behavior:'instant'});}});
 document.getElementById('logoutBtn').onclick=async()=>{try{await api('/auth/logout','POST',{});}catch{}session.role='';showLogin();};
 document.querySelectorAll('form[data-form]').forEach(e=>e.onsubmit=submitForm);
 document.querySelectorAll('button[data-action]').forEach(e=>e.onclick=clickAction);
}
const titles={orders:['Orders / ஆர்டர்','Request, review, bill and deliver.','🧺'],inventory:['Inventory / இருப்பு','Supplier invoices, received stock and returns.','📦'],accounts:['Accounts / கணக்கு','Cash, outstanding and supplier payments.','💰'],tools:['More / கூடுதல்','Uploads, reports and history when needed.','📁'],home:['Daily Control / தினசரி நிலை','What needs attention today? / இன்று என்ன செய்ய வேண்டும்?','☀️'],documents:['Upload Centre / ஆவணங்கள்','Three documents. One simple review journey. / மூன்று வகை ஆவணங்கள்','📤'],purchases:['Supplier Bills / சப்ளையர் பில்','Verify first, receive goods separately. / சரிபார்த்த பிறகு பொருள் பெறுங்கள்','🧾'],stock:['Inventory / இருப்பு','Count physical stock and know what is available. / எவ்வளவு இருப்பு?','📦'],sales:['Retailer Bills / கடை பில்','Create and view bills by shop. / கடை வாரியாக பில்','🛍️'],delivery:['Delivery / டெலிவரி','Dispatched is not the same as delivered. / பெற்றதை உறுதி செய்யுங்கள்','🚚'],collections:['Collections / வசூல்','Cash and UPI are verified separately. / பணம் பெற்றதை பதிவு','💵'],cash:['Cash Closing / பண ஒப்படைப்பு','Count notes and settle with the owner. / ரொக்கம் சரிபார்ப்பு','🪙'],dues:['Money to Collect / வரவேண்டிய பணம்','Amounts due are based on posted bills and cleared payments.','📊'],payables:['Supplier Payments / செலுத்த வேண்டியது','Only verified GRN bills become payable. / வாங்கிய பொருளுக்கான கட்டணம்','🏦'],returns:['Returns / திரும்பப்பெறுதல்','Returned items are quarantined before resale. / சேதமடைந்த பொருட்கள்','↩️'],reports:['Reports / அறிக்கை','Understand the business without manual totals.','📈'],audit:['Audit Trail / பதிவு','See who changed what, with timestamps.','🛡️'],admin:['SaaS Admin / நிர்வாகம்','Subscription status only; no trader commercial records.','⚙️']};
function pageTitle(){return titles[view]?.[0]||titles.home[0]}function pageSub(){return titles[view]?.[1]||''}function pageIcon(){return titles[view]?.[2]||'🏠'}
function pageContent(){switch(view){case'home':return home();case'orders':return orders();case'inventory':return inventory();case'accounts':return accounts();case'tools':return toolsPage();case'documents':return documents();case'purchases':return purchases();case'stock':return stock();case'sales':return sales();case'delivery':return delivery();case'collections':return collections();case'cash':return cash();case'dues':return dues();case'payables':return payables();case'returns':return returns();case'reports':return reports();case'audit':return audit();case'admin':return admin();default:return home()}}
function quick(id,emoji,title,sub){return allowedView(id)?`<button class="quick" data-nav="${id}"><span>${emoji}</span><b>${title}</b><small>${sub}</small></button>`:''}

function tile(label,value,help){return `<div class="kpi"><div class="label">${label}</div><strong>${value}</strong><small>${help}</small></div>`;}
function home(){
 if(session.role==='Retailer')return retailer();
 if(session.role==='Salesperson'){
  const t=state.totals||{};const dispatched=arr('deliveries').filter(x=>x.status==='Dispatched').length;
  return `<div class="hello-banner"><h2>My work today / இன்று என் வேலை</h2><p>Take orders → deliver goods → collect money → hand over cash.</p></div>
  <div class="grid mini-kpis">${tile('Deliveries / டெலிவரி',dispatched,'Get shop acknowledgment')}
  ${tile('Cash to hand over',money(t.pendingCash),'Count physical notes')}
  ${tile('New orders',t.pendingOrders??0,'Owner will review')}</div>
  ${box('What next? / அடுத்து என்ன?','Select your next task.',
   `<div class="actions-grid">${quick('orders','🧺','1. Take order','Shop request')}
    ${quick('delivery','🚚','2. Deliver','Confirm quantity received')}
    ${quick('collections','💵','3. Collect','Record cash or UPI')}
    ${quick('cash','🪙','4. Handover','Count cash for owner')}</div>`)}
  ${box('Alerts / அறிவிப்புகள்','Your delivery and cash reminders.',
   `<div class="list-message">${dispatched?'🚚 '+dispatched+' delivery acknowledgment(s) pending.':'✅ No delivery pending.'}</div>
    <div class="list-message">${Number(t.pendingCash)>0?'💵 Cash to hand over: '+money(t.pendingCash):'✅ Cash handover is up to date.'}</div>`)}`;
 }
 const t=state.totals||{},pending=arr('orders').filter(x=>x.status==='New').length,
       cashWaiting=arr('cashcloses').filter(x=>x.status==='Awaiting owner').length,
       bankWaiting=arr('collections').filter(x=>x.status==='Pending bank verification').length;
 return `<div class="hello-banner"><h2>Your business today / இன்றைய வியாபாரம்</h2><p>Do the urgent items first. Everything else fits into four areas.</p></div>
 <div class="grid mini-kpis">${tile('Retailer dues / நிலுவை',money(t.receivable),'Posted bills minus cleared receipts')}
 ${tile('New orders / ஆர்டர்',pending,'Retailer requests for review')}
 ${tile('Cash to approve / ரொக்கம்',cashWaiting,'Cash handovers')}</div>
 ${box('Needs your attention / கவனம் தேவை','One action at a time.',
  `<div class="task-row"><b>🧺 ${pending} retailer orders</b>${quick('orders','→','Review orders','Accept or decline')}</div>
  <div class="task-row"><b>🪙 ${cashWaiting} cash handovers</b>${quick('cash','→','Check cash','Confirm note count')}</div>
  <div class="task-row"><b>🏦 ${bankWaiting} UPI/bank receipts</b>${quick('collections','→','Verify payments','Requires bank evidence')}</div>
  <div class="task-row"><b>🧾 ${t.documentReviews??0} documents to review</b>${quick('documents','→','Check document','Paper is not posted')}</div>`)}
 ${box('Run the business / வியாபார வேலை','Choose an area to continue.',
  `<div class="actions-grid">${quick('orders','🧺','Orders & Sales','Retailer bookings, invoices')}
  ${quick('inventory','📦','Stock & Purchases','Supplier bills, GRN, returns')}
  ${quick('accounts','💰','Money & Accounts','Collections, cash, payables')}
  ${quick('tools','📁','Documents & Reports','Uploads, history')}</div>`)}
 <div class="notice"><b>XML pilot:</b> Historical sheets are reference-only. They do not automatically create a due, payment or delivery.</div>`;
}
function orderCards(){
 const rows=arr('orders');
 return rows.length?`<div class="order-list">${rows.map(o=>`<div class="order-item"><div>
 <b>${esc(o.retailer)}</b><p>${esc(o.product)} · ${esc(o.qty)} units</p>
 <small>${esc(o.note||'No note')}</small></div><div class="order-actions">${pill(o.status)}
 ${session.role==='Owner'&&o.status==='New'?`<button class="btn btn-mini" data-action="order-accept" data-id="${esc(o.id)}">Accept / ஏற்று</button>
 <button class="btn btn-mini btn-secondary" data-action="order-decline" data-id="${esc(o.id)}">Decline</button>`:''}</div></div>`).join('')}</div>`
 :'<div class="empty-state">No orders yet. / இன்னும் ஆர்டர் இல்லை.</div>';
}
function orders(){
 if(session.role==='Owner')return `${box('Retailer orders / கடை ஆர்டர்',
   'Review before accepting. Accepting an order does NOT post an invoice or move stock.',orderCards())}
   ${box('Next steps / அடுத்து','Once accepted, record a bill, dispatch goods and get receiver acknowledgment.',
   `<div class="actions-grid">${quick('sales','🧾','Create sales bill','Select accepted order')}
   ${quick('delivery','🚚','Dispatch goods','Deduct warehouse stock')}
   ${quick('dues','📋','Retailer balances','See pending collection')}</div>`)}`;
 const shop=session.role==='Retailer';
 const retailField=shop?'':select('retailerId','Retailer / கடை',arr('retailers'));
 return `${box(shop?'Place order / ஆர்டர் செய்':'Take order / ஆர்டர் பதிவு',
  shop?'Select item and quantity; owner will approve the request. No payment here.'
      :'Select the shop and item. Owner verifies and confirms it.',
   form('order',`${retailField}${select('productId','Product / பொருள்',arr('products'))}
    ${field('quantity','Quantity / எண்ணிக்கை','number','1')}
    ${field('note','Optional note / குறிப்பு','text','',false)}`,
   'Send order / ஆர்டர் அனுப்பு'))}
  ${box(shop?'My orders / என் ஆர்டர்':'Orders submitted / ஆர்டர் பட்டியல்',
    'Status: New → Accepted → Billed; delivery is tracked separately.',orderCards())}`;
}
function inventory(){const t=state.totals||{};
 return `<div class="grid mini-kpis">${tile('Stock units',t.stockUnits??0,'Available stock movements')}
 ${tile('Supplier liabilities',money(t.supplierPayables),'Received invoice values')}</div>
 ${box('Stock tasks / இருப்பு வேலை','Invoice approval never increases available stock. Count actual goods.',
  `<div class="actions-grid">${quick('purchases','🧾','Supplier invoices','Review then record GRN')}
  ${quick('stock','📦','Stock & batches','Physical counts')}
  ${quick('returns','↩️','Returns & damage','Quarantine goods')}
  ${quick('documents','📤','Upload supplier PDF','Manual verification')}</div>`)}`;}
function accounts(){const t=state.totals||{};
 return `<div class="grid mini-kpis">${tile('Receivables',money(t.receivable),'Cleared receipts deducted')}
 ${tile('Unverified receipts',money(t.pendingReceipts),'Cash and UPI still pending')}
 ${tile('Supplier due',money(t.supplierPayables),'Received invoice balance')}</div>
 ${box('Money tasks / பண பரிவர்த்தனை','Cash and bank need different verification.',
  `<div class="actions-grid">${quick('collections','💵','Collections','Record money received')}
  ${quick('cash','🪙','Cash handover','Owner approves note count')}
  ${quick('dues','📋','Retailer outstanding','Shop balances and registration')}
  ${quick('payables','🏦','Supplier payments','External evidence + approval')}</div>`)}`;}
function toolsPage(){return `${box('Use only when required / தேவைப்படும்போது',
 'Everyday work is on Today, Orders, Stock and Accounts.',
 `<div class="actions-grid">${quick('documents','📤','Upload & verify PDF','Collection, invoice and delivery')}
 ${quick('reports','📈','Owner report','Verified totals')}
 ${quick('audit','🛡️','Activity history','Who did what')}</div>`)}
 <div class="notice">Single-trader pilot using XML. Not yet designed for multiple real traders or commercial accounting.</div>`;}
function docsTable(){return arr('documents').map(d=>`<div class="doc-card"><div class="doc-meta"><h3>${esc(d.type)} · ${esc(d.title)}</h3>${pill(d.status)}</div><small>${esc(d.notes||d.source)}</small><div class="line"><span>Written total: <b>${d.claimed?money(d.claimed):'Not visible'}</b></span><span>Calculated: <b>${d.computed?money(d.computed):'Requires full document'}</b></span><span>${d.cashCount?'Counted cash: '+money(d.cashCount):''}</span></div><div class="form-actions">${d.path?`<a class="btn btn-secondary btn-mini" target="_blank" rel="noopener" href="/api/documents/${esc(d.id)}/file">View original file / படம்</a>`:''}${session.role==='Owner'||session.role==='Owner'?`<button class="btn btn-mini btn-secondary" data-action="view-rows" data-id="${esc(d.id)}">View rows / வரிகள்</button> <button class="btn btn-mini" data-action="review-doc" data-id="${esc(d.id)}">Review / சரிபார்</button>`:''}</div></div>`).join('')}
function documents(){return `<div class="notice"><b>PDF upload is real; extraction is not automated yet.</b> Selected PDFs/photos are stored privately and listed for manual review. Upload never posts stock or payments.</div>${box('Upload / பதிவேற்றம்','Select one type then choose a PDF, JPG or PNG up to 10 MB.',`<form data-form="upload" class="forms"><label>Document type / வகை<select name="type"><option>Collection sheet</option><option>Supplier invoice</option><option>Delivery sheet</option></select></label><label>Select PDF or photo / படம்<input type="file" name="file" accept=".pdf,.png,.jpg,.jpeg" required /></label>${submit('Upload & check / பதிவேற்றவும்')}</form>`)}${box('Uploaded & sample documents / ஆவணப்பட்டியல்','Compare the handwriting or supplier bill with the calculated figure. Then save a review note.',docsTable())}`;}
function purchases(){const options=arr('purchases');return `${box('Supplier invoice review / சப்ளையர் பில்','Based on PA Traders, Maha Enterprises and Himalaya examples; invoice values are not current dues.',`<div class="table-scroll"><table><thead><tr><th>Supplier</th><th>Bill</th><th>Printed value</th><th>Pages</th><th>Status</th><th>Action</th></tr></thead><tbody>${options.map(p=>`<tr><td>${esc(p.supplier)}</td><td><b>${esc(p.invoice)}</b></td><td>${p.amount?money(p.amount):'Unknown'}</td><td>${esc(p.completeness)}</td><td>${pill(p.status)}</td><td>${(session.role==='Owner'||session.role==='Owner')&&p.status==='Review required'?`<button class="btn btn-mini" data-action="approve-purchase" data-id="${esc(p.id)}">Review & approve</button>`:p.status==='Part received'&&(session.role==='Owner'||session.role==='Owner')?`<button class="btn btn-mini btn-green" data-action="finish-grn" data-id="${esc(p.id)}">Complete receiving</button>`:'—'}</td></tr>`).join('')}</tbody></table></div>`)}<div class="notice"><b>Stock safety rule:</b> Invoice approval creates no warehouse quantity. Warehouse posts received goods separately with a GRN. Missing pages cannot be approved.</div>${session.role==='Owner'?box('Physical goods received / பொருள் வந்ததா?','Count actual stock by SKU and batch, not only the PDF.',grnForm()):''}`;}
function grnForm(){return form('grn',`${select('purchaseId','Approved supplier bill / சரிபார்க்கப்பட்ட பில்',arr('purchases').filter(x=>x.status.startsWith('Approved')||x.status==='Part received'),'invoice')}${select('productId','Product / பொருள்',arr('products'))}${field('quantity','Physically counted units / வந்த எண்ணிக்கை','number','',true)}${field('damaged','Damaged units / சேதம்','number','0',true)}${field('batch','Batch / பேட்ச்','text','BATCH-01')}${field('expiry','Expiry date (optional) / காலாவதி','date','',false)}`,'Confirm GRN / பொருள் பெறப்பட்டது');}
function stock(){const products=arr('products');const movement=arr('stock');const qty=id=>movement.filter(x=>x.productId===id).reduce((s,x)=>s+Number(x.qty||0),0);return `${box('Available Stock / உள்ள இருப்பு','Starts at zero until physical opening count or approved GRN.',`<div class="table-scroll"><table><thead><tr><th>Product</th><th>Saleable</th><th>Daily sale</th><th>Lead time</th><th>Buy suggestion</th></tr></thead><tbody>${products.map(p=>{const onhand=qty(p.id),avg=Number(p.dailyDemand||0),safety=Number(p.safetyStock||20),rp=avg*Number(p.leadDays||4)+safety;return `<tr><td>${esc(p.name)}</td><td><b>${onhand}</b></td><td>${avg||'Need sales data'}</td><td>${esc(p.leadDays)} days</td><td>${avg>0?(onhand<=rp?'Reorder suggested':'In stock'):'Set sales history first'}</td></tr>`}).join('')}</tbody></table></div>`)}${(session.role==='Owner'||session.role==='Owner')?box('Opening stock count / ஆரம்ப இருப்பு','Enter the physical count ONCE per product; your account must authorize it.',form('opening',`${select('productId','Product / பொருள்',products)}${field('quantity','Units physically counted / உண்மை இருப்பு','number','0')}${field('batch','Batch / பேட்ச்','text','OPEN-2026')}`,'Save opening stock / பதிவு')):''}${box('Stock Ledger / இருப்பு பதிவு','Every movement is an auditable addition/subtraction.',`<div class="table-scroll"><table><thead><tr><th>Product</th><th>Reason</th><th>Quantity</th><th>Batch</th></tr></thead><tbody>${movement.map(x=>`<tr><td>${esc(x.product)}</td><td>${esc(x.reason)}</td><td>${Number(x.qty)>0?'+':''}${esc(x.qty)}</td><td>${esc(x.batch)}</td></tr>`).join('')||'<tr><td colspan="4">No GRN or opening count has been posted.</td></tr>'}</tbody></table></div>`)}${session.role==='Owner'?box('Receive Supplier Goods / பொருள் பெறுதல்','An approved commercial bill is required.',grnForm()):''}`;}
function sales(){return `${box('New Retailer Bill / கடை பில்','Create a live pilot bill so the salesman can dispatch and collect.',form('bill',`${select('retailerId','Retailer / கடை',arr('retailers'))}${field('billNo','Unique bill number / பில் எண்','text','PILOT-001')}${field('amount','Bill total (₹) / மொத்தம்','number','','true')}${field('dueDate','Due date / தேதி','date',new Date().toISOString().slice(0,10))}<label>Accepted order (optional) / ஆர்டர்<select name="orderId">
<option value="">Manual invoice / நேரடி பில்</option>${arr('orders').filter(x=>x.status==='Accepted - pending billing').map(x=>
`<option value="${esc(x.id)}">${esc(x.retailer)} — ${esc(x.product)} × ${esc(x.qty)}</option>`).join('')}</select></label>`,'Create Bill / பில் பதிவு'))}${box('Posted Pilot Bills / பதிவான பில்கள்','Only these appear as receivables; historical uploads do not.',billTable())}`;}
function billTable(){return `<div class="table-scroll"><table><thead><tr><th>Bill</th><th>Shop</th><th>Amount</th><th>Due</th><th>State</th></tr></thead><tbody>${arr('bills').map(x=>`<tr><td>${esc(x.billNo)}</td><td>${esc(x.retailer)}</td><td>${money(x.amount)}</td><td>${esc(x.due)}</td><td>${pill(x.status)}</td></tr>`).join('')||'<tr><td colspan="5">No bills entered yet.</td></tr>'}</tbody></table></div>`}
function delivery(){const deliveries=arr('deliveries');const canDispatch=session.role==='Owner';return `${canDispatch?box('Step 1 · Dispatch Order / சரக்கு அனுப்புக','Bill must exist and stock must be available. This decreases warehouse stock.',form('dispatch',`${select('billId','Retailer bill / கடை பில்',arr('bills').filter(x=>x.status==='Created - pending dispatch'),'billNo')}${select('productId','Product / பொருள்',arr('products'))}${field('quantity','Units to dispatch / எண்ணிக்கை','number','1')}`,'Dispatch / அனுப்புக')):''}${box('Step 2 · Confirm Delivery / டெலிவரி உறுதி','Receiver name and accepted quantity are separate from payment; mismatches create exceptions.',`<div class="table-scroll"><table><thead><tr><th>Bill / Shop</th><th>Product</th><th>Sent</th><th>Status</th><th>Action</th></tr></thead><tbody>${deliveries.map(x=>`<tr><td>${esc(x.billNo)}<br/><small>${esc(x.retailer)}</small></td><td>${esc(x.product)}</td><td>${esc(x.qty)}</td><td>${pill(x.status)}</td><td>${x.status==='Dispatched'?`<button class="btn btn-mini" data-action="pod" data-id="${esc(x.id)}" data-qty="${esc(x.qty)}">Confirm / பெறப்பட்டது</button>`:'—'}</td></tr>`).join('')||'<tr><td colspan="5">No dispatched bills yet.</td></tr>'}</tbody></table></div>`)}${session.role!=='Retailer'?'<div class="notice">The GK Road delivery sheet is only review evidence. Uploading it does not prove a delivery was made.</div>':''}`;}
function collections(){if(session.role==='Retailer')return retailer();const canRecord=session.role!=='Warehouse';return `${canRecord?box('Record Payment / வசூல் பதிவு','Select the bill, amount and method. A receipt is created but is not automatically cleared.',form('collection',`${select('billId','Unpaid bill / நிலுவை பில்',arr('bills'),'billNo')}${field('amount','Received amount (₹) / தொகை','number','')}${select('mode','Payment mode / பண முறை',[{id:'Cash',name:'Cash / ரொக்கம்'},{id:'UPI',name:'UPI / ஜிபே'},{id:'Bank',name:'Bank transfer / வங்கி'},{id:'Cheque',name:'Cheque / காசோலை'}])}`,'Record Receipt / ரசீது')):''}${box('Receipts / ரசீதுகள்','Cash awaits owner handover; UPI/bank needs independent payment evidence.',`<div class="table-scroll"><table><thead><tr><th>Bill</th><th>Retailer</th><th>Amount</th><th>Method</th><th>Status</th><th>Action</th></tr></thead><tbody>${arr('collections').map(x=>`<tr><td>${esc(x.billNo)}</td><td>${esc(x.retailer)}</td><td>${money(x.amount)}</td><td>${esc(x.mode)}</td><td>${pill(x.status)}</td><td>${(session.role==='Owner'||session.role==='Owner')&&x.status==='Pending bank verification'?`<button class="btn btn-mini" data-action="bank-verify" data-id="${esc(x.id)}">Review Bank</button>`:'—'}</td></tr>`).join('')||'<tr><td colspan="6">No pilot receipts recorded.</td></tr>'}</tbody></table></div>`)}<div class="notice danger">A UPI screenshot alone is NOT proof of bank settlement. The pilot records authorized manual bank evidence; it does not connect to a bank or move money.</div>`;}
function cash(){return `${(session.role==='Salesperson'||session.role==='Owner')?box('Step 1 · Count Notes / நோட்டு எண்ணிக்கை','Count physical money. The form totals denominations automatically.',`<form data-form="cash" class="forms">${[500,200,100,50,20,10,5,2,1].map(n=>field('note'+n,`₹${n} × number of notes`,'number','0',false)).join('')}${field('date','Business date / நாள்','date',new Date().toISOString().slice(0,10))}${field('reason','Explanation if different / வித்தியாசம்','text','',false)}<div class="form-full"><strong id="cashcalc">Counted ₹0</strong></div>${submit('Submit to Owner / ஒப்படைக்க')}</form>`):''}${box('Step 2 · Owner Approval / முதலாளி ஒப்புதல்','Cash counts close the outstanding receipts only after explicit approval.',`<div class="table-scroll"><table><thead><tr><th>Salesman</th><th>Expected</th><th>Counted</th><th>Difference</th><th>Status</th><th>Action</th></tr></thead><tbody>${arr('cashcloses').map(x=>`<tr><td>${esc(x.by)}</td><td>${money(x.expected)}</td><td>${money(x.counted)}</td><td>${money(x.difference)}</td><td>${pill(x.status)}</td><td>${session.role==='Owner'&&x.status==='Awaiting owner'?`<button class="btn btn-mini" data-action="cashapprove" data-id="${esc(x.id)}">Approve Close</button>`:'—'}</td></tr>`).join('')||'<tr><td colspan="6">No cash handed over yet.</td></tr>'}</tbody></table></div>`)}`;}
function dues(){const shops=arr('retailers'),bills=arr('bills'),receipts=arr('collections');return `${box('Retailer Outstanding / கடை நிலுவை','Historic paper amounts are NOT posted as unpaid until opening balances are verified.',`<div class="table-scroll"><table><thead><tr><th>Retailer</th><th>Beat</th><th>Posted bills</th><th>Cleared payments</th><th>Money due</th></tr></thead><tbody>${shops.map(x=>{const a=bills.filter(b=>b.retailerId===x.id);const posted=total(a),cleared=total(receipts.filter(c=>c.retailerId===x.id&&c.status==='Cleared'));return `<tr><td>${esc(x.name)}</td><td>${esc(x.beat)}</td><td>${money(posted)}</td><td>${money(cleared)}</td><td><b>${money(posted-cleared)}</b></td></tr>`}).join('')}</tbody></table></div>`)}${session.role==='Owner'||session.role==='Owner'?box('Add New Retailer / புதிய கடை','Complete initial customer setup with real authorized information.',form('retailer',`${field('name','Shop name / கடை பெயர்')}${field('beat','Beat / பகுதி','text','Bommidi')}`,'Save Retailer / சேமிக்க')):''}`;}
function payables(){return `${box('Supplier Payables / சப்ளையர் நிலுவை','Invoices from sample documents are historical face values; only received, approved liabilities appear due.',`<div class="table-scroll"><table><thead><tr><th>Supplier</th><th>Invoice</th><th>Face value</th><th>Received</th><th>Paid</th><th>Remaining</th></tr></thead><tbody>${arr('purchases').map(x=>`<tr><td>${esc(x.supplier)}</td><td>${esc(x.invoice)}</td><td>${x.amount?money(x.amount):'Unknown'}</td><td>${pill(x.status)}</td><td>${money(x.paid)}</td><td><b>${x.status==='Received'?money(Number(x.amount)-Number(x.paid||0)):'Not posted'}</b></td></tr>`).join('')}</tbody></table></div>`)}${box('Prepare Supplier Payment / செலுத்தும் பதிவு','Owner records external bank evidence and explicitly reviews before recording. No bank transfer is initiated.',form('supplierpay',`${select('purchaseId','Received supplier bill / பெறப்பட்ட பில்',arr('purchases').filter(x=>x.status==='Received'),'invoice')}${field('amount','Payment amount (₹) / தொகை','number')}${field('externalReference','External bank reference / வங்கி விவரம்','text')}`,'Submit for Approval / ஒப்புதலுக்கு'))}${box('Pending Supplier Payment Approvals','Single-owner pilot: approving your own prepared entry requires an evidence note, recorded for audit.',arr('payments').map(x=>`<div class="line"><div><b>${esc(x.supplier)} — ${money(x.amount)}</b><small>${esc(x.reference)} · ${esc(x.preparedBy)}</small></div>${pill(x.status)}${session.role==='Owner'&&x.status==='Owner review required'?`<button class="btn btn-mini" data-action="approve-supplierpay" data-id="${esc(x.id)}">Owner Approve</button>`:''}</div>`).join('')||'<p class="muted">No supplier payments submitted.</p>')}`;}
function returns(){return `${box('Record Damage / திரும்பப்பெறுதல்','Returned items remain quarantined pending stock review.',form('return',`${select('productId','Product / பொருள்',arr('products'))}${field('qty','Units returned / எண்ணிக்கை','number')}${field('reason','Reason / காரணம்','text')}`,'Record Return / பதிவு'))}${box('Returns to Review / ஆய்வு',arr('returns').map(x=>`<div class="line"><b>${esc(x.productId)} — ${esc(x.qty)} units</b>${pill(x.status)}<small>${esc(x.reason)}</small></div>`).join('')||'No returns yet.')}`;}
function reports(){const due=state.totals||{};return `${box('Owner Summary / முதலாளி அறிக்கை','Only verified application entries appear in totals.',`<div class="grid"><div class="kpi"><div class="label">Customer Due</div><strong>${money(due.receivable)}</strong></div><div class="kpi"><div class="label">Pending Verification</div><strong>${money(due.pendingReceipts)}</strong></div><div class="kpi"><div class="label">Supplier payable</div><strong>${money(due.supplierPayables)}</strong></div></div><button class="btn btn-secondary" data-action="csv">Export bills CSV / பில் அறிக்கை</button>`)}<div class="notice">For tax filing, a qualified accountant must verify GST slabs and missing source pages. This pilot does not submit GST returns.</div>`;}
function audit(){return box('Activity / நடந்த வேலை','Every change is stamped with actor and time; there is no silent deletion.',`<div class="table-scroll"><table><thead><tr><th>Time UTC</th><th>User</th><th>Action</th></tr></thead><tbody>${arr('audit').map(x=>`<tr><td>${esc(x.at)}</td><td>${esc(x.actor)}</td><td>${esc(x.action)}</td></tr>`).join('')||'<tr><td colspan="3">No changes yet.</td></tr>'}</tbody></table></div>`);}
function admin(){return `${box('Platform Operator / SaaS நிர்வாகம்','Separate from trader financial data. SaaS admin never sees retailer balances.',`<div class="grid"><div class="kpi"><div class="label">Active tenants</div><strong>1</strong></div><div class="kpi"><div class="label">Plan</div><strong>Pilot</strong></div><div class="kpi"><div class="label">Billing</div><strong>Not active</strong></div></div>`)}<div class="notice">Subscription, payment gateways and support access are not live. This login cannot browse trader invoices or bank accounts.</div>`;}
function retailer(){
 const t=state.totals||{};const toConfirm=arr('deliveries').filter(x=>x.status==='Dispatched').length;
 return `<div class="hello-banner"><h2>My shop / என் கடை</h2><p>Order products, track progress and confirm goods received.</p></div>
 <div class="grid mini-kpis">${tile('My bills due',money(t.receivable),'Only posted bills and cleared payments')}
 ${tile('Orders placed',arr('orders').length,'Check owner approval')}
 ${tile('Confirm deliveries',toConfirm,'Count accepted goods')}</div>
 ${box('What would you like to do? / என்ன செய்யலாம்?','Only two actions needed.',
  `<div class="actions-grid">${quick('orders','🛒','Place a new order','Product and quantity')}
  ${quick('delivery','🚚','Confirm goods received','Only after counting')}</div>`)}
 ${box('My orders / என் ஆர்டர்','The owner checks stock before confirming.',orderCards())}
 ${box('My payment receipts / ரசீது','Cash pending handover and UPI pending bank check are not cleared.',
 arr('collections').map(x=>`<div class="line"><b>${money(x.amount)}</b><span>${esc(x.mode)} ${pill(x.status)}</span></div>`).join('')||'<div class="empty-state">No receipts yet.</div>')}`;
}

async function submitForm(e){e.preventDefault();const formEl=e.currentTarget;const kind=formEl.dataset.form;const fd=new FormData(formEl);const object=Object.fromEntries(fd.entries());try{let path,body=object;
  switch(kind){case'upload':path='/documents/upload';body=fd;break;
  case'retailer':path='/retailers';break;
  case'order':path='/orders';body={retailerId:object.retailerId||null,productId:object.productId,quantity:Number(object.quantity),note:object.note||''};break;
  case'bill':path='/sales-bills';body={...object,amount:Number(object.amount)};break;
  case'opening':path='/products/'+object.productId+'/opening';body={quantity:Number(object.quantity),batch:object.batch};break;
  case'grn':path='/grn';body={...object,quantity:Number(object.quantity),damaged:Number(object.damaged)};break;
  case'dispatch':path='/deliveries';body={...object,quantity:Number(object.quantity)};break;
  case'collection':path='/collections';body={...object,amount:Number(object.amount),idempotencyKey:crypto.randomUUID()};break;
  case'cash':{path='/cashclose';let counted=0;for(let n of [500,200,100,50,20,10,5,2,1]){const count=Number(object['note'+n]||0);if(count<0||!Number.isInteger(count))throw Error('Enter valid non-negative note counts');counted+=n*count;}body={counted,date:object.date,reason:object.reason,notes:Object.fromEntries([500,200,100,50,20,10,5,2,1].map(n=>[n,Number(object['note'+n]||0)]))};break;}
  case'supplierpay':path='/supplierpayments';body={...object,amount:Number(object.amount)};break;
  case'return':path='/returns';body={...object,qty:Number(object.qty)};break;
  default:throw Error('Unknown form');
 }
 await api(path,'POST',body);warn('Saved successfully / பதிவு செய்யப்பட்டது',true);await refresh();
 }catch(err){warn(err.message||String(err));}}
async function clickAction(e){const {action,id,qty}=e.currentTarget.dataset;try{let url,body={};switch(action){
case'order-accept':case'order-decline':url='/orders/'+id+'/decision';body={approve:action==='order-accept',note:action==='order-accept'?'Owner accepted request; check available stock before bill':prompt('Reason for decline / காரணம்:')||''};if(!body.approve&&!body.note.trim())return;break;
case'view-rows':{const rows=await api('/documents/'+id+'/rows');if(!rows.length){warn('No source rows for this document.');return;}const table='<div class="table-scroll"><table><thead><tr><th>Bill</th><th>Retailer / Product</th><th>Amount</th><th>Paper note</th></tr></thead><tbody>'+rows.map(r=>'<tr><td>'+esc(r.bill||'—')+'</td><td>'+esc(r.shop)+'</td><td>'+money(r.amount)+'</td><td>'+esc(r.mark)+'</td></tr>').join('')+'</tbody></table></div>';const existing=document.getElementById('rowsPanel');if(existing)existing.remove();const el=document.createElement('section');el.id='rowsPanel';el.className='panel';el.innerHTML='<div class="row-wrap"><h2>Actual paper rows / அசல் வரிகள்</h2><button class="btn btn-mini btn-secondary" id="closeRows">Close</button></div><div class="notice">Transcribed from sample scans, unverified. No money is posted from these rows.</div>'+table;document.querySelector('main').insertBefore(el,document.querySelector('main .panel')||null);document.getElementById('closeRows').onclick=()=>el.remove();el.scrollIntoView({behavior:'smooth'});return;}
case'review-doc':url='/documents/'+id+'/review';body={approve:confirm('Reviewed source and amounts? / ஆவணத்தை சரிபார்த்தீர்களா?'),note:prompt('Review comment / சரிபார்ப்பு குறிப்பு:')||''};break;
case'finish-grn':if(!confirm('Confirm all goods checked? Partial/missing items must be documented first.'))return;url='/purchases/'+id+'/complete-grn';break;
case'approve-purchase':if(!confirm('Approve commercial invoice? This will NOT add stock.'))return;url='/purchases/'+id+'/approve';break;
case'pod':url='/deliveries/'+id+'/confirm';body={receiver:prompt('Receiver name / பெற்றவர் பெயர்:')||'',accepted:Number(prompt('Quantity accepted / பெற்ற எண்ணிக்கை:',qty)||'0')};break;
case'bank-verify':url='/collections/'+id+'/bank-verify';body={reference:prompt('Verified bank reference (NOT screenshot alone):')||'',reviewerNote:prompt('Evidence checked / உறுதிசெய்த குறிப்பு:')||''};break;
case'cashapprove':url='/cashclose/'+id+'/approve';body={reason:prompt('Owner approval note / ஒப்புதல் குறிப்பு:')||'No discrepancy'};break;
case'approve-supplierpay':url='/supplierpayments/'+id+'/approve';body={reason:prompt('Explain verified external bank evidence (minimum 10 characters):')||''};if(body.reason.trim().length<10)return;break;
case'csv':{const lines=['billNo,retailer,amount,status',...arr('bills').map(x=>[x.billNo,x.retailer,x.amount,x.status].map(v=>'"'+String(v).replaceAll('"','""')+'"').join(','))].join('\r\n');const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([lines],{type:'text/csv'}));a.download='badri-pilot-bills.csv';a.click();URL.revokeObjectURL(a.href);return;}
default:throw Error('Unknown action');}
await api(url,'POST',body);warn('Updated / மாற்றப்பட்டது',true);await refresh();}catch(err){warn(err.message||String(err))}}
// Live denomination total preview for low-literacy staff.
document.addEventListener('input',e=>{if(e.target.closest('form[data-form="cash"]')){const form=e.target.closest('form');let amount=0;for(let n of [500,200,100,50,20,10,5,2,1])amount+=n*Number(form.elements['note'+n]?.value||0);const target=document.getElementById('cashcalc');if(target)target.textContent='Counted / எண்ணிய பணம்: '+money(amount)}});
startup();
