import type {
  AccessActivityPage, AllowedUser, AppInfo, CreatedPersonalAccessToken,
  CustomTemplateInput, LogSnapshot, PersonalAccessToken, Reading, ReadingInput,
  ReadingPage, Session, Template,
} from './types'

interface ProblemDetails {
  detail?: string
  title?: string
  errors?: Record<string, string[]>
}

let antiforgeryToken: string | null = null

export function setAntiforgeryToken(token: string | null) {
  antiforgeryToken = token
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  const response = await fetch(input, {
    credentials: 'same-origin',
    ...init,
    headers: {
      Accept: 'application/json',
      ...(antiforgeryToken ? { RequestVerificationToken: antiforgeryToken } : {}),
      ...init?.headers,
    },
  })

  if (response.status === 401) {
    window.location.assign('/login')
    throw new Error('Authentication required.')
  }

  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? ''
    const problem = contentType.includes('application/json')
      ? ((await response.json()) as ProblemDetails)
      : null
    const validationMessage = problem?.errors ? Object.values(problem.errors).flat().at(0) : null
    throw new Error(validationMessage ?? problem?.detail ?? problem?.title ?? `Request failed (${response.status}).`)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const getSession = (signal?: AbortSignal) => request<Session>('/api/app/session', { signal })
export const getAppInfo = (signal?: AbortSignal) => request<AppInfo>('/api/app/info', { signal })
export const getCatalogue = (signal?: AbortSignal) => request<Template[]>('/api/templates/catalogue', { signal })
export const getTrackedTemplates = (signal?: AbortSignal) => request<Template[]>('/api/templates/tracked', { signal })

export function getReadingPage(
  filters: { templateId?: string; fromUtc?: string; toUtc?: string; page?: number; pageSize?: number },
  signal?: AbortSignal,
): Promise<ReadingPage> {
  const query = new URLSearchParams()
  if (filters.templateId) query.set('templateId', filters.templateId)
  if (filters.fromUtc) query.set('fromUtc', filters.fromUtc)
  if (filters.toUtc) query.set('toUtc', filters.toUtc)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 25))
  return request<ReadingPage>(`/api/readings?${query}`, { signal })
}

export function getReadings(templateId: string, fromUtc: string, toUtc: string, signal?: AbortSignal) {
  return getReadingPage({ templateId, fromUtc, toUtc, page: 1, pageSize: 100 }, signal)
}

export function createReading(reading: ReadingInput): Promise<Reading> {
  return request<Reading>('/api/readings', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(reading) })
}

export function updateReading(id: string, reading: Omit<ReadingInput, 'templateId'>): Promise<Reading> {
  return request<Reading>(`/api/readings/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(reading) })
}

export const deleteReading = (id: string) => request<void>(`/api/readings/${id}`, { method: 'DELETE' })

export function createCustomTemplate(input: CustomTemplateInput): Promise<Template> {
  return request<Template>('/api/templates/custom', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) })
}

export function updateCustomTemplate(id: string, input: CustomTemplateInput): Promise<Template> {
  return request<Template>(`/api/templates/custom/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) })
}

export const deleteCustomTemplate = (id: string) => request<void>(`/api/templates/custom/${id}`, { method: 'DELETE' })
export const setTemplateTracking = (id: string, tracked: boolean) => request<void>(`/api/templates/${id}/track`, { method: tracked ? 'POST' : 'DELETE' })

export const getTokens = (signal?: AbortSignal) => request<PersonalAccessToken[]>('/api/tokens', { signal })
export function createToken(name: string): Promise<CreatedPersonalAccessToken> {
  return request<CreatedPersonalAccessToken>('/api/tokens', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name }) })
}
export function revokeToken(id: string, userId?: string): Promise<void> {
  return request<void>(userId ? `/api/tokens/users/${userId}/${id}` : `/api/tokens/${id}`, { method: 'DELETE' })
}
export const getUserTokens = (userId: string, signal?: AbortSignal) => request<PersonalAccessToken[]>(`/api/tokens/users/${userId}`, { signal })

export const getUsers = (includeArchived = false, signal?: AbortSignal) => request<AllowedUser[]>(`/api/users?includeArchived=${includeArchived}`, { signal })
export function addUser(email: string, role: string): Promise<AllowedUser> {
  return request<AllowedUser>('/api/users', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, role }) })
}
export function updateUserRole(id: string, role: string): Promise<AllowedUser> {
  return request<AllowedUser>(`/api/users/${id}/role`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ role }) })
}
export const archiveUser = (id: string) => request<void>(`/api/users/${id}`, { method: 'DELETE' })

export const getLogSnapshot = (signal?: AbortSignal) => request<LogSnapshot>('/api/admin/diagnostics/logs', { signal })
export function getAccessActivity(filters: { userId?: string; type?: string; outcome?: string; page?: number }, signal?: AbortSignal) {
  const query = new URLSearchParams({ page: String(filters.page ?? 1), pageSize: '50' })
  if (filters.userId) query.set('userId', filters.userId)
  if (filters.type) query.set('type', filters.type)
  if (filters.outcome) query.set('outcome', filters.outcome)
  return request<AccessActivityPage>(`/api/admin/diagnostics/activity?${query}`, { signal })
}
