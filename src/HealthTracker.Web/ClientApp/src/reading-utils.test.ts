import { describe, expect, it } from '@rstest/core'
import {
  getRangeStartUtc,
  localDateTimeToUtc,
  sortReadingsAscending,
  toLocalDateTimeValue,
} from './reading-utils'
import type { Reading } from './types'

function reading(id: string, recordedAtUtc: string): Reading {
  return {
    id,
    templateId: 'template',
    templateName: 'Weight',
    value: 75,
    unit: 'kg',
    recordedAtUtc,
    note: null,
  }
}

describe('reading utilities', () => {
  it('calculates a UTC range without changing the supplied date', () => {
    const now = new Date('2026-08-27T10:30:00.000Z')
    expect(getRangeStartUtc(now, 30)).toBe('2026-07-28T10:30:00.000Z')
    expect(now.toISOString()).toBe('2026-08-27T10:30:00.000Z')
  })

  it('sorts readings without mutating the API result', () => {
    const input = [
      reading('later', '2026-08-27T10:00:00Z'),
      reading('earlier', '2026-08-26T10:00:00Z'),
    ]
    expect(sortReadingsAscending(input).map((item) => item.id)).toEqual(['earlier', 'later'])
    expect(input.map((item) => item.id)).toEqual(['later', 'earlier'])
  })

  it('round-trips the local date-time control value through UTC', () => {
    const local = new Date(2026, 7, 27, 8, 41)
    expect(localDateTimeToUtc(toLocalDateTimeValue(local))).toBe(local.toISOString())
  })
})
