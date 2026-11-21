import { PublicClientApplication, InteractionRequiredAuthError } from '@azure/msal-browser';

type IdentityConfig =
  | {mode:'development'}
  | {mode:'entra';tenantId:string;clientId:string;scope:string};
let config:IdentityConfig | undefined;
let msal:PublicClientApplication | undefined;

export async function initializeIdentity():Promise<void>{
  const response=await fetch('/auth/config');
  if(!response.ok)throw new Error('Authentication configuration is unavailable');
  config=await response.json() as IdentityConfig;
  if(config.mode==='development')return;
  if(!config.clientId || !config.tenantId || !config.scope)
    throw new Error('Microsoft Entra application registrations are not configured');
  msal=new PublicClientApplication({
    auth:{clientId:config.clientId,authority:'https://login.microsoftonline.com/'+config.tenantId,
      redirectUri:window.location.origin},
    cache:{cacheLocation:'sessionStorage'}
  });
  await msal.initialize();
  await msal.handleRedirectPromise();
  if(!msal.getAllAccounts().length){
    await msal.loginRedirect({scopes:[config.scope]});
    throw new Error('Redirecting to Microsoft sign-in');
  }
}

export async function identityHeaders():Promise<Record<string,string>>{
  if(!config)throw new Error('Authentication has not been initialized');
  if(config.mode==='development')return {
    'X-Dev-Tenant':'11111111-1111-1111-1111-111111111111',
    'X-Dev-User':'pilot-owner',
    'X-Dev-Role':'Owner'
  };
  if(!msal)throw new Error('Authentication unavailable');
  const account=msal.getAllAccounts()[0];
  if(!account)throw new Error('Please sign in');
  const request={account,scopes:[config.scope]};
  let accessToken:string;
  try{accessToken=(await msal.acquireTokenSilent(request)).accessToken}
  catch(error){
    if(error instanceof InteractionRequiredAuthError){
      await msal.acquireTokenRedirect(request);
      throw new Error('Re-authentication required');
    }
    throw error;
  }
  return {Authorization:'Bearer '+accessToken};
}
