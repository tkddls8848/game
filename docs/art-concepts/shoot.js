// 시안 화면 12장을 1600×900 PNG로 찍는다.  node shoot.js   (playwright + chromium 필요)
// 폰트는 Google Fonts에서 받는다. 프록시 환경이면 HTTPS_PROXY를 읽고, 받은 파일은 fontcache/에 캐시한다.
const { chromium } = require('playwright');
const path = require('path'), fs = require('fs'), crypto = require('crypto'), { execFileSync } = require('child_process');
const ROOT = __dirname, CACHE = path.join(ROOT, 'fontcache');
fs.mkdirSync(CACHE, { recursive: true });
function fetchCached(url, ua) {
  const file = path.join(CACHE, crypto.createHash('md5').update(url + ua).digest('hex'));
  if (!fs.existsSync(file)) {
    try { execFileSync('curl', ['-sS', '--retry', '6', '--retry-all-errors', '-A', ua, '-o', file, url], { timeout: 60000 }); }
    catch (e) { try { fs.unlinkSync(file); } catch (_) {} return null; }
  }
  return fs.readFileSync(file);
}
(async () => {
  const b = await chromium.launch(process.env.HTTPS_PROXY ? { proxy: { server: process.env.HTTPS_PROXY } } : {});
  const ctx = await b.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1700, height: 1000 }, deviceScaleFactor: 1 });
  await ctx.route(/fonts\.(googleapis|gstatic)\.com/, route => {
    const req = route.request(), body = fetchCached(req.url(), req.headers()['user-agent'] || 'Mozilla/5.0');
    if (!body) return route.abort();
    route.fulfill({ status: 200, body, headers: { 'content-type': req.url().includes('css2') ? 'text/css' : 'font/woff2', 'access-control-allow-origin': '*' } });
  });
  const p = await ctx.newPage();
  await p.goto('file://' + ROOT + '/index.html', { waitUntil: 'domcontentloaded', timeout: 120000 });
  await p.evaluate(() => { document.querySelector('.wrap').style.maxWidth = '1640px'; });
  await Promise.race([p.evaluate(() => document.fonts.ready), new Promise(r => setTimeout(r, 240000))]);
  await p.evaluate(() => Promise.all([...document.images].map(i => i.complete || new Promise(r => { i.onload = i.onerror = r; }))));
  await p.waitForTimeout(1500);
  const wraps = await p.$$('.screen-wrap'), ids = await p.$$eval('.screen', els => els.map(e => e.dataset.shot));
  for (let i = 0; i < wraps.length; i++) {
    await wraps[i].scrollIntoViewIfNeeded(); await p.waitForTimeout(300);
    await wraps[i].screenshot({ path: path.join(ROOT, 'png', String(i + 1).padStart(2, '0') + '-' + ids[i] + '.png') });
  }
  await b.close();
})();
