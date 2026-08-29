#!/usr/bin/env node
/**
 * qqqm_fetch_data.js — refresh docs/assets/_qqqm_yfinance.json (real QQQM daily closes).
 * Source: Yahoo Finance public chart API (query1.finance.yahoo.com). Free, no API key.
 * Non-SLA data source; research/backtest use only. See docs/CompleteWalkthrough-*.md.
 * Usage: node qqqm_fetch_data.js
 */
const fs = require('fs');
const path = require('path');
const https = require('https');
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36';
function httpGet(hostname, apiPath) {
  return new Promise((resolve) => {
    const r = https.request({ hostname, path: apiPath, method: 'GET', headers: { 'User-Agent': UA, 'Accept': 'application/json' }, timeout: 25000 }, (res) => {
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks).toString('utf8') }));
    });
    r.on('timeout', () => { r.destroy(); resolve({ status: 0, body: 'TIMEOUT' }); });
    r.on('error', (e) => resolve({ status: 0, body: 'ERR ' + e.message }));
    r.end();
  });
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const p1 = Math.floor(Date.UTC(2021, 0, 4) / 1000);
  const p2 = Math.floor(Date.now() / 1000);
  let res = await httpGet('query1.finance.yahoo.com', `/v8/finance/chart/QQQM?period1=${p1}&period2=${p2}&interval=1d`);
  if (!(res.status === 200 && res.body.includes('"timestamp"'))) { await sleep(1500); res = await httpGet('query1.finance.yahoo.com', `/v8/finance/chart/QQQM?period1=${p1}&period2=${p2}&interval=1d`); }
  if (!(res.status === 200 && res.body.includes('"timestamp"'))) {
    console.error('FAILED to fetch QQQM from Yahoo Finance: status=' + res.status + ' body=' + res.body.slice(0, 200));
    process.exit(1);
  }
  const j = JSON.parse(res.body);
  const r = j.chart.result[0];
  const ts = r.timestamp;
  const q = r.indicators.quote[0];
  const out = {
    symbol: 'QQQM',
    source: 'query1.finance.yahoo.com/v8/finance/chart (Yahoo Finance public chart API)',
    exchange: r.meta.exchangeName,
    currency: r.meta.currency,
    fetchedAt: new Date().toISOString(),
    note: 'Real QQQM daily closes. Free public data, no SLA guarantee; research/backtest use only. NOT investment advice.',
    bars: ts.map((t, i) => ({ t: t, c: q.close[i] === null ? null : Math.round(q.close[i] * 10000) / 10000 })),
  };
  const target = path.join(__dirname, '_qqqm_yfinance.json');
  fs.writeFileSync(target, JSON.stringify(out));
  console.log(`OK: wrote ${target} with ${ts.length} bars (${new Date(ts[0] * 1000).toISOString().slice(0, 10)} .. ${new Date(ts[ts.length - 1] * 1000).toISOString().slice(0, 10)})`);
})().catch((e) => { console.error('ERR', e); process.exit(1); });