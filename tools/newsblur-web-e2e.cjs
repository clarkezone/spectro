const { createRequire } = require('node:module');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');

const [action, feedTitle, storyTitle, storyHash] = process.argv.slice(2);
if (!['saved', 'unsaved', 'save', 'unsave', 'read', 'mark-unread'].includes(action) || !feedTitle || !storyTitle || !storyHash) {
  console.error('Usage: node tools\\newsblur-web-e2e.cjs saved|unsaved|save|unsave|read|mark-unread <feed-title> <story-title> <story-hash>');
  process.exit(1);
}
const moduleRoot = process.env.SPECTRO_PLAYWRIGHT_ROOT
  || path.join(os.homedir(), '.spiderloop', 'playwright-mcp');
const { chromium } = createRequire(path.join(moduleRoot, 'package.json'))('playwright');
let stage = 'launch';

async function run() {
  const context = await chromium.launchPersistentContext(
    path.join(process.env.LOCALAPPDATA, 'Spectro', 'E2E', 'browser-profile'),
    { headless: true });
  try {
    const page = await context.newPage();
    stage = 'navigation';
    await page.goto('https://newsblur.com/', { waitUntil: 'domcontentloaded' });
    if (await page.locator('#id_login-password').isVisible()) {
      assert.ok(process.env.NEWSBLUR_TEST_USERNAME && process.env.NEWSBLUR_TEST_PASSWORD, 'Missing credentials');
      await page.locator('#id_login-username').fill(process.env.NEWSBLUR_TEST_USERNAME);
      await page.locator('#id_login-password').fill(process.env.NEWSBLUR_TEST_PASSWORD);
      await Promise.all([
        page.waitForResponse(r => r.url().includes('/reader/login') && r.request().method() === 'POST'),
        page.locator('form[action="/reader/login"] input[type=submit]').click()
      ]);
      await page.locator('#id_login-password').waitFor({ state: 'hidden' });
    }
    if (action === 'read') {
      stage = 'server-read-precondition';
      const response = await context.request.get('https://newsblur.com/reader/unread_story_hashes');
      assert.equal(response.status(), 200);
      const data = await response.json();
      assert.equal(data.authenticated, true);
      assert.ok(data.unread_feed_story_hashes && typeof data.unread_feed_story_hashes === 'object');
      assert.equal(Object.values(data.unread_feed_story_hashes).flat().includes(storyHash), false,
        'The server must already be read before opening the web feed');
    }
    stage = 'select-feed';
    await page.locator('.feed').getByText(feedTitle, { exact: true }).click();
    // Restored feed rows can precede the live response, which replaces rows and closes menus.
    await page.waitForLoadState('networkidle');
    stage = 'locate-story';
    const title = page.locator('.NB-storytitles-title:visible').filter({ hasText: storyTitle });
    await title.waitFor({ state: 'visible' });
    assert.equal(await title.count(), 1, 'Story title must identify exactly one visible story');
    assert.equal(await title.innerText(), storyTitle);
    const row = title.locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " NB-story-title ")][1]');
    if (action === 'read' || action === 'mark-unread') {
      const expectedRead = action === 'read';
      if (!expectedRead) {
        stage = 'web-unread-mutation';
        assert.equal(await row.evaluate(e => e.classList.contains('read')), true);
        await title.click({ button: 'right' });
        await Promise.all([
          page.waitForResponse(r => /mark_story(_hash)?_as_unread/.test(r.url()) && r.request().method() === 'POST'),
          page.locator('.NB-menu-manage-story-unread:visible').click()
        ]);
      }
      stage = 'read-state-verification';
      await page.waitForFunction(({ titleText, expected }) => {
        const element = [...document.querySelectorAll('.NB-storytitles-title')]
          .find(e => e.textContent.trim() === titleText && e.getClientRects().length);
        return element?.closest('.NB-story-title')?.classList.contains('read') === expected;
      }, { titleText: storyTitle, expected: expectedRead }, { timeout: 15000 });
      let observedRead;
      let observations = 0;
      const deadline = Date.now() + 10000;
      do {
        const response = await context.request.get('https://newsblur.com/reader/unread_story_hashes');
        assert.equal(response.status(), 200);
        const data = await response.json();
        assert.equal(data.authenticated, true);
        assert.ok(data.unread_feed_story_hashes && typeof data.unread_feed_story_hashes === 'object');
        observedRead = !Object.values(data.unread_feed_story_hashes).flat().includes(storyHash);
        observations++;
        if (observedRead === expectedRead) break;
        await page.waitForTimeout(1000);
      } while (Date.now() < deadline);
      assert.equal(observedRead, expectedRead, 'Canonical unread set did not converge within 10 seconds');
      console.log(JSON.stringify({ passed: true, action, storyHash, webRead: expectedRead, serverRead: expectedRead, observations }));
      return;
    }
    const desired = action === 'saved' || action === 'save';
    const before = await row.evaluate(e => e.classList.contains('NB-story-starred'));
    if (action === 'save' || action === 'unsave') {
      stage = 'web-mutation';
      assert.notEqual(before, desired, 'Mutation must actually change the starting state');
      await title.click({ button: 'right' });
      await Promise.all([
        page.waitForResponse(r => /mark_story(_hash)?_as_(un)?starred/.test(r.url()) && r.request().method() === 'POST'),
        page.locator('.NB-menu-manage-story-star:visible').click()
      ]);
    }
    stage = 'web-verification';
    await page.waitForFunction(({ titleText, expected }) => {
      const title = [...document.querySelectorAll('.NB-storytitles-title')]
        .find(e => e.textContent.trim() === titleText && e.getClientRects().length);
      return title?.closest('.NB-story-title')?.classList.contains('NB-story-starred') === expected;
    }, { titleText: storyTitle, expected: desired }, { timeout: 15000 });

    stage = 'server-verification';
    const response = await context.request.get('https://newsblur.com/reader/starred_stories');
    assert.equal(response.status(), 200);
    const data = await response.json();
    assert.equal(data.authenticated, true);
    // This fixture account has a small saved collection. Never infer absence from an incomplete page.
    const hashes = new Set(data.stories.map(story => story.story_hash));
    if (!desired) assert.ok(data.stories.length === 0, 'Absence verification requires an empty saved fixture collection');
    assert.equal(hashes.has(storyHash), desired);
    console.log(JSON.stringify({ passed: true, action, storyHash, webSaved: desired, serverSaved: desired }));
  } finally {
    await context.close();
  }
}

run().catch(error => {
  const safeValue = value => typeof value === 'boolean' || typeof value === 'number' ? value : undefined;
  console.error(JSON.stringify({
    passed: false, action, stage, errorType: error.name,
    actual: safeValue(error.actual), expected: safeValue(error.expected),
    reason: 'Browser assertion or service request failed; sensitive diagnostics suppressed.'
  }));
  process.exitCode = 1;
});
