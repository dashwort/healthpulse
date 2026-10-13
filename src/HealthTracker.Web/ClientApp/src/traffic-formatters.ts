export interface ChartAxisTick {
  value: number
  y: number
}

export function chartYAxisTicks(maxValue: number): ChartAxisTick[] {
  const ticks: ChartAxisTick[] = [{ value: maxValue, y: 17 }]
  const midpoint = Math.floor(maxValue / 2)
  if (midpoint > 0 && midpoint < maxValue) {
    ticks.push({ value: midpoint, y: 76 })
  }
  ticks.push({ value: 0, y: 136 })
  return ticks
}

export function formatTrafficRouteTemplate(routeTemplate: string): string {
  return routeTemplate === 'unmatched' ? 'Other (no route template)' : routeTemplate
}
