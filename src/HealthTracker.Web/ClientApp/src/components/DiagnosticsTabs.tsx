interface DiagnosticsTabsProps {
  active: 'traffic' | 'security' | 'logs'
  onNavigate: (path: string) => void
}

const tabs = [
  { key: 'traffic', href: '/traffic', label: 'Traffic' },
  { key: 'security', href: '/security', label: 'Security' },
  { key: 'logs', href: '/logs', label: 'Logs' },
] as const

export function DiagnosticsTabs({ active, onNavigate }: DiagnosticsTabsProps) {
  return <nav className="diagnostics-tabs" aria-label="Diagnostics pages">
    {tabs.map((tab) => <a
      key={tab.key}
      href={tab.href}
      aria-current={active === tab.key ? 'page' : undefined}
      onClick={(event) => {
        event.preventDefault()
        onNavigate(tab.href)
      }}
    >{tab.label}</a>)}
  </nav>
}
