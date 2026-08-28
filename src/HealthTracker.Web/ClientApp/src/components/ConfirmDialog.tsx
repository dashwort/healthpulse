interface ConfirmDialogProps {
  open: boolean
  title: string
  confirmLabel: string
  children: React.ReactNode
  onCancel: () => void
  onConfirm: () => void
}

export function ConfirmDialog({ open, title, confirmLabel, children, onCancel, onConfirm }: ConfirmDialogProps) {
  if (!open) return null
  return (
    <>
      <button className="modal-backdrop" type="button" aria-label="Close confirmation" onClick={onCancel} />
      <section className="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title">
        <h2 id="confirm-title">{title}</h2>
        <div className="confirm-copy">{children}</div>
        <div className="form-actions">
          <button type="button" className="text-action" onClick={onCancel}>Cancel</button>
          <button type="button" className="danger-action" onClick={onConfirm}>{confirmLabel}</button>
        </div>
      </section>
    </>
  )
}
