import { useCallback, useEffect, useMemo, useState } from 'react'
import { createCustomTemplate, deleteCustomTemplate, getCatalogue, setTemplateTracking, updateCustomTemplate } from '../api'
import { ConfirmDialog } from '../components/ConfirmDialog'
import type { CustomTemplateInput, Template } from '../types'

interface TemplatesPageProps { onChanged: () => void; announce: (message: string, error?: boolean) => void }
const emptyForm: CustomTemplateInput = { name: '', category: 'Custom', unit: '' }

export function TemplatesPage({ onChanged, announce }: TemplatesPageProps) {
  const [templates, setTemplates] = useState<Template[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Template | 'new' | null>(null)
  const [form, setForm] = useState<CustomTemplateInput>(emptyForm)
  const [deleting, setDeleting] = useState<Template | null>(null)

  const load = useCallback(() => {
    const controller = new AbortController()
    setLoading(true)
    getCatalogue(controller.signal).then(setTemplates)
      .catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'Templates could not be loaded.', true) })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [announce])
  useEffect(() => load(), [load])

  const groups = useMemo(() => Object.entries(templates.reduce<Record<string, Template[]>>((result, template) => {
    ;(result[template.category] ??= []).push(template)
    return result
  }, {})), [templates])

  function beginEdit(template?: Template) {
    setEditing(template ?? 'new')
    setForm(template ? { name: template.name, category: template.category, unit: template.normalizedUnit } : emptyForm)
  }

  async function save(event: React.FormEvent) {
    event.preventDefault()
    try {
      if (editing === 'new') await createCustomTemplate(form)
      else if (editing) await updateCustomTemplate(editing.id, form)
      setEditing(null); announce('Template saved.'); onChanged(); load()
    } catch (error) { announce(error instanceof Error ? error.message : 'Template could not be saved.', true) }
  }

  async function toggle(template: Template) {
    try { await setTemplateTracking(template.id, !template.isTracked); announce(template.isTracked ? 'Tracking stopped. History remains available.' : 'Tracking enabled.'); onChanged(); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Tracking could not be changed.', true) }
  }

  async function remove() {
    if (!deleting) return
    try { await deleteCustomTemplate(deleting.id); setDeleting(null); announce('Custom template deleted.'); onChanged(); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Template could not be deleted.', true) }
  }

  return (
    <main className="page">
      <div className="page-heading"><div><p className="eyebrow">Measurements</p><h1>Templates</h1></div><button type="button" className="line-button" onClick={() => beginEdit()}>Custom template</button></div>

      {editing && <form className="inline-editor" onSubmit={save}>
        <div className="editor-heading"><h2>{editing === 'new' ? 'Create custom template' : 'Edit custom template'}</h2><button type="button" className="text-action" onClick={() => setEditing(null)}>Close</button></div>
        <div className="form-line">
          <label><span>Name</span><input value={form.name} maxLength={100} required onChange={(event) => setForm({ ...form, name: event.target.value })} /></label>
          <label><span>Category</span><input value={form.category} maxLength={100} required onChange={(event) => setForm({ ...form, category: event.target.value })} /></label>
          <label><span>Unit</span><input value={form.unit} maxLength={30} required onChange={(event) => setForm({ ...form, unit: event.target.value })} /></label>
          <button type="submit" className="primary-action">Save</button>
        </div>
      </form>}

      {loading && templates.length === 0 ? <div className="loading-state">Loading templates…</div> : groups.map(([category, items]) => <section className="template-group" key={category}>
        <h2>{category}</h2>
        {items?.map((template) => <div className="template-row" key={template.id}>
          <div><strong>{template.name}</strong><span>{template.allowedUnits.join(' · ')}</span></div>
          <div className="template-actions">
            {template.isCustom && <button type="button" className="text-action" aria-label={`Edit ${template.name} custom template`} onClick={() => beginEdit(template)}>Edit</button>}
            {template.isCustom && <button type="button" className="text-action danger-text" aria-label={`Delete ${template.name} custom template`} onClick={() => setDeleting(template)}>Delete</button>}
            <button type="button" className="track-toggle" role="switch" aria-checked={template.isTracked} onClick={() => toggle(template)}><span />{template.isTracked ? 'Tracked' : 'Track'}</button>
          </div>
        </div>)}
      </section>)}
      <ConfirmDialog open={deleting !== null} title="Delete custom template?" confirmLabel="Delete" onCancel={() => setDeleting(null)} onConfirm={remove}><p>Tracking will stop. Existing readings stay in history.</p></ConfirmDialog>
    </main>
  )
}
