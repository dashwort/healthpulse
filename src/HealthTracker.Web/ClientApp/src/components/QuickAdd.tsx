import { useEffect, useRef, useState } from 'react'
import { createReading, updateReading } from '../api'
import { localDateTimeToUtc, toLocalDateTimeValue } from '../reading-utils'
import type { Reading, Template } from '../types'

interface QuickAddProps {
  isOpen: boolean
  templates: Template[]
  selectedTemplateId: string
  reading?: Reading | null
  onClose: () => void
  onSaved: (reading: Reading) => void
}

export function QuickAdd({
  isOpen,
  templates,
  selectedTemplateId,
  reading = null,
  onClose,
  onSaved,
}: QuickAddProps) {
  const firstInputRef = useRef<HTMLInputElement>(null)
  const [templateId, setTemplateId] = useState(selectedTemplateId)
  const [value, setValue] = useState('')
  const [unit, setUnit] = useState('')
  const [recordedAtLocal, setRecordedAtLocal] = useState(() => toLocalDateTimeValue(new Date()))
  const [showTime, setShowTime] = useState(false)
  const [showNote, setShowNote] = useState(false)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const selectedTemplate = templates.find((template) => template.id === templateId) ?? templates[0]

  useEffect(() => {
    if (!isOpen) return
    const nextTemplateId = templates.some((template) => template.id === selectedTemplateId)
      ? selectedTemplateId
      : templates[0]?.id ?? ''
    setTemplateId(nextTemplateId)
    setValue(reading ? String(reading.value) : '')
    setRecordedAtLocal(toLocalDateTimeValue(reading ? new Date(reading.recordedAtUtc) : new Date()))
    setShowTime(reading !== null)
    setShowNote(Boolean(reading?.note))
    setNote(reading?.note ?? '')
    setError(null)
    window.setTimeout(() => firstInputRef.current?.focus(), 0)
  }, [isOpen, reading, selectedTemplateId, templates])

  useEffect(() => {
    if (!selectedTemplate) return
    setUnit(reading?.unit ?? selectedTemplate.allowedUnits[0] ?? selectedTemplate.normalizedUnit)
  }, [reading?.unit, selectedTemplate])

  useEffect(() => {
    if (!isOpen) return
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, onClose])

  if (!isOpen || !selectedTemplate) return null

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsedValue = Number(value)
    if (!Number.isFinite(parsedValue) || parsedValue < 0 || parsedValue > 1_000_000) {
      setError('Enter a value between 0 and 1,000,000.')
      return
    }
    if (note.length > 140) {
      setError('Notes are limited to 140 characters.')
      return
    }

    try {
      setSaving(true)
      setError(null)
      const input = {
        value: parsedValue,
        unit,
        recordedAtUtc: localDateTimeToUtc(recordedAtLocal),
        note: note.trim() || null,
      }
      const saved = reading
        ? await updateReading(reading.id, input)
        : await createReading({ templateId: selectedTemplate.id, ...input })
      onSaved(saved)
      onClose()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'The reading could not be saved.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <button type="button" className="quick-add-backdrop" aria-label="Close quick entry" onClick={onClose} />
      <section className="quick-add" role="dialog" aria-modal="true" aria-labelledby="quick-add-title">
        <form onSubmit={handleSubmit}>
          <div className="quick-add-topline">
            <h2 id="quick-add-title">{reading ? 'Edit reading' : 'Add reading'}</h2>
            <button type="button" className="text-action" onClick={onClose}>Close</button>
          </div>

          {!reading && templates.length > 1 && (
            <div className="quick-add-metrics" aria-label="Measurement">
              {templates.map((template) => (
                <button
                  type="button"
                  key={template.id}
                  aria-pressed={template.id === selectedTemplate.id}
                  onClick={() => setTemplateId(template.id)}
                >
                  {template.name}
                </button>
              ))}
            </div>
          )}

          <div className="quick-add-fields">
            <label className="value-field">
              <span>{selectedTemplate.name}</span>
              <input
                ref={firstInputRef}
                type="number"
                min="0"
                max="1000000"
                step="any"
                inputMode="decimal"
                value={value}
                onChange={(event) => setValue(event.target.value)}
                required
              />
            </label>

            {selectedTemplate.allowedUnits.length > 1 ? (
              <label className="unit-field">
                <span>Unit</span>
                <select value={unit} onChange={(event) => setUnit(event.target.value)}>
                  {selectedTemplate.allowedUnits.map((allowedUnit) => (
                    <option value={allowedUnit} key={allowedUnit}>{allowedUnit}</option>
                  ))}
                </select>
              </label>
            ) : (
              <span className="fixed-unit">{unit}</span>
            )}

            <button type="submit" className="save-reading" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>

          <div className="quick-add-options">
            {showTime ? (
              <label className="date-time-field">
                <span>Recorded at</span>
                <input
                  type="datetime-local"
                  value={recordedAtLocal}
                  onChange={(event) => setRecordedAtLocal(event.target.value)}
                  required
                />
              </label>
            ) : (
              <button type="button" className="text-action" onClick={() => setShowTime(true)}>
                Now · change time
              </button>
            )}

            {showNote ? (
              <label className="note-field">
                <span>Note</span>
                <input
                  value={note}
                  maxLength={140}
                  onChange={(event) => setNote(event.target.value)}
                />
              </label>
            ) : (
              <button type="button" className="text-action" onClick={() => setShowNote(true)}>
                Add note
              </button>
            )}
          </div>

          {error && <p className="form-error" role="alert">{error}</p>}
        </form>
      </section>
    </>
  )
}
