// Dedicated NewsBlur automation account only; never accesses a Spectro database.
// Usage: node tools\newsblur-library-fixture.cjs prepare|status|cleanup|probe|probe-coverage|recover-add [--large]
// Probe requires an already authenticated dedicated browser profile: GETs only, no login.
// It reports catalog deltas and hash shapes without raw payloads or journal changes.
// Probe emits two JSON records: inventory first, then protocol (or a sanitized failure).
// Probe-coverage reports per-feed newest/oldest unread window counts without story hashes.
// Pending mutations stay blocked even if probe observes the pre-mutation inventory.
// Explicit recover-add only reconciles one committed pending add_url after two stable catalogs.
// It requires the authenticated profile, makes no remote mutations, and records a local receipt.
// HTTP 429 cooldowns and API pacing persist in library-fixture-throttle.json under the same lock.
// Legacy interrupted cleanup waits six minutes from the journal's last write, not from restart.
// Cooldown is at least six minutes plus 10-30 seconds of jitter, honoring longer Retry-After.
// API requests are spaced at least three seconds apart, including across restarts.
// Saved-story body requests additionally remain at least eight seconds apart.
// --large: 25 added feeds (26 active including Guardian); default: four added feeds.
// The journal owns the size on resume/status/cleanup; --large cannot upgrade an existing plan.
// Both sizes mutate stories only in the original four feeds. Large folders alternate.
// Baseline unread snapshots cover only ORIGINAL_IDS; matrix unread checks cover only four feeds.
// Display-only feeds keep global saved-story cleanup checks; their unreadCount is not enumerated.
// A cleaned journal must be reviewed and archived externally before a new prepare.
// Free accounts may cap active feeds; prepare stops with pending ownership on rejection.
// Requires NEWSBLUR_TEST_USERNAME/PASSWORD and the existing Playwright installation.
// Journal: %LOCALAPPDATA%\Spectro\E2E\library-fixture.json (outside the repository).
// Leave the fixture prepared until signed Release UI acceptance is complete.
// Interrupted resource mutations remain pending unless explicitly verified by recover-add.
// No arbitrary resource adoption or destructive recovery.
// Prepare can settle an interrupted read only after canonical and feed-state verification.
// API contracts: https://www.newsblur.com/api and samuelclay/NewsBlur apps/reader/views.py.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { randomUUID, createHash, randomInt } = require('node:crypto');
const { createRequire } = require('node:module');

const ORIGINAL_IDS = [5771943, 10310620];
const ORIGIN = 'https://newsblur.com';
const COOLDOWN_MS = 6 * 60 * 1000;
// load_feeds allows 60 requests across current+previous minute buckets (shared session key).
// Source: NewsBlur main apps/reader/views.py and utils/ratelimit.py. delete_feed has no decorator.
const REQUEST_INTERVAL_MS = 3 * 1000;
const SAVED_BODY_INTERVAL_MS = 8 * 1000;
const PLANS = [
  { url: 'https://feeds.bbci.co.uk/news/world/rss.xml', folder: 0, read: true, saved: true },
  { url: 'https://feeds.npr.org/1001/rss.xml', folder: 0, read: false, saved: true },
  { url: 'https://feeds.arstechnica.com/arstechnica/index', folder: 1, read: true, saved: false },
  { url: 'https://www.nasa.gov/feed/', folder: 1, read: false, saved: false }
];
// Long-established publishers with distinct feeds; redirects/availability still require live verification.
const LARGE_PLANS = [
  ...PLANS,
  ...[
    'https://rss.nytimes.com/services/xml/rss/nyt/World.xml',
    'https://techcrunch.com/feed/',
    'https://www.wired.com/feed/rss',
    'https://www.theverge.com/rss/index.xml',
    'https://www.engadget.com/rss.xml',
    'https://rss.slashdot.org/Slashdot/slashdotMain',
    'https://news.ycombinator.com/rss',
    'https://xkcd.com/rss.xml',
    'https://www.sciencedaily.com/rss/all.xml',
    'https://phys.org/rss-feed/',
    'https://www.nature.com/nature.rss',
    'https://www.sciencenews.org/feed',
    'https://www.newscientist.com/feed/home/',
    'https://www.smithsonianmag.com/rss/latest_articles/',
    'https://www.eff.org/rss/updates.xml',
    'https://krebsonsecurity.com/feed/',
    'https://www.schneier.com/feed/atom/',
    'https://daringfireball.net/feeds/main',
    'https://lwn.net/headlines/rss',
    'https://www.theregister.com/headlines.atom',
    'https://www.propublica.org/feeds/propublica/main'
  ].map(url => ({ url }))
].map((plan, index) => ({ ...plan, folder: index % 2 }));
let stage = 'arguments';

class SafetyError extends Error {}
function check(condition, message) {
  if (!condition) throw new SafetyError(message);
}
const sorted = values => [...values].sort();
const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const hashFeed = hash => Number(hash.split(':')[0]);

function plansFor(journal) {
  // Version 1 journals written before size selection always used the original four.
  check(journal.plan === undefined || ['standard', 'large'].includes(journal.plan),
    'Invalid journal fixture plan.');
  return journal.plan === 'large' ? LARGE_PLANS : PLANS;
}

function verificationFeedIds(journal) {
  return [...ORIGINAL_IDS, ...(journal?.feeds.slice(0, PLANS.length)
    .filter(feed => feed.state === 'created').map(feed => feed.id) || [])];
}

function unreadWindowValues(data, feedId) {
  const groups = data.unread_feed_story_hashes;
  check(groups && typeof groups === 'object' && !Array.isArray(groups) &&
    Object.keys(groups).every(key => key === String(feedId)) &&
    Object.values(groups).every(Array.isArray),
  `Invalid canonical unread response scope for feed ${feedId}.`);
  const values = groups[feedId] || [];
  check(values.length <= 500 && values.every(hash => typeof hash === 'string' &&
    /^\d+:[a-z0-9]+$/i.test(hash) && hashFeed(hash) === feedId) &&
    new Set(values).size === values.length,
  `Invalid canonical unread window for feed ${feedId}.`);
  return values;
}

function retryAfterInfo(raw, now = Date.now()) {
  let label = 'absent';
  let until = null;
  if (raw !== undefined) {
    if (/^\d{1,10}$/.test(raw)) {
      label = `${Number(raw)} seconds`;
      until = now + Number(raw) * 1000;
    }
    else if (/^(Mon|Tue|Wed|Thu|Fri|Sat|Sun), \d{2} (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(raw) &&
      Number.isFinite(Date.parse(raw))) {
      label = new Date(raw).toUTCString();
      until = Date.parse(raw);
    } else label = 'invalid (suppressed)';
  }
  return { label, until };
}

function httpFailure(response, endpoint) {
  return `NewsBlur ${endpoint} HTTP ${response.status()}; response suppressed, pending journal unchanged.`;
}

function writeJsonAtomic(file, value) {
  const temporary = `${file}.tmp`;
  const fd = fs.openSync(temporary, 'w', 0o600);
  try {
    fs.writeFileSync(fd, JSON.stringify(value, null, 2) + '\n');
    fs.fsyncSync(fd);
  } finally {
    fs.closeSync(fd);
  }
  fs.renameSync(temporary, file);
}

function validateThrottle(throttle) {
  const timestamp = value => value === null ||
    (typeof value === 'string' && Number.isFinite(Date.parse(value)));
  check(throttle && throttle.version === 1 &&
    timestamp(throttle.lastRequestAt) && timestamp(throttle.nextRequestAt) &&
    (throttle.nextSavedBodyAt === undefined || timestamp(throttle.nextSavedBodyAt)) &&
    (throttle.lastRequestAt === null) === (throttle.nextRequestAt === null) &&
    (throttle.nextRequestAt === null ||
      Date.parse(throttle.nextRequestAt) - Date.parse(throttle.lastRequestAt) >= REQUEST_INTERVAL_MS),
  'Invalid persistent fixture pacing; no HTTP requests permitted.');
  if (throttle.cooldown !== null) {
    const cooldown = throttle.cooldown;
    check(cooldown && ['http-429', 'legacy-cleanup'].includes(cooldown.reason) &&
      typeof cooldown.observedAt === 'string' && typeof cooldown.until === 'string' &&
      Number.isFinite(Date.parse(cooldown.observedAt)) && Number.isFinite(Date.parse(cooldown.until)) &&
      Date.parse(cooldown.until) - Date.parse(cooldown.observedAt) >= COOLDOWN_MS,
    'Invalid persistent fixture cooldown; no HTTP requests permitted.');
  }
}

function cooldownRecord(reason, retryAfter, now = Date.now()) {
  return { reason, observedAt: new Date(now).toISOString(),
    until: new Date(Math.max(now + COOLDOWN_MS, retryAfter?.until || 0) +
      randomInt(10000, 30001)).toISOString() };
}

function shapeOf(value, depth = 0) {
  const type = value === null ? 'null' : Array.isArray(value) ? 'array' : typeof value;
  if (type !== 'array' && type !== 'object') return { type };
  const values = Object.values(value);
  const result = { type, count: values.length };
  if (type === 'object') {
    const keys = Object.keys(value);
    result.numericKeyCount = keys.filter(key => /^\d+$/.test(key)).length;
    result.hashKeyCount = keys.filter(key => /^\d+:[a-z0-9]+$/i.test(key)).length;
  }
  result.valueTypes = {};
  for (const item of values) {
    const itemType = item === null ? 'null' : Array.isArray(item) ? 'array' : typeof item;
    result.valueTypes[itemType] = (result.valueTypes[itemType] || 0) + 1;
  }
  // Bounded structural samples only: never keys, hashes, timestamps, or story values.
  if (depth < 3) result.sampleShapes = values.slice(0, 3).map(item => shapeOf(item, depth + 1));
  return result;
}

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
  const plans = plansFor(journal);
  check(['preparing', 'ready', 'cleaning', 'cleaned'].includes(journal.state),
    'Invalid journal lifecycle.');
  check(Array.isArray(journal.folders) && journal.folders.length === 2 &&
    journal.folders.every(f => /^Spectro Automation - (News|Science) - [a-f0-9]{8}$/.test(f.name) &&
      ['planned', 'created', 'deleted'].includes(f.state)) &&
    new Set(journal.folders.map(f => f.name)).size === 2, 'Invalid fixture folders.');
  check(Array.isArray(journal.feeds) && journal.feeds.length <= plans.length, 'Invalid fixture feeds.');
  const ids = journal.feeds.map(f => f.id);
  check(new Set(ids).size === ids.length && journal.feeds.every((f, index) =>
    Number.isSafeInteger(f.id) && f.id > 0 && !ORIGINAL_IDS.includes(f.id) &&
    f.folder === journal.folders[plans[index].folder].name &&
    ['created', 'deleted'].includes(f.state)), 'Invalid or baseline-overlapping fixture ownership.');
  check(Array.isArray(journal.stories) && journal.stories.length <= Math.min(journal.feeds.length, PLANS.length) &&
    new Set(journal.stories.map(s => s.hash)).size === journal.stories.length &&
    journal.stories.every((s, index) =>
      s.feedId === journal.feeds[index].id && typeof s.hash === 'string' &&
      /^\d+:[a-z0-9]+$/i.test(s.hash) && hashFeed(s.hash) === s.feedId &&
      typeof s.before?.read === 'boolean' && s.before?.saved === false &&
      s.target?.read === plans[index].read && s.target?.saved === plans[index].saved),
  'Invalid fixture story ownership or state.');
  if (journal.recoveredAdds !== undefined) {
    check(Array.isArray(journal.recoveredAdds) && journal.recoveredAdds.length <= journal.feeds.length &&
      new Set(journal.recoveredAdds.map(receipt => receipt.slot)).size === journal.recoveredAdds.length &&
      journal.recoveredAdds.every(receipt =>
        receipt.operation === 'add_url' && receipt.plan === (journal.plan || 'standard') &&
        Number.isSafeInteger(receipt.slot) && receipt.slot >= 0 && receipt.slot < journal.feeds.length &&
        receipt.feedId === journal.feeds[receipt.slot].id &&
        receipt.folder === journal.feeds[receipt.slot].folder && receipt.url === plans[receipt.slot].url &&
        typeof receipt.catalogSha256 === 'string' && /^[a-f0-9]{64}$/.test(receipt.catalogSha256) &&
        Array.isArray(receipt.verifiedAt) && receipt.verifiedAt.length === 2 &&
        receipt.verifiedAt.every(time => typeof time === 'string' && Number.isFinite(Date.parse(time))) &&
        Date.parse(receipt.verifiedAt[1]) - Date.parse(receipt.verifiedAt[0]) >= 1500),
    'Invalid recovered-add verification receipt.');
  }
  check(journal.pending === null, 'Journal has an unresolved mutation; manual review required, nothing will be deleted.');
}

function pendingAddIntent(journal) {
  check(journal && journal.state === 'preparing' && journal.pending?.operation === 'add_url',
    'Recovery requires a preparing journal with a pending add_url.');
  validateJournal({ ...journal, pending: null });
  const plans = plansFor(journal);
  const { slot, folder } = journal.pending;
  check(equal(sorted(Object.keys(journal.pending)), ['folder', 'operation', 'slot']) &&
    Number.isSafeInteger(slot) && slot === journal.feeds.length && slot < plans.length &&
    folder === journal.folders[plans[slot].folder].name &&
    journal.folders.every(item => item.state === 'created') &&
    journal.feeds.every(feed => feed.state === 'created') && journal.stories.length === 0,
  'Pending add_url does not match an uninterrupted feed-creation plan slot.');
  return { slot, folder, url: plans[slot].url };
}

function recoveredAddCandidate(catalog, journal) {
  const intent = pendingAddIntent(journal);
  check(catalog.feeds && typeof catalog.feeds === 'object' && !Array.isArray(catalog.feeds),
    'Invalid recovery subscription catalog.');
  const keys = Object.keys(catalog.feeds);
  check(keys.every(key => Number.isSafeInteger(Number(key)) && Number(key) > 0 &&
    String(Number(key)) === key), 'Invalid recovery subscription IDs.');
  const ids = keys.map(Number).sort((a, b) => a - b);
  const expected = [...ORIGINAL_IDS, ...journal.feeds.map(feed => feed.id)];
  const unexpected = ids.filter(id => !expected.includes(id));
  check(unexpected.length === 1 && expected.every(id => ids.includes(id)),
    'Recovery requires exactly one unexpected subscription and no missing subscriptions.');
  const [id] = unexpected;
  check(catalog.feeds[id]?.active === true && catalog.feeds[id]?.feed_address === intent.url &&
    ids.filter(feedId => catalog.feeds[feedId]?.feed_address === intent.url).length === 1,
  'Recovery candidate must be active and uniquely match the exact planned canonical URL; redirects are not inferred.');
  const feed = { id, folder: intent.folder, state: 'created' };
  const projected = { ...journal, feeds: [...journal.feeds, feed], pending: null };
  validateJournal(projected);
  // Includes exact fixture folder membership and exactly-once placement across the entire tree.
  validateCatalog(catalog, projected);
  check(projected.feeds.every(item => catalog.feeds[item.id]?.active === true),
    'An owned fixture feed is no longer active; recovery refused.');
  check(ids.every(feedId => typeof catalog.feeds[feedId]?.feed_address === 'string' &&
    typeof catalog.feeds[feedId]?.feed_title === 'string' &&
    typeof catalog.feeds[feedId]?.active === 'boolean'), 'Invalid recovery feed properties.');
  const snapshot = {
    feeds: ids.map(feedId => ({
      id: feedId, active: catalog.feeds[feedId].active,
      address: catalog.feeds[feedId].feed_address, title: catalog.feeds[feedId].feed_title
    })),
    folders: catalog.folders
  };
  return { feed, snapshot };
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

async function run(action, flags = []) {
  check(['prepare', 'status', 'cleanup', 'probe', 'probe-coverage', 'recover-add'].includes(action) &&
    Array.isArray(flags) && flags.length <= 1 && flags.every(flag => flag === '--large'),
    'Usage: node tools\\newsblur-library-fixture.cjs prepare|status|cleanup|probe|probe-coverage|recover-add [--large]');
  const requestedPlan = flags.includes('--large') ? 'large' : null;
  const remoteReadOnly = ['probe', 'probe-coverage', 'recover-add'].includes(action);
  check(process.env.NEWSBLUR_TEST_USERNAME && process.env.NEWSBLUR_TEST_PASSWORD &&
    process.env.LOCALAPPDATA, 'Required automation environment is missing.');
  const directory = path.join(process.env.LOCALAPPDATA, 'Spectro', 'E2E');
  const journalPath = path.join(directory, 'library-fixture.json');
  const throttlePath = path.join(directory, 'library-fixture-throttle.json');
  const lockPath = path.join(directory, 'library-fixture.lock');
  fs.mkdirSync(directory, { recursive: true });
  stage = 'exclusive-fixture-lock';
  const lock = fs.openSync(lockPath, 'wx');
  let context;
  try {
    let journal = fs.existsSync(journalPath) ? JSON.parse(fs.readFileSync(journalPath, 'utf8')) : null;
    const hadThrottle = fs.existsSync(throttlePath);
    const throttle = hadThrottle ? JSON.parse(fs.readFileSync(throttlePath, 'utf8')) :
      { version: 1, cooldown: null, lastRequestAt: null, nextRequestAt: null, nextSavedBodyAt: null };
    validateThrottle(throttle);
    const saveThrottle = () => {
      validateThrottle(throttle);
      writeJsonAtomic(throttlePath, throttle);
    };
    function assertNoCooldown() {
      stage = 'fixture-cooldown';
      check(!throttle.cooldown || Date.now() >= Date.parse(throttle.cooldown.until),
        `Fixture cooldown active until ${throttle.cooldown?.until}; no HTTP requests permitted.`);
    }
    if (!hadThrottle && journal?.state === 'cleaning' && journal.pending === null) {
      validateJournal(journal);
      const lastWrite = fs.statSync(journalPath).mtimeMs;
      check(Number.isFinite(lastWrite), 'Invalid legacy cleanup timestamp; no HTTP requests permitted.');
      throttle.cooldown = { reason: 'legacy-cleanup', observedAt: new Date(lastWrite).toISOString(),
        until: new Date(lastWrite + COOLDOWN_MS).toISOString() };
      saveThrottle();
    }
    assertNoCooldown();
    check(journal || action !== 'recover-add',
      'Explicit recovery requires an existing ownership journal.');
    if (journal) {
      if (action === 'recover-add') {
        pendingAddIntent(journal);
      } else if (action === 'probe') {
        validateJournal({ ...journal, pending: null });
        check(journal.pending === null || (journal.pending && typeof journal.pending === 'object' &&
          !Array.isArray(journal.pending)), 'Invalid pending journal metadata.');
      } else if (action === 'prepare' && journal.state === 'preparing' && journal.pending?.operation === 'read_story') {
        validateJournal({ ...journal, pending: null });
        check(journal.stories.some(s => s.hash === journal.pending.hash && s.target.read),
          'Pending read does not belong to a verified fixture story.');
      } else {
        validateJournal(journal);
      }
      check(!requestedPlan || (journal.plan || 'standard') === requestedPlan,
        'Requested size conflicts with the ownership journal; no resources will be changed.');
    }
    const plans = journal ? plansFor(journal) : requestedPlan === 'large' ? LARGE_PLANS : PLANS;
    const save = () => {
      check(!['probe', 'probe-coverage'].includes(action), 'Read-only probe cannot write the journal.');
      writeJsonAtomic(journalPath, journal);
    };
    stage = 'browser-launch';
    const moduleRoot = process.env.SPECTRO_PLAYWRIGHT_ROOT ||
      path.join(os.homedir(), '.spiderloop', 'playwright-mcp');
    const { chromium } = createRequire(path.join(moduleRoot, 'package.json'))('playwright');
    context = await chromium.launchPersistentContext(path.join(directory, 'browser-profile'), {
      headless: true, ...(remoteReadOnly ? { serviceWorkers: 'block' } : {})
    });
    const page = await context.newPage();
    // Browser UI must not mark a restored story read; only explicit API calls below may mutate.
    await context.route('**/*', route => {
      if (throttle.cooldown && Date.now() < Date.parse(throttle.cooldown.until)) return route.abort();
      const request = route.request();
      const url = new URL(request.url());
      const login = !remoteReadOnly && url.origin === ORIGIN &&
        url.pathname === '/reader/login' && request.method() === 'POST';
      return ['GET', 'HEAD'].includes(request.method()) || login ? route.continue() : route.abort();
    });
    stage = 'authentication';
    await page.goto(`${ORIGIN}/api`, { waitUntil: 'domcontentloaded' });
    if (!await page.evaluate(() => window.NEWSBLUR?.Globals?.is_authenticated === true)) {
      check(!remoteReadOnly, 'Read-only remote access requires an already authenticated dedicated browser profile.');
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
    const clusterSafe = !remoteReadOnly &&
      await page.evaluate(() => window.NEWSBLUR.Preferences.cluster_mark_read === false);
    async function api(endpoint, form, params) {
      assertNoCooldown();
      check(/^(?:[a-z_]+|feed\/[1-9]\d*)$/.test(endpoint), 'Invalid fixture API endpoint.');
      stage = endpoint;
      check(action !== 'probe' || (!form &&
        ['feeds', 'unread_story_hashes', 'starred_story_hashes'].includes(endpoint)),
      'Read-only probe only permits the catalog and hash GET endpoints.');
      check(action !== 'recover-add' || (!form && endpoint === 'feeds'),
        'Add recovery only permits catalog GET requests.');
      check(action !== 'probe-coverage' || (!form && ['feeds', 'unread_story_hashes'].includes(endpoint)),
        'Coverage probe only permits catalog and unread hash GET requests.');
      const requestNotBefore = Math.max(
        throttle.nextRequestAt ? Date.parse(throttle.nextRequestAt) : 0,
        endpoint === 'starred_stories' && throttle.nextSavedBodyAt ? Date.parse(throttle.nextSavedBodyAt) : 0);
      if (requestNotBefore) {
        const remaining = requestNotBefore - Date.now();
        check(remaining <= 60000, 'Persistent API pacing is too far in the future; inspect the system clock.');
        if (remaining > 0) await page.waitForTimeout(remaining);
      }
      assertNoCooldown();
      stage = endpoint;
      const now = Date.now();
      check(now >= requestNotBefore,
        'API pacing deadline has not elapsed; no HTTP request sent.');
      throttle.lastRequestAt = new Date(now).toISOString();
      throttle.nextRequestAt = new Date(now + REQUEST_INTERVAL_MS).toISOString();
      if (endpoint === 'starred_stories') throttle.nextSavedBodyAt = new Date(now + SAVED_BODY_INTERVAL_MS).toISOString();
      saveThrottle();
      const response = form
        ? await context.request.post(`${ORIGIN}/reader/${endpoint}`, { form, timeout: 90000, maxRedirects: 0 })
        : await context.request.get(`${ORIGIN}/reader/${endpoint}`, {
          params, timeout: 90000, maxRedirects: 0,
          ...(action === 'recover-add' ? { headers: { 'Cache-Control': 'no-cache', Pragma: 'no-cache' } } : {})
        });
      if (response.status() === 429) {
        throttle.cooldown = cooldownRecord('http-429', retryAfterInfo(response.headers()['retry-after']));
        saveThrottle();
        throw new SafetyError(`${httpFailure(response, endpoint)} Cooldown until ${throttle.cooldown.until}; no HTTP requests permitted before then.`);
      }
      if (response.status() !== 200) throw new SafetyError(httpFailure(response, endpoint));
      const data = await response.json();
      check(data && data.authenticated === true, 'NewsBlur API did not confirm authentication.');
      if (form) check(typeof data.code === 'number' && data.code >= 0,
        'NewsBlur rejected a fixture mutation; response suppressed, pending journal retained.');
      return data;
    }
    const catalog = () => api('feeds', null, { include_favicons: false, update_counts: false });
    if (action === 'probe-coverage') {
      stage = 'probe-coverage-catalog';
      const current = await catalog();
      validateCatalog(current, journal);
      const coverage = [];
      const feedIds = [...ORIGINAL_IDS, ...(journal?.feeds.filter(feed => feed.state === 'created').map(feed => feed.id) || [])];
      async function window(feedId, order) {
        const data = await api('unread_story_hashes', null, {
          feed_id: feedId, read_filter: 'unread', order, include_timestamps: true
        });
        const groups = data.unread_feed_story_hashes;
        check(groups && typeof groups === 'object' && !Array.isArray(groups) &&
          Object.keys(groups).every(key => key === String(feedId)) &&
          Object.values(groups).every(Array.isArray), 'Invalid feed-scoped coverage response.');
        const entries = Object.values(groups).flat();
        check(entries.length <= 500 && entries.every(entry =>
          Array.isArray(entry) && entry.length === 2 && typeof entry[0] === 'string' &&
          /^\d+:[a-z0-9]+$/i.test(entry[0]) && hashFeed(entry[0]) === feedId &&
          (typeof entry[1] === 'number' || typeof entry[1] === 'string') &&
          String(entry[1]).trim() !== '' && Number.isFinite(Number(entry[1]))),
        'Invalid timestamped coverage entries.');
        check(new Set(entries.map(entry => entry[0])).size === entries.length,
          'Duplicate hashes in coverage window.');
        return entries;
      }
      for (const feedId of feedIds) {
        stage = `probe-coverage-${feedId}`;
        const newest = await window(feedId, 'newest');
        const oldest = newest.length === 500 ? await window(feedId, 'oldest') : null;
        const repeat = oldest ? await window(feedId, 'newest') : null;
        const newestSet = new Set(newest.map(entry => entry[0]));
        const overlap = oldest ? oldest.filter(entry => newestSet.has(entry[0])).length : null;
        const entry = {
          feedId, scope: ORIGINAL_IDS.includes(feedId) ? 'baseline' :
            journal.feeds.slice(0, PLANS.length).some(feed => feed.id === feedId) ? 'story-matrix' : 'display-only',
          newestCount: newest.length, oldestCount: oldest?.length ?? null, overlap,
          newestStable: repeat ? equal(newest, repeat) : null,
          boundaryTimestampTied: oldest && newest.length ? Number(oldest[oldest.length - 1]?.[1]) === Number(newest[newest.length - 1][1]) : null,
          completeByExistingRule: newest.length < 500 || overlap > 0
        };
        coverage.push(entry);
        console.log(JSON.stringify({ action, phase: 'feed-coverage', ...entry }));
      }
      await verifyIdentity();
      return { action, identityVerified: true, journalChanged: false, remoteMutations: 0,
        incompleteFeedIDs: coverage.filter(entry => !entry.completeByExistingRule).map(entry => entry.feedId),
        baselineCompleteByExistingRule: coverage.filter(entry => entry.scope === 'baseline').every(entry => entry.completeByExistingRule) };
    }
    if (action === 'recover-add') {
      stage = 'verify-pending-add';
      const intent = pendingAddIntent(journal);
      const first = recoveredAddCandidate(await catalog(), journal);
      await verifyIdentity();
      const verifiedAt = [new Date().toISOString()];
      await page.waitForTimeout(1500);
      await verifyIdentity();
      const second = recoveredAddCandidate(await catalog(), journal);
      verifiedAt.push(new Date().toISOString());
      check(equal(first.snapshot, second.snapshot),
        'Recovery catalogs changed between observations; pending journal retained.');
      await verifyIdentity();
      stage = 'record-recovered-add';
      check(equal(JSON.parse(fs.readFileSync(journalPath, 'utf8')), journal),
        'Ownership journal changed during recovery; nothing will be recorded.');
      const receipt = {
        operation: 'add_url', plan: journal.plan || 'standard', slot: intent.slot,
        feedId: second.feed.id, folder: intent.folder, url: intent.url, verifiedAt,
        catalogSha256: createHash('sha256').update(JSON.stringify(second.snapshot)).digest('hex')
      };
      const recovered = {
        ...journal, feeds: [...journal.feeds, second.feed],
        recoveredAdds: [...(journal.recoveredAdds || []), receipt], pending: null
      };
      validateJournal(recovered);
      journal = recovered;
      save();
      check(equal(JSON.parse(fs.readFileSync(journalPath, 'utf8')), journal),
        'Recovery journal persistence verification failed; inspect the journal before proceeding.');
      return { action, recovered: true, ready: false, identityVerified: true,
        remoteMutations: 0, state: journal.state, plan: journal.plan || 'standard',
        ownedFeedCount: journal.feeds.length, pending: null, receipt };
    }
    if (action === 'probe') {
      stage = 'probe-catalog';
      let pending = null;
      if (journal?.pending) {
        pending = { operation: 'unclassified', recovery: 'manual-review-only' };
        if (journal.pending.operation === 'add_url') {
          const slot = journal.pending.slot;
          check(journal.state === 'preparing' && Number.isSafeInteger(slot) &&
            slot === journal.feeds.length && slot < plans.length &&
            journal.pending.folder === journal.folders[plans[slot].folder].name,
          'Pending add_url does not match the next recorded plan slot.');
          pending = { operation: 'add_url', slot, ordinal: slot + 1,
            plannedUrl: plans[slot].url, recovery: 'manual-review-only' };
        }
      }
      const current = await catalog();
      check(current.feeds && typeof current.feeds === 'object' && !Array.isArray(current.feeds),
        'Invalid probe subscription catalog.');
      const actual = Object.keys(current.feeds).map(Number);
      check(actual.every(id => Number.isSafeInteger(id) && id > 0), 'Invalid probe subscription IDs.');
      const expected = [...ORIGINAL_IDS, ...(journal?.feeds.filter(f => f.state === 'created').map(f => f.id) || [])];
      if (pending?.operation === 'add_url') {
        pending.exactAddressMatchIDs = actual.filter(id =>
          current.feeds[id]?.feed_address === plans[pending.slot].url);
      }
      let catalogMatchesJournal = false;
      let catalogMismatchReason = null;
      try {
        validateCatalog(current, journal);
        catalogMatchesJournal = true;
      } catch (error) {
        if (!(error instanceof SafetyError)) throw error;
        catalogMismatchReason = error.message;
      }
      const inventory = {
        catalogMatchesJournal, catalogMismatchReason,
        subscriptionCount: actual.length,
        activeSubscriptionCount: Object.values(current.feeds).filter(feed => feed.active === true).length,
        unexpectedSubscriptionIDs: actual.filter(id => !expected.includes(id)),
        missingSubscriptionIDs: expected.filter(id => !actual.includes(id)),
        baselineActiveStateMatches: current.feeds[5771943]?.active === true &&
          current.feeds[10310620]?.active === false
      };
      // Emit inventory first so a subsequent protocol HTTP failure cannot hide recovery evidence.
      console.log(JSON.stringify({ action: 'probe', phase: 'inventory', identityVerified: true,
        plan: journal?.plan || requestedPlan || 'standard', state: journal?.state || 'absent',
        ownedFeedCount: journal?.feeds.length || 0, pending, inventory,
        journalChanged: false, recoveryAuthorized: false }));
      const protocol = {};
      for (const [endpoint, params, field] of [
        ['unread_story_hashes', { read_filter: 'all', include_timestamps: true }, 'unread_feed_story_hashes'],
        ['starred_story_hashes', { include_timestamps: true }, 'starred_story_hashes']
      ]) {
        stage = `probe-${endpoint}`;
        const data = await api(endpoint, null, params);
        protocol[endpoint] = {
          responseShape: shapeOf(data),
          expectedFieldPresent: Object.hasOwn(data, field),
          expectedFieldShape: shapeOf(data[field])
        };
      }
      await verifyIdentity();
      return { action: 'probe', identityVerified: true, journalChanged: false,
        recoveryAuthorized: false, protocol };
    }
    async function hashes(feedIds = verificationFeedIds(journal)) {
      const unreadValues = [];
      // The unfiltered endpoint skips zero-count feeds when another feed has unread stories.
      // Query each owned feed separately so new subscriptions take the server's all-stories fallback.
      const allowedIds = verificationFeedIds(journal);
      check(Array.isArray(feedIds) && new Set(feedIds).size === feedIds.length &&
        feedIds.every(id => allowedIds.includes(id)), 'Unread verification scope is not baseline or matrix owned.');
      for (const feedId of feedIds) {
        const unread = await api('unread_story_hashes', null,
          { feed_id: feedId, read_filter: 'unread', order: 'newest' });
        let values = unreadWindowValues(unread, feedId);
        if (values.length === 500) {
          // This endpoint caps results at 500 with no offset. Opposite windows must overlap
          // to prove complete coverage; otherwise absence cannot establish a read state.
          const oldest = await api('unread_story_hashes', null,
            { feed_id: feedId, read_filter: 'unread', order: 'oldest' });
          const older = unreadWindowValues(oldest, feedId);
          const newest = new Set(values);
          check(older.some(h => newest.has(h)),
            `Unread windows do not overlap for feed ${feedId}; complete state cannot be verified safely.`);
          values = [...new Set([...values, ...older])];
        }
        unreadValues.push(...values);
      }
      const saved = await api('starred_story_hashes');
      check(Array.isArray(saved.starred_story_hashes), 'Invalid canonical saved response.');
      check([...unreadValues, ...saved.starred_story_hashes].every(h => typeof h === 'string' &&
        /^\d+:[a-z0-9]+$/i.test(h)), 'Unexpected story hash format.');
      return { unread: new Set(unreadValues), saved: new Set(saved.starred_story_hashes),
        unreadFeedIds: new Set(feedIds) };
    }
    stage = 'baseline-safety-gate';
    const initialCatalog = await catalog();
    validateCatalog(initialCatalog, journal);
    const originalCatalog = baselineCatalog(initialCatalog, journal);
    const initialHashes = await hashes(ORIGINAL_IDS);
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
      check(equal(originalHashes(await hashes(ORIGINAL_IDS)), originalStoryState),
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
          version: 1, plan: requestedPlan || 'standard',
          state: 'preparing', originalSubscriptionIDs: [...ORIGINAL_IDS],
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
        for (const plan of plans.slice(journal.feeds.length)) {
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
        for (const [index, feed] of journal.feeds.slice(0, PLANS.length).entries()) {
          stage = 'select-fresh-fixture-story';
          const plan = plans[index];
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
      const completeUnread = state.unreadFeedIds.has(feed.id);
      if (!completeUnread) check(sample.every(story => story.read_status === 0 || story.read_status === 1),
        'Display-only feed page has an invalid read status; unread inventory was not enumerated.');
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
        unreadCount: completeUnread ? [...state.unread].filter(h => hashFeed(h) === feed.id).length : null,
        ...(!completeUnread ? { unreadCoverage: 'not-enumerated', latestPageReadSource: 'feed-read-status' } : {}),
        savedCount: [...state.saved].filter(h => hashFeed(h) === feed.id).length,
        latestPage: { total: sample.length,
          read: sample.filter(s => completeUnread ? !state.unread.has(s.story_hash) : s.read_status === 1).length,
          unread: sample.filter(s => completeUnread ? state.unread.has(s.story_hash) : s.read_status === 0).length } });
    }
    if (action === 'prepare') {
      check(journal.state === 'ready' && feeds.length === plans.length &&
        feeds.every(feed => current.feeds[feed.id].active === true) && matrix.length === PLANS.length &&
        matrix.every(s => s.matchesTarget), 'Prepared fixture does not satisfy the four-state matrix.');
    }
    await preserveBaseline();
    return { action, ready: journal?.state === 'ready' && feeds.length === plans.length &&
        feeds.every(feed => current.feeds[feed.id].active === true) && matrix.length === PLANS.length &&
        matrix.every(s => s.matchesTarget), state: journal?.state || 'absent',
      plan: journal ? journal.plan || 'standard' : requestedPlan || 'standard',
      plannedFeedCount: plans.length,
      activeSubscriptionCount: Object.values(current.feeds).filter(feed => feed.active === true).length,
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
  run(process.argv[2], process.argv.slice(3)).then(result => console.log(JSON.stringify(result, null, 2))).catch(error => {
    console.error(JSON.stringify({ ready: false, stage,
      reason: error instanceof SafetyError ? error.message :
        'Browser, filesystem, or API operation failed; sensitive diagnostics suppressed. Inspect journal status before retrying.' }));
    process.exitCode = 1;
  });
}
