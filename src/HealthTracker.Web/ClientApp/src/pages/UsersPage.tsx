import { useCallback, useEffect, useState } from 'react'
import { addUser, archiveUser, getUsers, getUserTokens, revokeToken, updateUserRole } from '../api'
import { ConfirmDialog } from '../components/ConfirmDialog'
import type { AllowedUser, PersonalAccessToken } from '../types'

interface UsersPageProps { announce: (message: string, error?: boolean) => void }

export function UsersPage({ announce }: UsersPageProps) {
  const [users, setUsers] = useState<AllowedUser[]>([])
  const [tokens, setTokens] = useState<Record<string, PersonalAccessToken[]>>({})
  const [showArchived, setShowArchived] = useState(false)
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<'Member' | 'Admin'>('Member')
  const [archiving, setArchiving] = useState<AllowedUser | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    const controller = new AbortController()
    setLoading(true)
    getUsers(showArchived, controller.signal).then(async (loadedUsers) => {
      setUsers(loadedUsers)
      const pairs = await Promise.all(loadedUsers.map(async (user) => [user.id, await getUserTokens(user.id, controller.signal)] as const))
      setTokens(Object.fromEntries(pairs))
    }).catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'Users could not be loaded.', true) }).finally(() => setLoading(false))
    return () => controller.abort()
  }, [announce, showArchived])
  useEffect(() => load(), [load])

  async function saveUser(event: React.FormEvent) {
    event.preventDefault()
    try { await addUser(email, role); setEmail(''); setRole('Member'); announce('User saved.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'User could not be saved.', true) }
  }

  async function changeRole(user: AllowedUser, nextRole: string) {
    try { await updateUserRole(user.id, nextRole); announce('Role updated.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Role could not be updated.', true); load() }
  }

  async function archive() {
    if (!archiving) return
    try { await archiveUser(archiving.id); setArchiving(null); announce('User archived.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'User could not be archived.', true) }
  }

  async function reactivate(user: AllowedUser) {
    try { await addUser(user.email, user.role); announce('User reactivated.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'User could not be reactivated.', true) }
  }

  async function revoke(userId: string, tokenId: string) {
    try { await revokeToken(tokenId, userId); announce('Token revoked.'); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Token could not be revoked.', true) }
  }

  function status(user: AllowedUser) { return user.isArchived ? 'Archived' : user.hasSignedIn ? 'Active' : 'Invited' }
  function date(value: string | null) { return value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—' }

  return <main className="page wide-page">
    <div className="page-heading"><div><p className="eyebrow">Administration</p><h1>Users</h1></div><label className="check-line"><input type="checkbox" checked={showArchived} onChange={(event) => setShowArchived(event.target.checked)} />Show archived</label></div>
    <form className="create-line user-create" onSubmit={saveUser}><label><span>Email</span><input type="email" value={email} required onChange={(event) => setEmail(event.target.value)} /></label><label><span>Role</span><select value={role} onChange={(event) => setRole(event.target.value as 'Member' | 'Admin')}><option value="Member">Member</option><option value="Admin">Administrator</option></select></label><button type="submit" className="primary-action">Add user</button></form>
    {loading && users.length === 0 ? <div className="loading-state">Loading users…</div> : <div className="plain-list user-list">{users.map((user) => <article className="user-row" key={user.id}>
      <div className="user-identity"><strong>{user.email}</strong><span>{status(user)} · last sign-in {date(user.lastSignedInUtc)}</span></div>
      <div className="user-role">{user.isArchived ? <span>{user.role}</span> : <select aria-label={`Role for ${user.email}`} value={user.role} onChange={(event) => changeRole(user, event.target.value)}><option value="Member">Member</option><option value="Admin">Administrator</option></select>}</div>
      <div className="user-tokens">{tokens[user.id]?.filter((token) => !token.isRevoked).map((token) => <span key={token.id}>{token.name}<button type="button" aria-label={`Revoke ${token.name} for ${user.email}`} onClick={() => revoke(user.id, token.id)}>×</button></span>)}</div>
      {user.isArchived ? <button type="button" className="text-action" onClick={() => reactivate(user)}>Reactivate</button> : <button type="button" className="text-action danger-text" onClick={() => setArchiving(user)}>Archive</button>}
    </article>)}</div>}
    <ConfirmDialog open={archiving !== null} title="Archive user?" confirmLabel="Archive" onCancel={() => setArchiving(null)} onConfirm={archive}><p>{archiving?.email} will lose access. Their health data will be retained.</p></ConfirmDialog>
  </main>
}
