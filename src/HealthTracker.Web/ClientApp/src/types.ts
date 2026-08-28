export interface Session {
  isAuthenticated: boolean
  displayName: string | null
  email: string | null
  isAdministrator: boolean
  antiforgeryToken: string | null
}

export interface Template {
  id: string
  code: string | null
  name: string
  category: string
  normalizedUnit: string
  allowedUnits: string[]
  isCustom: boolean
  isTracked: boolean
}

export interface Reading {
  id: string
  templateId: string
  templateName: string
  value: number
  unit: string
  recordedAtUtc: string
  note: string | null
}

export interface ReadingPage {
  items: Reading[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ReadingInput {
  templateId: string
  value: number
  unit: string
  recordedAtUtc: string
  note: string | null
}

export interface CustomTemplateInput {
  name: string
  category: string
  unit: string
}

export interface PersonalAccessToken {
  id: string
  name: string
  prefix: string
  expiresUtc: string
  lastUsedUtc: string | null
  isRevoked: boolean
}

export interface CreatedPersonalAccessToken {
  token: PersonalAccessToken
  secret: string
}

export interface AllowedUser {
  id: string
  email: string
  role: 'Member' | 'Admin'
  hasSignedIn: boolean
  firstSignedInUtc: string | null
  lastSignedInUtc: string | null
  isArchived: boolean
}

export interface AppInfo {
  deployment: { version: string; build: string; commit: string; builtAtUtc: string }
  android: { latestVersion: string; apkUrl: string | null; releaseNotes: string }
}

export interface AccessActivity {
  id: string
  allowedUserId: string | null
  userEmail: string | null
  type: string
  outcome: string
  failureReason: string | null
  occurredUtc: string
  sourceIpAddress: string | null
  userAgent: string | null
}

export interface AccessActivityPage {
  items: AccessActivity[]
  totalCount: number
  page: number
  pageSize: number
}

export interface LogSnapshot {
  content: string
  fileCount: number
  generatedAtUtc: string
}
