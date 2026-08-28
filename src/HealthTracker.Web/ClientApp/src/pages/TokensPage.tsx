import { useCallback, useEffect, useState } from 'react'
import { createToken, getTokens, revokeToken } from '../api'
import { ConfirmDialog } from '../components/ConfirmDialog'
import type { PersonalAccessToken } from '../types'

interface TokensPageProps { announce: (message: string, error?: boolean) => void }

export function TokensPage({ announce }: TokensPageProps) {
  const [tokens, setTokens] = useState<PersonalAccessToken[]>([])
  const [name, setName] = useState('')
  const [secret, setSecret] = useState<string | null>(null)
  const [revoking, setRevoking] = useState<PersonalAccessToken | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    const controller = new AbortController()
    setLoading(true)
    getTokens(controller.signal).then(setTokens).catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'Tokens could not be loaded.', true) }).finally(() => setLoading(false))
    return () => controller.abort()
  }, [announce])
  useEffect(() => load(), [load])

  async function create(event: React.FormEvent) {
    event.preventDefault()
    try { const result = await createToken(name); setSecret(result.secret); setName(''); announce('Token created.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Token could not be created.', true) }
  }

  async function revoke() {
    if (!revoking) return
    try { await revokeToken(revoking.id); setRevoking(null); announce('Token revoked.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Token could not be revoked.', true) }
  }

  return <main className="page narrow-page">
    <div className="page-heading"><div><p className="eyebrow">MCP access</p><h1>Access tokens</h1></div></div>
    <form className="create-line" onSubmit={create}><label><span>Token name</span><input value={name} maxLength={100} required onChange={(event) => setName(event.target.value)} /></label><button type="submit" className="primary-action">Create token</button></form>
    {secret && <section className="secret-reveal" aria-live="polite"><div><strong>Copy this now</strong><button type="button" className="text-action" onClick={() => navigator.clipboard.writeText(secret)}>Copy</button></div><code>{secret}</code><button type="button" className="text-action" onClick={() => setSecret(null)}>I’ve saved it</button></section>}
    {loading && tokens.length === 0 ? <div className="loading-state">Loading tokens…</div> : <div className="plain-list">{tokens.map((token) => <div className="plain-row" key={token.id}><div><strong>{token.name}</strong><span><code>{token.prefix}…</code> · expires {new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(token.expiresUtc))}</span><span>Last used {token.lastUsedUtc ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(token.lastUsedUtc)) : 'never'}</span></div>{token.isRevoked ? <span className="muted">Revoked</span> : <button type="button" className="text-action danger-text" onClick={() => setRevoking(token)}>Revoke</button>}</div>)}</div>}
    <ConfirmDialog open={revoking !== null} title="Revoke token?" confirmLabel="Revoke" onCancel={() => setRevoking(null)} onConfirm={revoke}><p>Anything using this token will lose access immediately.</p></ConfirmDialog>
  </main>
}
