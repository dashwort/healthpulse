import { useState } from 'react'
import type { Session, Template } from '../types'

interface AppShellProps {
  path: string
  session: Session
  trackedTemplates: Template[]
  theme: 'light' | 'dark'
  children: React.ReactNode
  onNavigate: (path: string) => void
  onToggleTheme: () => void
  onAddReading: () => void
}

const primaryLinks = [
  { path: '/', label: 'Trend' },
  { path: '/readings', label: 'History' },
  { path: '/templates', label: 'Templates' },
]

export function AppShell({ path, session, trackedTemplates, theme, children, onNavigate, onToggleTheme, onAddReading }: AppShellProps) {
  const [menuOpen, setMenuOpen] = useState(false)

  function link(pathname: string, label: string) {
    return (
      <a href={pathname} aria-current={path === pathname ? 'page' : undefined} onClick={(event) => {
        event.preventDefault()
        setMenuOpen(false)
        onNavigate(pathname)
      }}>{label}</a>
    )
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <a className="wordmark" href="/" aria-label="HealthPulse trends" onClick={(event) => { event.preventDefault(); onNavigate('/') }}>healthpulse</a>
        <nav className="primary-nav" aria-label="Primary navigation">
          {primaryLinks.map((item) => <span key={item.path}>{link(item.path, item.label)}</span>)}
        </nav>
        <div className="header-actions">
          <button type="button" className="theme-toggle" onClick={onToggleTheme} aria-label={`Use ${theme === 'dark' ? 'light' : 'dark'} theme`}>{theme === 'dark' ? '○' : '●'}</button>
          <button type="button" className="add-reading" disabled={trackedTemplates.length === 0} onClick={onAddReading} aria-label="Add reading">+</button>
          <button type="button" className="menu-button" aria-expanded={menuOpen} aria-label={menuOpen ? 'Close navigation menu' : 'Open navigation menu'} onClick={() => setMenuOpen((open) => !open)}>•••</button>
        </div>
      </header>

      {menuOpen && (
        <div className="utility-menu">
          <div className="utility-menu-inner">
            <p>{session.displayName}</p>
            <nav aria-label="Account navigation">
              {link('/tokens', 'Access tokens')}
              {link('/settings', 'App information')}
              {session.isAdministrator && link('/users', 'Users')}
              {session.isAdministrator && link('/logs', 'Diagnostics')}
            </nav>
            <form method="post" action="/logout">
              {session.antiforgeryToken && <input type="hidden" name="__RequestVerificationToken" value={session.antiforgeryToken} />}
              <button type="submit" className="text-action">Sign out</button>
            </form>
          </div>
        </div>
      )}

      {children}
    </div>
  )
}
