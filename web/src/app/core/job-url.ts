/** Query parameters that only describe how you reached a page, never which job it is. */
const TRACKING_PARAMS = new Set([
  'gclid',
  'fbclid',
  'msclkid',
  'igshid',
  'mc_cid',
  'mc_eid',
  'trk',
  'trackingid',
  'refid',
  'trkemail',
  'lipi',
  'midtoken',
  'midsig',
  'ebp',
  'gh_src',
  'lever-source',
  'lever-origin',
  '_hsenc',
  '_hsmi',
]);

const isTracking = (name: string): boolean => {
  const key = name.toLowerCase();
  return key.startsWith('utm_') || TRACKING_PARAMS.has(key);
};

/** The job id in a LinkedIn link: search-result pages carry it in `currentJobId`, job pages in the path. */
function linkedInJobId(url: URL): string | null {
  const fromQuery = url.searchParams.get('currentJobId');
  if (fromQuery && /^\d+$/.test(fromQuery)) return fromQuery;
  const fromPath = /^\/(?:comm\/)?jobs\/view\/(?:[^/?#]*-)?(\d+)\/?$/.exec(url.pathname);
  return fromPath ? fromPath[1] : null;
}

/**
 * Turns a pasted job link into one worth saving.
 * LinkedIn links collapse to the job itself (the search-results links are several hundred characters of
 * tracking); other links lose only tracking parameters. Anything that is not an http(s) link is returned
 * trimmed but untouched, so the form can still explain what is wrong with it.
 */
export function tidyJobUrl(raw: string): string {
  const value = raw.trim();
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return value;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return value;

  const host = url.hostname.toLowerCase();
  if (host === 'linkedin.com' || host.endsWith('.linkedin.com')) {
    const id = linkedInJobId(url);
    if (id) return `https://www.linkedin.com/jobs/view/${id}/`;
  }

  const tracking = [...new Set([...url.searchParams.keys()].filter(isTracking))];
  if (tracking.length === 0) return value; // nothing to remove: keep exactly what was pasted
  for (const name of tracking) url.searchParams.delete(name);
  return url.toString();
}
