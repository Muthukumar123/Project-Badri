#!/usr/bin/env python3
"""Generate six independent passwords; output files are LOCAL and must not be committed."""
from pathlib import Path
import secrets
import string
alphabet=string.ascii_letters+string.digits
users={
  'owner.badri':'BADRI_PASS_OWNER',
  'accounts.badri':'BADRI_PASS_ACCOUNTS',
  'warehouse.badri':'BADRI_PASS_WAREHOUSE',
  'salesman.santhosh':'BADRI_PASS_SALESMAN',
  'retailer.demo':'BADRI_PASS_RETAILER',
  'operator.demo':'BADRI_PASS_OPERATOR',
}
base=Path(__file__).resolve().parents[1]
passwords={user:''.join(secrets.choice(alphabet) for _ in range(24)) for user in users}
creds=base/'.env.pilot'
if creds.exists():raise SystemExit('Credentials exist. Refusing to overwrite.')
creds.write_text('\n'.join(f'{key}={passwords[user]}' for user,key in users.items())+'\n')
try:creds.chmod(0o600)
except OSError:pass
print('Generated .env.pilot (mode 0600 when supported). Not committed to Git.')
print('Accounts:')
for user,key in users.items():print(f'  {user:24} {passwords[user]}')
print('\nUse six independent secrets in Azure App Service app settings for a protected pilot.')
