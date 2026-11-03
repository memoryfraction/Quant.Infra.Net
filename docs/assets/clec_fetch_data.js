#!/usr/bin/env node
/**
 * clec_fetch_data.js — refresh docs/assets/_clec_yfinance.json (real daily closes for the CLEC
 * allocation backtests: QQQ = base index, QLD = 2x leveraged ETF, BIL = cash / T-bills).
 *
 * Closes are Yahoo's dividend- and split-ADJUSTED closes ("adjclose"), so the backtest is a
 * total-return backtest (BIL's T-bill interest arrives as distributions, which only show up in
 * the adjusted series).
 *
 * Source: Yahoo Finance public chart API (query1.finance.yahoo.com). Free, no API key.
 * Non-SLA data source; research/backtest use only. NOT investment advice.
 * Usage: node docs/assets/clec_fetch_data.js
 */
const fs = require('fs');
const path = require('path');
const https = require('https');

const SYMBOLS = ['QQQ', 'QLD', 'BIL'];
const START = Date.UTC(2007, 5, 1); // BIL starts 2007-05-30; QLD 2006-06-21 → common window from 2007-06
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

async function fetchSymbol(sym) {
  const p1 = Math.floor(START / 1000);
  const p2 = Math.floor(Date.now() / 1000);
  const apiPath = `/v8/finance/chart/${sym}?period1=${p1}&period2=${p2}&interval=1d&events=div,split`;
  let res;
  for (let attempt = 0; attempt < 3; attempt++) {
    res = await httpGet('query1.finance.yahoo.com', apiPath);
    if (res.status === 200 && res.body.includes('"timestamp"')) break;
    await sleep(1500 * (attempt + 1));
  }
  if (!(res.status === 200 && res.body.includes('"timestamp"'))) {
    throw new Error(`FAILED to fetch ${sym}: status=${res.status} body=${res.body.slice(0, 200)}`);
  }
  const r = JSON.parse(res.body).chart.result[0];
  const adj = r.indicators.adjclose && r.indicators.adjclose[0] && r.indicators.adjclose[0].adjclose;
  if (!adj) throw new Error(`${sym}: no adjclose series in response`);
  const bars = [];
  r.timestamp.forEach((t, i) => {
    if (adj[i] !== null && adj[i] !== undefined) bars.push({ t, c: Math.round(adj[i] * 10000) / 10000 });
  });
  return { symbol: sym, exchange: r.meta.exchangeName, currency: r.meta.currency, bars };
}

(async () => {
  const series = [];
  for (const s of SYMBOLS) {
    const x = await fetchSymbol(s);
    console.log(`${s}: ${x.bars.length} bars (${new Date(x.bars[0].t * 1000).toISOString().slice(0, 10)} .. ${new Date(x.bars[x.bars.length - 1].t * 1000).toISOString().slice(0, 10)})`);
    series.push(x);
    await sleep(800);
  }
  const out = {
    source: 'query1.finance.yahoo.com/v8/finance/chart (Yahoo Finance public chart API), adjclose',
    fetchedAt: new Date().toISOString(),
    note: 'Real daily ADJUSTED closes (dividends + splits). Free public data, no SLA guarantee; research/backtest use only. NOT investment advice.',
    series,
  };
  const target = path.join(__dirname, '_clec_yfinance.json');
  fs.writeFileSync(target, JSON.stringify(out));
  console.log(`OK: wrote ${target}`);
})().catch((e) => { console.error('ERR', e.message || e); process.exit(1); });
