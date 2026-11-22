// Smoke-check the three role-specific HTML views without a browser/CDN dependency.
// The DOM/fetch mocks exercise initial render, menu authorization and order screens.
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const source=readFileSync('pilot/wwwroot/app.js','utf8');
const base={
 totals:{receivable:0,pendingReceipts:0,supplierPayables:0,stockUnits:0,documentReviews:7,
         assignedDeliveries:0,pendingCash:0,pendingOrders:0},
 retailers:[{id:'shop-demo',name:'Kumar Maligai',beat:'Bommidi'}],
 products:[{id:'sku',name:'HARPIC 450ML'}],
 bills:[],documents:[],purchases:[],orders:[],stock:[],collections:[],
 deliveries:[],cashcloses:[],payments:[],returns:[],suppliers:[],audit:[]
};

async function simulate(role, username, signedIn=true) {
  const root={innerHTML:''};
  const nodes=new Map([['app',root]]);
  const document={
    getElementById(id){if(!nodes.has(id))nodes.set(id,{onclick:null,onsubmit:null,innerHTML:'',textContent:''});return nodes.get(id);},
    querySelectorAll(){return [];},
    addEventListener(){},
    querySelector(){return null;}
  };
  const fetch=async (url)=>{
    if(url==='/api/auth/session')return signedIn
      ?{status:200,ok:true,json:async()=>({username,role,csrf:'X'.repeat(64)})}
      :{status:401,ok:false,json:async()=>({})};
    if(url==='/api/state')return {status:200,ok:true,json:async()=>({...base,role})};
    throw new Error('Unexpected mock fetch: '+url);
  };
  const ctx=vm.createContext({document,fetch,window:{scrollTo(){}},
    console,setTimeout,clearTimeout,Intl,Date,Map,Math,
    crypto:{randomUUID:()=> 'fixed-uuid'}});
  vm.runInContext(source,ctx,{timeout:1500});
  await new Promise(resolve=>setTimeout(resolve,35));
  if(!signedIn){
    assert(root.innerHTML.includes('Sign in'), 'Login screen is missing');
    assert(root.innerHTML.includes('placeholder="owner"'), 'Username example must be owner');
    return;
  }
  assert(root.innerHTML.includes('BADRI'), 'Logo missing');
  assert(root.innerHTML.includes(username), 'Account label missing');
  assert.equal(vm.runInContext('menuFor().length',ctx), role==='Retailer'?3:5);
  assert.equal(vm.runInContext('allowedView("purchases")',ctx), role==='Owner');
  assert.equal(vm.runInContext('allowedView("orders")',ctx), true);
  vm.runInContext('view="orders";render()',ctx,{timeout:1500});
  assert(root.innerHTML.includes('Orders')||root.innerHTML.includes('order'), 'Order screen blank');
  if(role==='Retailer'||role==='Salesperson'){
    assert(root.innerHTML.includes('data-form="order"'), 'Order entry form missing');
    assert(!root.innerHTML.includes('Owner Approve'), 'Unauthorized approval action shown');
  }
  if(role==='Retailer') {
    assert(!root.innerHTML.includes('name="retailerId"'), 'Retailer should not choose another shop');
    assert.equal(vm.runInContext('allowedView("cash")',ctx),false);
  }
  if(role==='Owner') {
    for(const area of ['inventory','accounts','tools']){
      vm.runInContext('view='+JSON.stringify(area)+';render()',ctx,{timeout:1500});
      assert(root.innerHTML.includes('quick')||root.innerHTML.includes('kpi'),area+' page empty');
    }
  }
  console.log('PASS: '+role+' menu, dashboard, orders and authorized navigation');
}
await simulate('Owner','owner');
await simulate('Salesperson','salesperson');
await simulate('Retailer','retailer');
await simulate('', '',false);
console.log('PASS: login form and three role-specific mobile-ready page skeletons');
