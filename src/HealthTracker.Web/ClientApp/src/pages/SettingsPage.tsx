import { useEffect, useState } from 'react'
import { getAppInfo } from '../api'
import type { AppInfo } from '../types'

interface SettingsPageProps { announce: (message: string, error?: boolean) => void }

export function SettingsPage({ announce }: SettingsPageProps) {
  const [info, setInfo] = useState<AppInfo | null>(null)
  useEffect(() => { const controller = new AbortController(); getAppInfo(controller.signal).then(setInfo).catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'App information could not be loaded.', true) }); return () => controller.abort() }, [announce])
  return <main className="page narrow-page">
    <div className="page-heading"><div><p className="eyebrow">HealthPulse</p><h1>App information</h1></div></div>
    {!info ? <div className="loading-state">Loading app information…</div> : <>
      <section className="info-section"><h2>Android app</h2>{info.android.apkUrl ? <><a className="line-button" href={info.android.apkUrl} target="_blank" rel="noreferrer">Download APK</a><p>Version {info.android.latestVersion}</p></> : <p className="muted">Not currently available.</p>}</section>
      <section className="info-section"><h2>Deployment</h2><dl className="facts"><div><dt>Version</dt><dd>{info.deployment.version}</dd></div><div><dt>Build</dt><dd>{info.deployment.build}</dd></div><div className="wide-fact"><dt>Commit</dt><dd><code>{info.deployment.commit}</code></dd></div><div className="wide-fact"><dt>Built</dt><dd>{Number.isNaN(Date.parse(info.deployment.builtAtUtc)) ? info.deployment.builtAtUtc : new Intl.DateTimeFormat(undefined, { dateStyle: 'long', timeStyle: 'short' }).format(new Date(info.deployment.builtAtUtc))}</dd></div></dl></section>
    </>}
  </main>
}
