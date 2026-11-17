// sw.js - Service worker: SoftCalc works offline and starts quicker the second time
//
// Two kinds of files:
// - Fixed: Pyodide (in a folder named after its version) and the build's
//   hashed assets never change under the same name, so they come from the
//   cache first.
// - Fresh: the page, worker.js, engine.zip and the manifest keep their
//   names across versions, so they come from the network first, and from
//   the cache only when the network fails (offline).
// The page posts the files it already loaded ("cache"), so they are kept
// from the first visit on, before this worker was in charge of them.

const CACHE = "softcalc";
const FRESH = /(\/|\.html|worker\.js|engine\.zip|\.webmanifest)$/;

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", event => event.waitUntil(self.clients.claim()));

self.addEventListener("message", event =>
{
	if (event.data?.type === "cache")
	{
		event.waitUntil(caches.open(CACHE).then(cache => Promise.all(event.data.urls.map(url =>
			cache.match(url, { ignoreVary: true }).then(found => found ?? cache.add(url).catch(() => undefined))))));
	}
});

async function fresh(request)
{
	const cache = await caches.open(CACHE);
	try
	{
		const answer = await fetch(request);
		if (answer.ok)
		{
			cache.put(request, answer.clone());
		}
		return answer;
	}
	catch (error)
	{
		const kept = await cache.match(request, { ignoreSearch: true, ignoreVary: true });
		if (kept)
		{
			return kept;
		}
		throw error;
	}
}

async function fixed(request)
{
	const cache = await caches.open(CACHE);
	// ignoreVary: the server says "Vary: Origin", and a module script asks with an Origin header
	// while the copy kept from the first visit was asked for without one
	const kept = await cache.match(request, { ignoreVary: true });
	if (kept)
	{
		return kept;
	}
	const answer = await fetch(request);
	if (answer.ok)
	{
		cache.put(request, answer.clone());
	}
	return answer;
}

self.addEventListener("fetch", event =>
{
	const request = event.request;
	const url = new URL(request.url);
	if (request.method !== "GET" || url.origin !== location.origin)
	{
		return;
	}
	event.respondWith(request.mode === "navigate" || FRESH.test(url.pathname) ? fresh(request) : fixed(request));
});
