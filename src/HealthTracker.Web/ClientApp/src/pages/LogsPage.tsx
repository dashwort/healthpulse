import { useCallback, useEffect, useState } from 'react'
import { getAccessActivity, getLogSnapshot, getUsers } from '../api'
import type { AccessActivityPage, AllowedUser, LogSnapshot } from '../types'

interface LogsPageProps { announce: (message: string, error?: boolean) => void }

export function LogsPage({ announce }: LogsPageProps) {
  const [snapshot, setSnapshot] = useState<LogSnapshot | null>(null)
  const [activity, setActivity] = useState<AccessActivityPage | null>(null)
  const [users, setUsers] = useState<AllowedUser[]>([])
  const [userId, setUserId] = useState('')
  const [type, setType] = useState('')
  const [outcome, setOutcome] = useState('')
  const [page, setPage] = useState(1)
  const [refresh, setRefresh] = useState(0)

  const load = useCallback(() => {
    const controller = new AbortController()
    Promise.all([
      getLogSnapshot(controller.signal), getUsers(true, controller.signal),
      getAccessActivity({ userId: userId || undefined, type: type || undefined, outcome: outcome || undefined, page }, controller.signal),
    ]).then(([logs, loadedUsers, loadedActivity]) => { setSnapshot(logs); setUsers(loadedUsers); setActivity(loadedActivity) })
      .catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'Diagnostics could not be loaded.', true) })
    return () => controller.abort()
  }, [announce, outcome, page, refresh, type, userId])
  useEffect(() => load(), [load])

  function eventName(value: string) { return value === 'WebSignIn' ? 'Web sign-in' : value === 'AndroidAuthorization' ? 'Android authorization' : value }
  return <main className="page wide-page">
    <div className="page-heading"><div><p className="eyebrow">Administration</p><h1>Application logs</h1></div><div className="heading-actions"><a className="line-button" href="/admin/logs/raw" target="_blank" rel="noreferrer">Open text view</a><a className="line-button" href="/admin/logs/download">Download .txt</a><button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Refresh</button></div></div>
    <section className="diagnostic-section"><h2>Access activity</h2><div className="filter-line"><label><span>User</span><select value={userId} onChange={(event) => { setUserId(event.target.value); setPage(1) }}><option value="">All users</option>{users.map((user) => <option value={user.id} key={user.id}>{user.email}</option>)}</select></label><label><span>Event</span><select value={type} onChange={(event) => { setType(event.target.value); setPage(1) }}><option value="">All events</option><option value="WebSignIn">Web sign-in</option><option value="AndroidAuthorization">Android authorization</option></select></label><label><span>Result</span><select value={outcome} onChange={(event) => { setOutcome(event.target.value); setPage(1) }}><option value="">All results</option><option value="Success">Success</option><option value="Failure">Failure</option></select></label></div>
      {!activity ? <div className="loading-state">Loading activity…</div> : activity.items.length === 0 ? <p className="quiet-empty">No access activity matches these filters.</p> : <div className="data-list activity-list"><div className="data-row data-header"><span>When</span><span>User</span><span>Event</span><span>Result</span><span>Source</span></div>{activity.items.map((item) => <div className="data-row" key={item.id}><time>{new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(item.occurredUtc))}</time><span>{item.userEmail ?? 'Unattributed'}</span><span>{eventName(item.type)}</span><span>{item.failureReason ? `${item.outcome} (${item.failureReason})` : item.outcome}</span><span title={item.userAgent ?? ''}>{item.sourceIpAddress ?? '—'}</span></div>)}</div>}
      {activity && <div className="pagination"><span>{activity.totalCount} events</span><div><button type="button" className="text-action" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>Page {page}</span><button type="button" className="text-action" disabled={page * 50 >= activity.totalCount} onClick={() => setPage((value) => value + 1)}>Next</button></div></div>}
    </section>
    <section className="diagnostic-section"><div className="section-heading"><h2>Server log</h2>{snapshot && <span>{snapshot.fileCount} files · {new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(snapshot.generatedAtUtc))}</span>}</div>{snapshot ? <pre className="log-viewer">{snapshot.content}</pre> : <div className="loading-state">Loading log…</div>}</section>
  </main>
}
