#!/usr/bin/env bash
set -euo pipefail

REMOTE_DIR="/apivoltaz"
EXPECTED_APP_ID="2182171352343231"

command -v lftp >/dev/null || { echo "lftp is required. On macOS: brew install lftp" >&2; exit 1; }
command -v node >/dev/null || { echo "node is required." >&2; exit 1; }

if [ -z "${FTP_HOST:-}" ]; then read -r -p "FTP host: " FTP_HOST; fi
if [ -z "${FTP_USER:-}" ]; then read -r -p "FTP username: " FTP_USER; fi
if [ -z "${FTP_PASS:-}" ]; then read -r -s -p "FTP password: " FTP_PASS; echo; fi

[ -n "$FTP_HOST" ] || { echo "FTP host cannot be empty." >&2; exit 1; }
[ -n "$FTP_USER" ] || { echo "FTP username cannot be empty." >&2; exit 1; }
[ -n "$FTP_PASS" ] || { echo "FTP password cannot be empty." >&2; exit 1; }

TASK_TEMP_DIR="$(mktemp -d)"
CONFIG_FILE="$TASK_TEMP_DIR/meta-inbox.production.json"
cleanup() {
  unset FTP_PASS
  if [ -f "$CONFIG_FILE" ]; then rm -f -- "$CONFIG_FILE"; fi
  if [ -d "$TASK_TEMP_DIR" ]; then rmdir "$TASK_TEMP_DIR" 2>/dev/null || true; fi
}
trap cleanup EXIT
chmod 700 "$TASK_TEMP_DIR"

echo "Reading protected production configuration..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_DOWNLOAD
set ftp:ssl-allow no
get "$REMOTE_DIR/meta-inbox.production.json" -o "$CONFIG_FILE"
bye
LFTP_DOWNLOAD
chmod 600 "$CONFIG_FILE"

echo "Checking Meta token, phone number, WABA, and webhook subscriptions..."
CONFIG_FILE="$CONFIG_FILE" EXPECTED_APP_ID="$EXPECTED_APP_ID" node <<'NODE'
const fs = require('node:fs');

const configuration = JSON.parse(fs.readFileSync(process.env.CONFIG_FILE, 'utf8'));
const meta = configuration?.MetaInbox;
if (!meta || typeof meta !== 'object') throw new Error('MetaInbox configuration is missing.');

const expectedAppId = process.env.EXPECTED_APP_ID;
const version = String(meta.GraphApiVersion || 'v26.0').replace(/^\/+|\/+$/g, '');
const whatsappToken = String(meta.WhatsAppAccessToken || '');
const appSecret = String(meta.AppSecret || '');
const phoneId = String(meta.WhatsAppPhoneNumberId || '');
const wabaId = String(meta.WhatsAppBusinessAccountId || '');

if (!whatsappToken || !appSecret || !phoneId || !wabaId) {
  throw new Error('Production WhatsApp configuration is incomplete.');
}

const appToken = `${expectedAppId}|${appSecret}`;

async function graph(path, token, parameters = {}) {
  const url = new URL(`https://graph.facebook.com/${version}/${path}`);
  for (const [name, value] of Object.entries(parameters)) url.searchParams.set(name, value);
  url.searchParams.set('access_token', token);
  const response = await fetch(url);
  const body = await response.json().catch(() => ({
    error: { message: `Meta returned a non-JSON response with HTTP ${response.status}.` }
  }));
  return { status: response.status, body };
}

function printResult(label, result) {
  console.log(`\n=== ${label} — HTTP ${result.status} ===`);
  console.log(JSON.stringify(result.body, null, 2));
}

(async () => {
  const debug = await graph('debug_token', appToken, { input_token: whatsappToken });
  const phone = await graph(phoneId, whatsappToken, {
    fields: 'id,display_phone_number,verified_name,quality_rating'
  });
  const phoneRuntime = await graph(phoneId, whatsappToken, {
    fields: 'id,code_verification_status,status,platform_type,name_status'
  });
  const wabaPhones = await graph(`${wabaId}/phone_numbers`, whatsappToken, {
    fields: 'id,display_phone_number,verified_name,quality_rating'
  });
  const liveWabaPhones = await graph(`${wabaId}/phone_numbers`, whatsappToken, {
    fields: 'id,display_phone_number,verified_name',
    filtering: JSON.stringify([{ field: 'account_mode', operator: 'EQUAL', value: 'LIVE' }])
  });
  const wabaSubscriptions = await graph(`${wabaId}/subscribed_apps`, whatsappToken);
  const appSubscriptions = await graph(`${expectedAppId}/subscriptions`, appToken);

  printResult('TOKEN DEBUG', debug);
  printResult('CONFIGURED PHONE', phone);
  printResult('PHONE RUNTIME STATUS', phoneRuntime);
  printResult('PHONES INSIDE WABA', wabaPhones);
  printResult('LIVE PHONES INSIDE WABA', liveWabaPhones);
  printResult('WABA SUBSCRIBED APPS', wabaSubscriptions);
  printResult('APP WEBHOOK SUBSCRIPTIONS', appSubscriptions);

  const tokenData = debug.body?.data || {};
  const scopes = new Set(tokenData.scopes || []);
  const subscribedApps = wabaSubscriptions.body?.data || [];
  const correctAppSubscribed = subscribedApps.some(item =>
    String(item?.whatsapp_business_api_data?.id || '') === expectedAppId);
  const configuredPhones = wabaPhones.body?.data || [];
  const correctPhoneInWaba = configuredPhones.some(item => String(item?.id || '') === phoneId);
  const livePhones = liveWabaPhones.body?.data || [];
  const phoneIsLive = livePhones.some(item => String(item?.id || '') === phoneId);
  const runtime = phoneRuntime.body || {};
  const webhookSubscriptions = appSubscriptions.body?.data || [];
  const whatsappSubscription = webhookSubscriptions.find(item =>
    item?.object === 'whatsapp_business_account');
  const subscribedFields = (whatsappSubscription?.fields || []).map(field =>
    typeof field === 'string' ? field : field?.name);

  console.log('\n=== SUMMARY ===');
  console.log(`${tokenData.is_valid === true ? 'PASS' : 'FAIL'} Token is valid`);
  console.log(`${String(tokenData.app_id || '') === expectedAppId ? 'PASS' : 'FAIL'} Token belongs to app ${expectedAppId}`);
  console.log(`${scopes.has('whatsapp_business_messaging') ? 'PASS' : 'FAIL'} Token has whatsapp_business_messaging`);
  console.log(`${scopes.has('whatsapp_business_management') ? 'PASS' : 'FAIL'} Token has whatsapp_business_management`);
  console.log(`${correctPhoneInWaba ? 'PASS' : 'FAIL'} Phone ${phoneId} belongs to WABA ${wabaId}`);
  console.log(`${phoneIsLive ? 'PASS' : 'FAIL'} Phone is in LIVE account mode`);
  console.log(`${runtime.code_verification_status === 'VERIFIED' ? 'PASS' : 'FAIL'} Phone verification status is ${runtime.code_verification_status || 'not returned'}`);
  console.log(`${runtime.status === 'CONNECTED' ? 'PASS' : 'FAIL'} Phone runtime status is ${runtime.status || 'not returned'}`);
  console.log(`${runtime.platform_type === 'CLOUD_API' ? 'PASS' : 'FAIL'} Phone platform is ${runtime.platform_type || 'not returned'}`);
  console.log(`${correctAppSubscribed ? 'PASS' : 'FAIL'} App is subscribed to the WABA`);
  console.log(`${whatsappSubscription ? 'PASS' : 'FAIL'} App has a whatsapp_business_account webhook subscription`);
  console.log(`${subscribedFields.includes('messages') ? 'PASS' : 'FAIL'} Webhook messages field is subscribed`);

  if (whatsappSubscription) {
    console.log(`Webhook callback: ${whatsappSubscription.callback_url || 'not returned'}`);
    console.log(`Webhook active: ${String(whatsappSubscription.active ?? 'not returned')}`);
  }
})().catch(error => {
  console.error(`Diagnostic failed: ${error.message}`);
  process.exitCode = 1;
});
NODE

echo
echo "Diagnostic complete. The output above does not include access tokens or the App Secret."
