import '@fontsource/ibm-plex-sans/latin-400.css'
import '@fontsource/ibm-plex-sans/latin-500.css'
import '@fontsource/ibm-plex-sans/latin-600.css'
import '@fontsource/ibm-plex-sans/vietnamese-400.css'
import '@fontsource/ibm-plex-sans/vietnamese-500.css'
import '@fontsource/ibm-plex-sans/vietnamese-600.css'
import '@fontsource/space-grotesk/latin-500.css'
import '@fontsource/space-grotesk/latin-600.css'
import '@fontsource/space-grotesk/vietnamese-500.css'
import '@fontsource/space-grotesk/vietnamese-600.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App'
import { AuthProvider } from './context/AuthContext'
import { bootstrapTheme } from './hooks/useTheme'

bootstrapTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)

if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  let refreshing = false
  const updateIntervalMs = 60 * 60 * 1000

  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (refreshing) return
    refreshing = true
    window.location.reload()
  })

  window.addEventListener('load', () => {
    navigator.serviceWorker
      .register('/sw.js', { scope: '/' })
      .then((registration) => {
        const checkForUpdates = () => {
          registration.update().catch(() => undefined)
        }

        checkForUpdates()
        window.setInterval(checkForUpdates, updateIntervalMs)

        registration.addEventListener('updatefound', () => {
          const worker = registration.installing
          if (!worker) return
          worker.addEventListener('statechange', () => {
            if (worker.state === 'installed' && navigator.serviceWorker.controller) {
              console.info('Phiên bản mới đã sẵn sàng và sẽ được áp dụng khi tải lại trang.')
            }
          })
        })
      })
      .catch(() => undefined)
  })
}
