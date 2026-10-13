import { describe, expect, it } from '@rstest/core'
import { chartYAxisTicks, formatTrafficRouteTemplate } from './traffic-formatters'

describe('traffic diagnostics formatting', () => {
  it('does not repeat a zero tick for a single-event chart', () => {
    expect(chartYAxisTicks(1)).toEqual([
      { value: 1, y: 17 },
      { value: 0, y: 136 },
    ])
  })

  it('keeps a distinct midpoint when the chart has at least two events', () => {
    expect(chartYAxisTicks(2)).toEqual([
      { value: 2, y: 17 },
      { value: 1, y: 76 },
      { value: 0, y: 136 },
    ])
  })

  it('explains when a request has no route template', () => {
    expect(formatTrafficRouteTemplate('unmatched')).toBe('Other (no route template)')
    expect(formatTrafficRouteTemplate('api/readings')).toBe('api/readings')
  })
})
