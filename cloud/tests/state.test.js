'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { _test } = require('../api/state');

test('accepts only explicitly synced preference keys and favorites', () => {
  assert.deepEqual(_test.sanitize({settings: [{ key: 'ui.theme', value: 'oled'}], favorites: ['help', 'ui.theme']}),
    { settings: [{key: 'ui.theme', value: 'oled'}], favorites: ['help', 'ui.theme'] });
  assert.equal(_test.sanitize({ settings: [{key:'database.password',value:'secret'}], favorites: [] }), null);
  assert.equal(_test.sanitize({ settings: [], favorites: ['../../secret'] }), null);
  assert.equal(_test.sanitize({settings: 'bad', favorites: []}), null);
});

test('token authentication requires a matching SHA-256 digest', () => {
  const token = 'abcdef0123456789abcdef0123456789abcdef';
  const digest = crypto.createHash('sha256').update(token).digest('hex');
  process.env.ZUNU_TOKEN_HASHES = JSON.stringify({ [digest]: 'zunu-test-user' });
  assert.equal(_test.userFor({headers: {authorization: `Bearer ${token}`}}), 'zunu-test-user');
  assert.equal(_test.userFor({headers: {authorization: 'Bearer not-your-token-12345678901234567890'}}), null);
  assert.equal(_test.userFor({headers: {}}), null);
  delete process.env.ZUNU_TOKEN_HASHES;
});
