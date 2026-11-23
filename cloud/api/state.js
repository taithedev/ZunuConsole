'use strict';

// ZunuConsole serverless API. Neon credentials ONLY exist in server environment variables.
const crypto = require('node:crypto');

let pool;
function getPool() {
  if (!process.env.DATABASE_URL) throw new Error('DATABASE_URL missing');
  if (!pool) {
    const { Pool } = require('pg');
    pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    max: 2,
    connectionTimeoutMillis: 5000,
    idleTimeoutMillis: 20000,
    ssl: { rejectUnauthorized: true }
    });
  }
  return pool;
}

function userFor(req) {
  const auth = req.headers.authorization || '';
  const match = /^Bearer ([A-Za-z0-9_\-]{32,256})$/.exec(auth);
  if (!match) return null;
  const digest = crypto.createHash('sha256').update(match[1], 'utf8').digest();
  let allowed;
  try { allowed = JSON.parse(process.env.ZUNU_TOKEN_HASHES || '{}'); }
  catch { return null; }
  for (const [sha, accountId] of Object.entries(allowed)) {
    if (!/^[a-fA-F0-9]{64}$/.test(sha) || typeof accountId !== 'string' || accountId.length > 120) continue;
    const permitted = Buffer.from(sha, 'hex');
    if (crypto.timingSafeEqual(digest, permitted)) return accountId;
  }
  return null;
}

const keys = new Set(['ui.theme', 'ui.accent', 'ui.opacity', 'ui.rounding', 'ui.fontSize',
  'ui.logFontSize', 'ui.density', 'ui.accentR', 'ui.accentG', 'ui.accentB']);
function sanitize(body) {
  if (!body || typeof body !== 'object' || !Array.isArray(body.settings) || !Array.isArray(body.favorites)) return null;
  if (body.settings.length > 50 || body.favorites.length > 250) return null;
  const settings = [];
  for (const item of body.settings) {
    if (!item || !keys.has(item.key) || typeof item.value !== 'string' || item.value.length > 32) return null;
    settings.push({ key: item.key, value: item.value });
  }
  const favorites = [];
  for (const name of body.favorites) {
    if (typeof name !== 'string' || !/^[a-zA-Z0-9_.:-]{1,80}$/.test(name)) return null;
    favorites.push(name);
  }
  return { settings, favorites };
}

module.exports = async function handler(req, res) {
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  if (req.method !== 'GET' && req.method !== 'PUT') {
    res.setHeader('Allow', 'GET, PUT');
    return res.status(405).json({ error: 'method_not_allowed' });
  }
  const user = userFor(req);
  if (!user) return res.status(401).json({ error: 'unauthorized' });
  try {
    const db = getPool();
    if (req.method === 'GET') {
      const result = await db.query('SELECT settings, favorites FROM zunuconsole.profiles WHERE account_id = $1', [user]);
      if (!result.rows.length) return res.status(404).json({ error: 'not_found' });
      return res.status(200).json({ settings: result.rows[0].settings, favorites: result.rows[0].favorites });
    }
    const data = sanitize(req.body);
    if (!data) return res.status(400).json({ error: 'invalid_profile' });
    await db.query(`INSERT INTO zunuconsole.profiles (account_id, settings, favorites, updated_at)
       VALUES ($1, $2::jsonb, $3::jsonb, now())
       ON CONFLICT (account_id) DO UPDATE SET settings = EXCLUDED.settings,
         favorites = EXCLUDED.favorites, updated_at = now()`,
      [user, JSON.stringify(data.settings), JSON.stringify(data.favorites)]);
    return res.status(200).json({ ok: true });
  } catch (err) {
    // Do not reveal SQL errors or credentials to clients.
    console.error('ZunuCloud database request failed:', err.code || err.name || 'unknown');
    return res.status(503).json({ error: 'service_unavailable' });
  }
};

// Export pure functions for offline tests; no Postgres call is made on import.
module.exports._test = { sanitize, userFor };
