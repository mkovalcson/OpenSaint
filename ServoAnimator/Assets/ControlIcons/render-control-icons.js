const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    args: ['--disable-gpu', '--disable-software-rasterizer'],
  });
  const page = await browser.newPage({ viewport: { width: 1100, height: 1100 }, deviceScaleFactor: 1 });
  const source = path.resolve(__dirname, 'control-icons-source.html').replace(/\\/g, '/');
  await page.goto(`file:///${source}`);
  await page.locator('.sheet').screenshot({ path: path.resolve(__dirname, 'control-icons-preview.png') });
  await page.locator('.sheet').evaluate(element => element.classList.add('export'));
  const icons = page.locator('svg.asset');
  for (let index = 0; index < await icons.count(); index++) {
    const icon = icons.nth(index);
    const name = await icon.getAttribute('data-name');
    const box = await icon.boundingBox();
    await page.screenshot({
      path: path.resolve(__dirname, `${name}.png`),
      clip: box,
      omitBackground: true,
    });
  }
  await browser.close();
})();
