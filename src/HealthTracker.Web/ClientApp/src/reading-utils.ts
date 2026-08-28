import type { Reading } from './types'

export function getRangeStartUtc(now: Date, days: number): string {
  const start = new Date(now)
  start.setUTCDate(start.getUTCDate() - days)
  return start.toISOString()
}

export function sortReadingsAscending(readings: Reading[]): Reading[] {
  return [...readings].sort(
    (left, right) => Date.parse(left.recordedAtUtc) - Date.parse(right.recordedAtUtc),
  )
}

export function toLocalDateTimeValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function localDateTimeToUtc(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    throw new Error('Enter a valid date and time.')
  }
  return date.toISOString()
}

export function formatValue(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}
