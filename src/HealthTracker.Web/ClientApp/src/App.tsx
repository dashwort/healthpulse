import { useCallback, useEffect, useState } from 'react'
import { getSession, getTrackedTemplates, setAntiforgeryToken } from './api'
import { AppShell } from './components/AppShell'
import { QuickAdd } from './components/QuickAdd'
import { HistoryPage } from './pages/HistoryPage'
import { LogsPage } from './pages/LogsPage'
import { SettingsPage } from './pages/SettingsPage'
import { TemplatesPage } from './pages/TemplatesPage'
import { TokensPage } from './pages/TokensPage'
import { TrendPage } from './pages/TrendPage'
import { UsersPage } from './pages/UsersPage'
import type { Reading, Session, Template } from './types'

const themeKey = 'healthtracker.theme'
type Theme = 'light' | 'dark'

function initialTheme(): Theme {
  const saved = window.localStorage.getItem(themeKey)
  if (saved === 'light' || saved === 'dark') return saved
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function normalizePath(path: string) {
  if (path === '/app' || path === '/app/') return '/'
  return path.length > 1 ? path.replace(/\/$/, '') : path
}

function App() {
  const [path, setPath] = useState(() => normalizePath(window.location.pathname))
  const [session, setSession] = useState<Session | null>(null)
  const [trackedTemplates, setTrackedTemplates] = useState<Template[]>([])
  const [theme, setTheme] = useState<Theme>(initialTheme)
  const [quickAddOpen, setQuickAddOpen] = useState(false)
  const [refreshVersion, setRefreshVersion] = useState(0)
  const [notice, setNotice] = useState<{ message: string; error: boolean } | null>(null)
  const [startupError, setStartupError] = useState<string | null>(null)

  const announce = useCallback((message: string, error = false) => {
    setNotice({ message, error })
    window.setTimeout(() => setNotice((current) => current?.message === message ? null : current), 4200)
  }, [])

  const loadTracked = useCallback(() => {
    const controller = new AbortController()
    getTrackedTemplates(controller.signal).then(setTrackedTemplates).catch((error: unknown) => {
      if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'Tracked measurements could not be loaded.', true)
    })
    return () => controller.abort()
  }, [announce])

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    window.localStorage.setItem(themeKey, theme)
  }, [theme])

  useEffect(() => {
    const titles: Record<string, string> = {
      '/': 'Trends', '/readings': 'History', '/templates': 'Templates', '/tokens': 'Access tokens',
      '/settings': 'App information', '/users': 'Users', '/logs': 'Application logs',
    }
    document.title = `${titles[path] ?? 'HealthPulse'} · HealthPulse`
  }, [path])

  useEffect(() => {
    const listener = () => setPath(normalizePath(window.location.pathname))
    window.addEventListener('popstate', listener)
    return () => window.removeEventListener('popstate', listener)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    getSession(controller.signal).then((result) => {
      setSession(result)
      setAntiforgeryToken(result.antiforgeryToken)
      if (result.isAuthenticated) loadTracked()
    }).catch((error: unknown) => {
      if (!(error instanceof DOMException && error.name === 'AbortError')) setStartupError(error instanceof Error ? error.message : 'HealthPulse could not start.')
    })
    return () => controller.abort()
  }, [loadTracked])

  function navigate(nextPath: string) {
    window.history.pushState({}, '', nextPath)
    setPath(normalizePath(nextPath))
    window.scrollTo({ top: 0, behavior: 'auto' })
  }

  function changed() {
    setRefreshVersion((value) => value + 1)
    loadTracked()
  }

  function saved(reading: Reading) {
    setQuickAddOpen(false)
    announce(`${reading.templateName} saved.`)
    changed()
  }

  if (path === '/signed-out') return <PublicPage title="Signed out"><a className="line-button" href="/login">Sign in again</a></PublicPage>
  if (path === '/error' || path === '/Error') return <PublicPage title="Something went wrong"><a className="text-action" href="/">Return to HealthPulse</a></PublicPage>
  if (startupError) return <PublicPage title="HealthPulse is unavailable"><p className="page-error">{startupError}</p></PublicPage>
  if (!session) return <div className="boot-state">healthpulse</div>
  if (!session.isAuthenticated) return <PublicPage title="HealthPulse"><a className="line-button" href="/login">Sign in</a></PublicPage>

  let page: React.ReactNode
  if (path === '/') page = <TrendPage templates={trackedTemplates} refreshVersion={refreshVersion} onNavigate={navigate} />
  else if (path === '/readings') page = <HistoryPage trackedTemplates={trackedTemplates} onChanged={changed} announce={announce} />
  else if (path === '/templates') page = <TemplatesPage onChanged={changed} announce={announce} />
  else if (path === '/tokens') page = <TokensPage announce={announce} />
  else if (path === '/settings') page = <SettingsPage announce={announce} />
  else if (path === '/users' && session.isAdministrator) page = <UsersPage announce={announce} />
  else if (path === '/logs' && session.isAdministrator) page = <LogsPage announce={announce} />
  else page = <main className="page"><section className="empty-state"><h1>Page not found</h1><button type="button" className="text-action" onClick={() => navigate('/')}>Return to trends</button></section></main>

  return <AppShell path={path} session={session} trackedTemplates={trackedTemplates} theme={theme} onNavigate={navigate} onToggleTheme={() => setTheme((value) => value === 'dark' ? 'light' : 'dark')} onAddReading={() => setQuickAddOpen(true)}>
    {page}
    {notice && <div className={`toast${notice.error ? ' toast-error' : ''}`} role={notice.error ? 'alert' : 'status'}>{notice.message}</div>}
    <QuickAdd isOpen={quickAddOpen} templates={trackedTemplates} selectedTemplateId={trackedTemplates[0]?.id ?? ''} onClose={() => setQuickAddOpen(false)} onSaved={saved} />
  </AppShell>
}

function PublicPage({ title, children }: { title: string; children: React.ReactNode }) {
  return <main className="public-page"><a className="wordmark" href="/">healthpulse</a><h1>{title}</h1>{children}</main>
}

export default App
