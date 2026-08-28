import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { formatValue } from '../reading-utils'
import type { Reading } from '../types'

interface TrendChartProps {
  readings: Reading[]
  unit: string
  metricName: string
}

interface TooltipState {
  left: number
  top: number
  value: number
  recordedAt: Date
}

const chartHeight = 310
const margin = { top: 20, right: 74, bottom: 51, left: 60 }

function formatShortDate(value: Date): string {
  return new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' }).format(value)
}

function formatTooltipDate(value: Date): string {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(value)
}

export function TrendChart({ readings, unit, metricName }: TrendChartProps) {
  const hostRef = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(680)
  const [tooltip, setTooltip] = useState<TooltipState | null>(null)
  const clipId = `trend-clip-${useId().replace(/:/g, '')}`

  useEffect(() => {
    const host = hostRef.current
    if (!host) return

    const observer = new ResizeObserver(([entry]) => {
      setWidth(Math.max(320, Math.round(entry.contentRect.width)))
    })
    observer.observe(host)
    return () => observer.disconnect()
  }, [])

  const chart = useMemo(() => {
    if (readings.length === 0) return null

    const innerWidth = Math.max(1, width - margin.left - margin.right)
    const innerHeight = chartHeight - margin.top - margin.bottom
    const times = readings.map((reading) => Date.parse(reading.recordedAtUtc))
    const values = readings.map((reading) => reading.value)
    const minTime = Math.min(...times)
    const maxTime = Math.max(...times)
    const minValue = Math.min(...values)
    const maxValue = Math.max(...values)
    const valueSpan = Math.max(1, maxValue - minValue)
    const lower = Math.max(0, minValue - valueSpan * 0.16)
    const upper = maxValue + valueSpan * 0.16

    const x = (time: number) =>
      margin.left +
      (maxTime === minTime ? innerWidth / 2 : ((time - minTime) / (maxTime - minTime)) * innerWidth)
    const y = (value: number) => margin.top + ((upper - value) / (upper - lower)) * innerHeight
    const path = readings
      .map((reading, index) => {
        const command = index === 0 ? 'M' : 'L'
        return `${command}${x(Date.parse(reading.recordedAtUtc)).toFixed(2)},${y(reading.value).toFixed(2)}`
      })
      .join(' ')
    const yTicks = Array.from({ length: 4 }, (_, index) => upper - (index / 3) * (upper - lower))
    const xTicks = [minTime, minTime + (maxTime - minTime) / 2, maxTime]

    return { innerWidth, innerHeight, minTime, maxTime, lower, upper, x, y, path, yTicks, xTicks }
  }, [readings, width])

  function handlePointerMove(event: React.PointerEvent<SVGRectElement>) {
    if (!chart || readings.length === 0) return
    const bounds = event.currentTarget.ownerSVGElement?.getBoundingClientRect()
    if (!bounds) return

    const pointerX = ((event.clientX - bounds.left) / bounds.width) * width
    const constrainedX = Math.max(margin.left, Math.min(width - margin.right, pointerX))
    const ratio = (constrainedX - margin.left) / chart.innerWidth
    const time = chart.minTime + ratio * (chart.maxTime - chart.minTime)

    let leftIndex = 0
    while (
      leftIndex < readings.length - 2 &&
      Date.parse(readings[leftIndex + 1].recordedAtUtc) < time
    ) {
      leftIndex += 1
    }
    const rightIndex = Math.min(readings.length - 1, leftIndex + 1)
    const leftReading = readings[leftIndex]
    const rightReading = readings[rightIndex]
    const leftTime = Date.parse(leftReading.recordedAtUtc)
    const rightTime = Date.parse(rightReading.recordedAtUtc)
    const interpolation = rightTime === leftTime ? 0 : (time - leftTime) / (rightTime - leftTime)
    const value = leftReading.value + (rightReading.value - leftReading.value) * interpolation
    const top = chart.y(value)

    setTooltip({
      left: constrainedX,
      top,
      value,
      recordedAt: new Date(time),
    })
  }

  if (!chart) {
    return <div className="trend-empty">No readings in this range.</div>
  }

  const latest = readings.at(-1)!

  return (
    <div className="trend-chart-wrap" ref={hostRef}>
      <svg
        className="trend-chart"
        viewBox={`0 0 ${width} ${chartHeight}`}
        role="img"
        aria-label={`${metricName} trend with ${readings.length} readings, latest ${formatValue(latest.value)} ${unit}`}
      >
        <defs>
          <clipPath id={clipId}>
            <rect
              x={margin.left}
              y={margin.top}
              width={chart.innerWidth}
              height={chart.innerHeight}
            />
          </clipPath>
        </defs>

        {chart.yTicks.map((tick) => (
          <g key={tick}>
            <line
              className="chart-grid-line"
              x1={margin.left}
              x2={width - margin.right}
              y1={chart.y(tick)}
              y2={chart.y(tick)}
            />
            <text
              className="chart-axis-label"
              x={margin.left - 10}
              y={chart.y(tick) + 4}
              textAnchor="end"
            >
              {formatValue(tick)}
            </text>
          </g>
        ))}

        {chart.xTicks.map((tick, index) => (
          <text
            className="chart-axis-label"
            key={`${tick}-${index}`}
            x={chart.x(tick)}
            y={chartHeight - 25}
            textAnchor={index === 0 ? 'start' : index === 2 ? 'end' : 'middle'}
          >
            {formatShortDate(new Date(tick))}
          </text>
        ))}

        <text
          className="chart-axis-title"
          x={15}
          y={margin.top + chart.innerHeight / 2}
          textAnchor="middle"
          transform={`rotate(-90 15 ${margin.top + chart.innerHeight / 2})`}
        >
          {unit}
        </text>
        <text
          className="chart-axis-title"
          x={margin.left + chart.innerWidth / 2}
          y={chartHeight - 5}
          textAnchor="middle"
        >
          Recorded date
        </text>

        <g clipPath={`url(#${clipId})`}>
          {readings.length > 1 && <path className="chart-line" d={chart.path} />}
          {readings.map((reading) => (
            <circle
              className="chart-point"
              key={reading.id}
              cx={chart.x(Date.parse(reading.recordedAtUtc))}
              cy={chart.y(reading.value)}
              r={3}
            />
          ))}
          {tooltip && (
            <>
              <line
                className="chart-hover-line"
                x1={tooltip.left}
                x2={tooltip.left}
                y1={margin.top}
                y2={margin.top + chart.innerHeight}
              />
              <circle
                className="chart-hover-point"
                cx={tooltip.left}
                cy={tooltip.top}
                r={5}
              />
            </>
          )}
        </g>

        <text
          className="chart-latest-label"
          x={Math.min(width - margin.right + 9, chart.x(Date.parse(latest.recordedAtUtc)) + 9)}
          y={chart.y(latest.value) + 4}
        >
          {formatValue(latest.value)}
        </text>

        <rect
          className="chart-hit-area"
          x={margin.left}
          y={margin.top}
          width={chart.innerWidth}
          height={chart.innerHeight}
          onPointerMove={handlePointerMove}
          onPointerLeave={() => setTooltip(null)}
        />
      </svg>

      {tooltip && (
        <div
          className="chart-tooltip"
          role="tooltip"
          style={{
            left: `${(tooltip.left / width) * 100}%`,
            top: `${Math.max(6, tooltip.top - 62)}px`,
          }}
        >
          <strong>{formatValue(tooltip.value)} {unit}</strong>
          <span>{formatTooltipDate(tooltip.recordedAt)}</span>
        </div>
      )}
    </div>
  )
}
