// Dedicated NewsBlur automation account only; never accesses a Spectro database.
// Usage: node tools\newsblur-library-fixture.cjs prepare|status|cleanup
// Requires NEWSBLUR_TEST_USERNAME/PASSWORD and the existing Playwright installation.
// Journal: %LOCALAPPDATA%\Spectro\E2E\library-fixture.json (outside the repository).
// Leave the fixture prepared until signed Release UI acceptance is complete.
// Interrupted resource mutations remain pending: no adoption or destructive recovery.
// Prepare can settle an interrupted read only after canonical and feed-state verification.
// API contracts: https://www.newsblur.com/api and samuelclay/NewsBlur apps/reader/views.py.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const { createRequire } = require('node:module');

const ORIGINAL_IDS = [5771943, 10310620];
const ORIGIN = 'https://newsblur.com';
const PLANS = [
  { url: 'https://feeds.bbci.co.uk/news/world/rss.xml', folder: 0, read: true, saved: true },
  { url: 'https://feeds.npr.org/1001/rss.xml', folder: 0, read: false, saved: true },
  { url: 'https://feeds.arstechnica.com/arstechnica/index', folder: 1, read: true, saved: false },
  { url: 'https://www.nasa.gov/feed/', folder: 1, read: false, saved: false }
];
let stage = 'arguments';

class SafetyError extends Error {}
function check(condition, message) {
  if (!condition) throw new SafetyError(message);
}
const sorted = values => [...values].sort();
const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const hashFeed = hash => Number(hash.split(':')[0]);

function foldersOf(tree, parent = '', result = []) {
  check(Array.isArray(tree), 'Invalid folder tree; refusing unknown state.');
  for (const entry of tree) {
    if (Number.isSafeInteger(entry)) continue;
    check(entry && typeof entry === 'object' && Object.keys(entry).length === 1,
      'Invalid folder entry; refusing unknown state.');
    const [name] = Object.keys(entry);
    result.push({ name, parent, items: entry[name] });
    foldersOf(entry[name], name, result);
  }
  return result;
}

function validateJournal(journal) {
  check(journal && journal.version === 1 &&
    equal(journal.originalSubscriptionIDs, ORIGINAL_IDS),
  'Journal baseline or version mismatch.');
  check(['preparing', 'ready', 'cleaning', 'cleaned'].includes(journal.state),
    'Invalid journal lifecycle.');
  check(Array.isArray(journal.folders) && journal.folders.length === 2 &&
    journal.folders.every(f => /^Spectro Automation - (News|Science) - [a-f0-9]{8}$/.test(f.name) &&
      ['planned', 'created', 'deleted'].includes(f.state)) &&
    new Set(journal.folders.map(f => f.name)).size === 2, 'Invalid fixture folders.');
  check(Array.isArray(journal.feeds) && journal.feeds.length <= PLANS.length, 'Invalid fixture feeds.');
  const ids = journal.feeds.map(f => f.id);
  check(new Set(ids).size === ids.length && journal.feeds.every((f, index) =>
    Number.isSafeInteger(f.id) && f.id > 0 && !ORIGINAL_IDS.includes(f.id) &&
    f.folder === journal.folders[PLANS[index].folder].name &&
    ['created', 'deleted'].includes(f.state)), 'Invalid or baseline-overlapping fixture ownership.');
  check(Array.isArray(journal.stories) && journal.stories.length <= journal.feeds.length &&
    new Set(journal.stories.map(s => s.hash)).size === journal.stories.length &&
    journal.stories.every((s, index) =>
      s.feedId === journal.feeds[index].id && typeof s.hash === 'string' &&
      /^\d+:[a-z0-9]+$/i.test(s.hash) && hashFeed(s.hash) === s.feedId &&
      typeof s.before?.read === 'boolean' && s.before?.saved === false &&
      s.target?.read === PLANS[index].read && s.target?.saved === PLANS[index].saved),
  'Invalid fixture story ownership or state.');
  check(journal.pending === null, 'Journal has an unresolved mutation; manual review required, nothing will be deleted.');
}

function validateCatalog(catalog, journal) {
  check(catalog.feeds && !Array.isArray(catalog.feeds), 'Invalid subscription catalog.');
  const ids = Object.keys(catalog.feeds).map(Number);
  const expected = [...ORIGINAL_IDS, ...(journal?.feeds.filter(f => f.state === 'created').map(f => f.id) || [])];
  check(equal(sorted(ids), sorted(expected)), 'Unexpected subscription IDs; no unknown subscription will be changed.');
  check(catalog.feeds[5771943]?.active === true && catalog.feeds[10310620]?.active === false,
    'Baseline active/inactive state changed; refusing mutations.');
  const folders = foldersOf(catalog.folders);
  if (!journal) return;
  for (const folder of journal.folders) {
    const matches = folders.filter(f => f.name === folder.name);
    if (folder.state !== 'created') {
      check(matches.length === 0, 'Unowned or deleted fixture folder exists; refusing unknown state.');
      continue;
    }
    check(matches.length === 1 && matches[0].parent === '', 'Fixture folder moved or duplicated.');
    const children = journal.feeds.filter(f => f.state === 'created' && f.folder === folder.name).map(f => f.id);
    check(equal(sorted(matches[0].items), sorted(children)), 'Fixture folder contains unknown or missing entries.');
  }
  for (const feed of journal.feeds.filter(f => f.state === 'created')) {
    let occurrences = 0;
    function count(tree) {
      for (const item of tree) {
        if (item === feed.id) occurrences++;
        else if (item && typeof item === 'object') count(Object.values(item)[0]);
      }
    }
    count(catalog.folders);
    check(occurrences === 1, 'Fixture subscription placement changed; refusing deletion.');
  }
}

function baselineCatalog(catalog, journal) {
  const fixtureFolders = new Set(journal?.folders.map(f => f.name) || []);
  function strip(tree) {
    return tree.flatMap(item => {
      if (Number.isSafeInteger(item)) return ORIGINAL_IDS.includes(item) ? [item] : [];
      const [name] = Object.keys(item);
      return fixtureFolders.has(name) ? [] : [{ [name]: strip(item[name]) }];
    });
  }
  return {
    feeds: ORIGINAL_IDS.map(id => ({
      id, active: catalog.feeds[id].active,
      title: catalog.feeds[id].feed_title, address: catalog.feeds[id].feed_address
    })),
    folders: strip(catalog.folders)
  };
}

async function run(action) {
  check(['prepare', 'status', 'cleanup'].includes(action),
    'Usage: node tools\\newsblur-library-fixture.cjs prepare|status|cleanup');
  check(process.env.NEWSBLUR_TEST_USERNAME && process.env.NEWSBLUR_TEST_PASSWORD &&
    process.env.LOCALAPPDATA, 'Required automation environment is missing.');
  const directory = path.join(process.env.LOCALAPPDATA, 'Spectro', 'E2E');
  const journalPath = path.join(directory, 'library-fixture.json');
  const lockPath = path.join(directory, 'library-fixture.lock');
  fs.mkdirSync(directory, { recursive: true });
  stage = 'exclusive-fixture-lock';
  const lock = fs.openSync(lockPath, 'wx');
  let context;
  try {
    let journal = fs.existsSync(journalPath) ? JSON.parse(fs.readFileSync(journalPath, 'utf8')) : null;
    if (journal) {
      if (action === 'prepare' && journal.state === 'preparing' && journal.pending?.operation === 'read_story') {
        validateJournal({ ...journal, pending: null });
        check(journal.stories.some(s => s.hash === journal.pending.hash && s.target.read),
          'Pending read does not belong to a verified fixture story.');
      } else {
        validateJournal(journal);
      }
    }
    const save = () => {
      const temporary = `${journalPath}.tmp`;
      const fd = fs.openSync(temporary, 'w', 0o600);
      try {
        fs.writeFileSync(fd, JSON.stringify(journal, null, 2) + '\n');
        fs.fsyncSync(fd);
      } finally {
        fs.closeSync(fd);
      }
      fs.renameSync(temporary, journalPath);
    };
    stage = 'browser-launch';
    const moduleRoot = process.env.SPECTRO_PLAYWRIGHT_ROOT ||
      path.join(os.homedir(), '.spiderloop', 'playwright-mcp');
    const { chromium } = createRequire(path.join(moduleRoot, 'package.json'))('playwright');
    context = await chromium.launchPersistentContext(path.join(directory, 'browser-profile'), { headless: true });
    const page = await context.newPage();
    // Browser UI must not mark a restored story read; only explicit API calls below may mutate.
    await context.route('**/*', route => {
      const request = route.request();
      const url = new URL(request.url());
      const login = url.origin === ORIGIN && url.pathname === '/reader/login' && request.method() === 'POST';
      return ['GET', 'HEAD'].includes(request.method()) || login ? route.continue() : route.abort();
    });
    stage = 'authentication';
    await page.goto(`${ORIGIN}/api`, { waitUntil: 'domcontentloaded' });
    if (!await page.evaluate(() => window.NEWSBLUR?.Globals?.is_authenticated === true)) {
      await page.goto(ORIGIN, { waitUntil: 'domcontentloaded' });
      await page.locator('#id_login-username').fill(process.env.NEWSBLUR_TEST_USERNAME);
      await page.locator('#id_login-password').fill(process.env.NEWSBLUR_TEST_PASSWORD);
      await Promise.all([
        page.waitForResponse(r => new URL(r.url()).pathname === '/reader/login' && r.request().method() === 'POST'),
        page.locator('form[action="/reader/login"] input[type=submit]').click()
      ]);
      await page.goto(`${ORIGIN}/api`, { waitUntil: 'domcontentloaded' });
    }
    async function verifyIdentity() {
      check(await page.evaluate(username =>
        window.NEWSBLUR?.Globals?.is_authenticated === true &&
        window.NEWSBLUR.Globals.username === username, process.env.NEWSBLUR_TEST_USERNAME),
      'Signed-in account does not exactly match the dedicated automation username.');
    }
    await verifyIdentity();
    const clusterSafe = await page.evaluate(() => window.NEWSBLUR.Preferences.cluster_mark_read === false);
    async function api(endpoint, form, params) {
      const response = form
        ? await context.request.post(`${ORIGIN}/reader/${endpoint}`, { form, timeout: 90000, maxRedirects: 0 })
        : await context.request.get(`${ORIGIN}/reader/${endpoint}`, { params, timeout: 90000, maxRedirects: 0 });
      check(response.status() === 200, 'NewsBlur returned an unsuccessful HTTP status; response suppressed.');
      const data = await response.json();
      check(data && data.authenticated === true, 'NewsBlur API did not confirm authentication.');
      if (form) check(typeof data.code === 'number' && data.code >= 0,
        'NewsBlur rejected a fixture mutation; response suppressed, pending journal retained.');
      return data;
    }
    const catalog = () => api('feeds', null, { include_favicons: false, update_counts: false });
    async function hashes() {
      const unreadValues = [];
      // The unfiltered endpoint skips zero-count feeds when another feed has unread stories.
      // Query each owned feed separately so new subscriptions take the server's all-stories fallback.
      const feedIds = [...ORIGINAL_IDS, ...(journal?.feeds.filter(f => f.state === 'created').map(f => f.id) || [])];
      for (const feedId of feedIds) {
        const unread = await api('unread_story_hashes', null, { feed_id: feedId });
        check(unread.unread_feed_story_hashes && typeof unread.unread_feed_story_hashes === 'object',
          'Invalid canonical unread response.');
        let values = Object.values(unread.unread_feed_story_hashes).flat();
        check(values.length <= 500, 'Unexpected unread endpoint page size.');
        if (values.length === 500) {
          // This endpoint caps results at 500 with no offset. Opposite windows must overlap
          // to prove complete coverage; otherwise absence cannot establish a read state.
          const oldest = await api('unread_story_hashes', null, { feed_id: feedId, order: 'oldest' });
          check(oldest.unread_feed_story_hashes && typeof oldest.unread_feed_story_hashes === 'object',
            'Invalid oldest unread response.');
          const older = Object.values(oldest.unread_feed_story_hashes).flat();
          const newest = new Set(values);
          check(older.length <= 500 && older.some(h => newest.has(h)),
            'Unread windows do not overlap; complete state cannot be verified safely.');
          values = [...new Set([...values, ...older])];
        }
        check(values.every(h => typeof h === 'string' && hashFeed(h) === feedId),
          'Canonical unread result contains another feed.');
        unreadValues.push(...values);
      }
      const saved = await api('starred_story_hashes');
      check(Array.isArray(saved.starred_story_hashes), 'Invalid canonical saved response.');
      check([...unreadValues, ...saved.starred_story_hashes].every(h => typeof h === 'string' &&
        /^\d+:[a-z0-9]+$/i.test(h)), 'Unexpected story hash format.');
      return { unread: new Set(unreadValues), saved: new Set(saved.starred_story_hashes) };
    }
    stage = 'baseline-safety-gate';
    const initialCatalog = await catalog();
    validateCatalog(initialCatalog, journal);
    const originalCatalog = baselineCatalog(initialCatalog, journal);
    const initialHashes = await hashes();
    const originalHashes = state => ({
      unread: sorted([...state.unread].filter(h => ORIGINAL_IDS.includes(hashFeed(h)))),
      saved: sorted([...state.saved].filter(h => ORIGINAL_IDS.includes(hashFeed(h))))
    });
    const originalStoryState = originalHashes(initialHashes);
    async function guard() {
      await verifyIdentity();
      const current = await catalog();
      validateCatalog(current, journal);
      check(equal(baselineCatalog(current, journal), originalCatalog),
        'Baseline subscription properties or folder placement changed.');
      return current;
    }
    async function preserveBaseline() {
      await guard();
      check(equal(originalHashes(await hashes()), originalStoryState),
        'Baseline story state changed during this operation; no automatic restoration attempted.');
    }
    async function mutate(endpoint, form, pending, verifyAndRecord) {
      await guard();
      journal.pending = pending;
      save();
      stage = endpoint;
      const response = await api(endpoint, form);
      await verifyAndRecord(response);
      journal.pending = null;
      save();
      await guard();
    }
    async function stories(feedId) {
      check(journal.feeds.some(f => f.id === feedId && f.state === 'created') &&
        !ORIGINAL_IDS.includes(feedId), 'Story retrieval is restricted to verified fixture feeds.');
      const data = await api(`feed/${feedId}`, null,
        { page: 1, read_filter: 'all', order: 'newest', include_story_content: false });
      check(Array.isArray(data.stories) && data.stories.every(s =>
        typeof s.story_hash === 'string' && hashFeed(s.story_hash) === feedId),
      'Unexpected feed story response.');
      return data.stories;
    }
    async function waitForState(story, target) {
      for (let attempt = 0; attempt < 10; attempt++) {
        const state = await hashes();
        if (!state.unread.has(story.hash) === target.read && state.saved.has(story.hash) === target.saved) return;
        await page.waitForTimeout(1000);
      }
      throw new SafetyError('Canonical fixture story state did not converge; pending journal retained.');
    }
    function verifyReadResponse(response, story) {
      // Deployed API versions may omit echoes, and an idempotent read returns empty lists.
      check((response.story_hashes === undefined ||
        (Array.isArray(response.story_hashes) && response.story_hashes.every(h => h === story.hash))) &&
        (response.feed_ids === undefined ||
        (Array.isArray(response.feed_ids) && response.feed_ids.every(id => Number(id) === story.feedId))),
      'Read mutation echoed unexpected stories or feeds.');
    }
    if (journal?.pending) {
      stage = 'verify-pending-read';
      const story = journal.stories.find(s => s.hash === journal.pending.hash);
      check(action === 'prepare' && clusterSafe && story, 'Pending mutation cannot be safely reconciled.');
      await waitForState(story, story.target);
      const observed = (await stories(story.feedId)).find(s => s.story_hash === story.hash);
      check(observed?.read_status === 1 && Boolean(observed.starred) === story.target.saved,
        'Pending read is not independently confirmed by the fixture feed.');
      await preserveBaseline();
      journal.pending = null;
      save();
    }
    if (action === 'prepare') {
      check(!journal || ['preparing', 'ready'].includes(journal.state),
        'A cleaning or cleaned journal exists; refusing to recreate resources.');
      if (!journal) {
        const suffix = randomUUID().slice(0, 8);
        journal = {
          version: 1, state: 'preparing', originalSubscriptionIDs: [...ORIGINAL_IDS],
          folders: ['News', 'Science'].map(label => ({
            name: `Spectro Automation - ${label} - ${suffix}`, state: 'planned'
          })),
          feeds: [], stories: [], pending: null
        };
        validateCatalog(initialCatalog, journal);
        save();
      }
      if (journal.state === 'preparing') {
        check(clusterSafe, 'Cluster mark-read must already be disabled; baseline-affecting propagation is unsafe.');
        for (const folder of journal.folders.filter(f => f.state === 'planned')) {
          await mutate('add_folder', { folder: folder.name }, { operation: 'add_folder', folder: folder.name },
            async () => {
              const current = await catalog();
              const matches = foldersOf(current.folders).filter(f => f.name === folder.name);
              check(matches.length === 1 && matches[0].parent === '' && matches[0].items.length === 0,
                'New folder could not be verified.');
              folder.state = 'created';
            });
        }
        for (const plan of PLANS.slice(journal.feeds.length)) {
          const folder = journal.folders[plan.folder].name;
          await mutate('add_url', { url: plan.url, folder, auto_active: true },
            { operation: 'add_url', folder, slot: journal.feeds.length }, async response => {
              const id = response.feed?.id;
              check(Number.isSafeInteger(id) && !ORIGINAL_IDS.includes(id) &&
                !journal.feeds.some(f => f.id === id), 'Added feed is not a new distinct subscription.');
              const current = await catalog();
              check(current.feeds[id]?.active === true, 'New feed is not active.');
              journal.feeds.push({ id, folder, state: 'created' });
              validateCatalog(current, journal);
            });
        }
        for (const [index, feed] of journal.feeds.entries()) {
          stage = 'select-fresh-fixture-story';
          const plan = PLANS[index];
          let story = journal.stories[index];
          if (!story) {
            let candidate;
            let state;
            for (let attempt = 0; attempt < 6 && !candidate; attempt++) {
              const sample = await stories(feed.id);
              state = await hashes();
              candidate = sample.find(s => state.unread.has(s.story_hash) && !state.saved.has(s.story_hash)) ||
                sample.find(s => !state.saved.has(s.story_hash));
              if (!candidate) await page.waitForTimeout(5000);
            }
            check(candidate, 'No unsaved fixture story available; existing subscriptions untouched.');
            story = { feedId: feed.id, hash: candidate.story_hash,
              before: { read: !state.unread.has(candidate.story_hash), saved: false },
              target: { read: plan.read, saved: plan.saved } };
            journal.stories.push(story);
            save();
          }
          let observed = await hashes();
          if (plan.saved && !observed.saved.has(story.hash)) {
            await mutate('mark_story_hash_as_starred', { story_hash: story.hash },
              { operation: 'save_story', hash: story.hash },
              () => waitForState(story, { read: !observed.unread.has(story.hash), saved: true }));
          }
          observed = await hashes();
          if (plan.read && observed.unread.has(story.hash)) {
            check(clusterSafe, 'Cross-feed clustered mark-read is unsafe.');
            await mutate('mark_story_hashes_as_read', { story_hash: story.hash },
              { operation: 'read_story', hash: story.hash }, response => {
                verifyReadResponse(response, story);
                return waitForState(story, story.target);
              });
          } else if (!plan.read && !observed.unread.has(story.hash)) {
            await mutate('mark_story_hash_as_unread', { story_hash: story.hash },
              { operation: 'unread_story', hash: story.hash }, response => {
                check(response.story_hash === story.hash && response.feed_id === story.feedId,
                  'Unread mutation returned an unexpected story or feed.');
                return waitForState(story, story.target);
              });
          }
          await waitForState(story, story.target);
        }
        await preserveBaseline();
        journal.state = 'ready';
        save();
      }
    } else if (action === 'cleanup') {
      check(journal, 'No ownership journal exists; nothing will be deleted.');
      check(['ready', 'preparing', 'cleaning', 'cleaned'].includes(journal.state), 'Unsafe cleanup lifecycle.');
      journal.state = 'cleaning';
      save();
      for (const story of journal.stories) {
        const state = await hashes();
        // Only undo saves made by prepare, not unknown user/UI saves.
        if (story.target.saved && state.saved.has(story.hash)) {
          check(journal.feeds.some(f => f.id === story.feedId && f.state === 'created'),
            'Saved fixture story has no verified live subscription.');
          await mutate('mark_story_hash_as_unstarred', { story_hash: story.hash },
            { operation: 'unsave_story', hash: story.hash }, async () => {
              check(!(await hashes()).saved.has(story.hash), 'Fixture save was not removed.');
            });
        }
        if (story.before.read !== story.target.read &&
          journal.feeds.some(f => f.id === story.feedId && f.state === 'created')) {
          const current = await hashes();
          if (!current.unread.has(story.hash) !== story.before.read) {
            check(clusterSafe, 'Cluster mark-read must be disabled before restoring fixture state.');
            const endpoint = story.before.read ? 'mark_story_hashes_as_read' : 'mark_story_hash_as_unread';
            await mutate(endpoint, { story_hash: story.hash },
              { operation: 'restore_story_read', hash: story.hash },
              response => {
                if (story.before.read) verifyReadResponse(response, story);
                return waitForState(story, { read: story.before.read, saved: current.saved.has(story.hash) });
              });
          }
        }
      }
      for (const feed of journal.feeds.filter(f => f.state === 'created')) {
        check(![...(await hashes()).saved].some(h => hashFeed(h) === feed.id),
          'A fixture feed has an unjournaled saved story; cleanup stopped without deleting it.');
        await mutate('delete_feed', { feed_id: feed.id, in_folder: feed.folder },
          { operation: 'delete_feed', feedId: feed.id }, async () => {
            check(!(await catalog()).feeds[feed.id], 'Fixture subscription deletion was not verified.');
            feed.state = 'deleted';
          });
      }
      for (const folder of journal.folders.filter(f => f.state === 'created')) {
        // Never supply feed IDs to folder deletion; the ownership guard requires an empty folder.
        await mutate('delete_folder', { folder_name: folder.name, in_folder: '' },
          { operation: 'delete_folder', folder: folder.name }, async () => {
            check(!foldersOf((await catalog()).folders).some(f => f.name === folder.name),
              'Empty fixture folder deletion was not verified.');
            folder.state = 'deleted';
          });
      }
      await preserveBaseline();
      journal.state = 'cleaned';
      save();
    }
    stage = 'final-verification';
    const current = await guard();
    const state = await hashes();
    const matrix = [];
    const feeds = [];
    for (const feed of journal?.feeds.filter(f => f.state === 'created') || []) {
      const sample = await stories(feed.id);
      const entry = journal.stories.find(s => s.feedId === feed.id);
      if (entry) {
        const story = sample.find(s => s.story_hash === entry.hash) ||
          (await api('starred_stories', null, { h: entry.hash, include_story_content: false })).stories
            .find(s => s.story_hash === entry.hash);
        check(story, 'Fixture story is not in the latest feed page or saved collection.');
        matrix.push({ feedId: feed.id, hash: entry.hash, title: story.story_title,
          read: !state.unread.has(entry.hash), saved: state.saved.has(entry.hash),
          matchesTarget: !state.unread.has(entry.hash) === entry.target.read &&
            state.saved.has(entry.hash) === entry.target.saved });
      }
      feeds.push({ id: feed.id, title: current.feeds[feed.id].feed_title, folder: feed.folder,
        unreadCount: [...state.unread].filter(h => hashFeed(h) === feed.id).length,
        savedCount: [...state.saved].filter(h => hashFeed(h) === feed.id).length,
        latestPage: { total: sample.length,
          read: sample.filter(s => !state.unread.has(s.story_hash)).length,
          unread: sample.filter(s => state.unread.has(s.story_hash)).length } });
    }
    if (action === 'prepare') {
      check(journal.state === 'ready' && feeds.length === 4 && matrix.length === 4 &&
        matrix.every(s => s.matchesTarget), 'Prepared fixture does not satisfy the four-state matrix.');
    }
    await preserveBaseline();
    return { action, ready: journal?.state === 'ready' && matrix.length === 4 &&
        matrix.every(s => s.matchesTarget), state: journal?.state || 'absent',
      identityVerified: true, baselinePreserved: true,
      originalSubscriptionIDs: ORIGINAL_IDS, subscriptionCount: Object.keys(current.feeds).length,
      journal: journal ? journalPath : null, feeds, matrix,
      matrixCounts: { total: matrix.length, read: matrix.filter(s => s.read).length,
        unread: matrix.filter(s => !s.read).length, saved: matrix.filter(s => s.saved).length,
        unsaved: matrix.filter(s => !s.saved).length },
      cleanupCommand: 'node tools\\newsblur-library-fixture.cjs cleanup' };
  } finally {
    try {
      if (context) await context.close();
    } finally {
      fs.closeSync(lock);
      fs.unlinkSync(lockPath);
    }
  }
}

module.exports = { validateCatalog, validateJournal, baselineCatalog, run };
if (require.main === module) {
  run(process.argv[2]).then(result => console.log(JSON.stringify(result, null, 2))).catch(error => {
    console.error(JSON.stringify({ ready: false, stage,
      reason: error instanceof SafetyError ? error.message :
        'Browser, filesystem, or API operation failed; sensitive diagnostics suppressed. Inspect journal status before retrying.' }));
    process.exitCode = 1;
  });
}
