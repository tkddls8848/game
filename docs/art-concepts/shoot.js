// 남아 있는 시안 화면을 1600×900 PNG로 찍는다.  node shoot.js   (playwright + chromium 필요)
// 파일명의 번호는 index.html의 ORDER(원래 순서)를 쓴다. 탈락으로 번호를 다시 매기면
// 이미 있는 png/NN-*.png와 어긋난다.
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
  // 번호는 화면에 이미 렌더된 것을 읽는다(페이지 전역 함수에 의존하지 않는다).
  const nums = await p.$$eval('.concept h2 small', els => els.map(e => e.textContent.trim().split(' ')[0]));
  for (let i = 0; i < wraps.length; i++) {
    await wraps[i].scrollIntoViewIfNeeded(); await p.waitForTimeout(300);
    await wraps[i].screenshot({ path: path.join(ROOT, 'png', nums[i] + '-' + ids[i] + '.png') });
  }
  await b.close();
})();
