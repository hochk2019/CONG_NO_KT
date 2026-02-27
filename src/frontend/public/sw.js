const VERSION = 'v2'
const APP_SHELL_CACHE = `app-shell-${VERSION}`
const API_CACHE = `api-get-${VERSION}`
const APP_SHELL_ASSETS = ['/', '/index.html', '/offline.html', '/manifest.json', '/favicon.ico', '/pwa-192.png', '/pwa-512.png']

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(APP_SHELL_CACHE)
      .then((cache) => cache.addAll(APP_SHELL_ASSETS))
      .finally(() => self.skipWaiting()),
  )
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((key) => key !== APP_SHELL_CACHE && key !== API_CACHE)
            .map((key) => caches.delete(key)),
        ),
      )
      .finally(() => self.clients.claim()),
  )
})

const isApiRequest = (url) => url.pathname.startsWith('/api/')
const isSameOrigin = (url) => url.origin === self.location.origin

const networkFirst = async (request, cacheName) => {
  const cache = await caches.open(cacheName)
  try {
    const response = await fetch(request)
    if (response && response.ok) {
      cache.put(request, response.clone())
    }
    return response
  } catch {
    const cached = await cache.match(request)
    if (cached) {
      return cached
    }
    throw new Error('network_unavailable')
  }
}

const handleNavigation = async (request) => {
  try {
    return await networkFirst(request, APP_SHELL_CACHE)
  } catch {
    const cache = await caches.open(APP_SHELL_CACHE)
    return (await cache.match('/offline.html')) || Response.error()
  }
}

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return

  const url = new URL(event.request.url)
  if (!isSameOrigin(url)) return

  if (event.request.mode === 'navigate') {
    event.respondWith(handleNavigation(event.request))
    return
  }

  if (isApiRequest(url)) {
    event.respondWith(
      networkFirst(event.request, API_CACHE).catch(
        () =>
          new Response(JSON.stringify({ error: 'offline', message: 'Không có kết nối mạng.' }), {
            status: 503,
            headers: { 'Content-Type': 'application/json' },
          }),
      ),
    )
    return
  }

  event.respondWith(
    caches.match(event.request).then(
      (cached) =>
        cached ||
        fetch(event.request).then((response) => {
          if (response && response.ok) {
            caches.open(APP_SHELL_CACHE).then((cache) => cache.put(event.request, response.clone()))
          }
          return response
        }),
    ),
  )
})
