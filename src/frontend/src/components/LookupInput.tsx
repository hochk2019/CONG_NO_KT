import { useId } from 'react'
import type { ComponentPropsWithoutRef } from 'react'
import type { LookupOption } from '../api/lookups'

type LookupInputProps = {
  label: string
  value: string
  options: LookupOption[]
  placeholder?: string
  helpText?: string
  onChange: (value: string) => void
  type?: string
  autoComplete?: string
  inputMode?: ComponentPropsWithoutRef<'input'>['inputMode']
  onBlur?: () => void
  errorText?: string
}

export default function LookupInput({
  label,
  value,
  options,
  placeholder,
  helpText,
  onChange,
  type = 'text',
  autoComplete,
  inputMode,
  onBlur,
  errorText,
}: LookupInputProps) {
  const listId = useId().replace(/:/g, '')
  const helpId = helpText ? `${listId}-help` : undefined
  const errorId = errorText ? `${listId}-error` : undefined
  const describedBy = [helpId, errorId].filter(Boolean).join(' ') || undefined

  return (
    <label className={errorText ? 'field field--error' : 'field'}>
      <span>{label}</span>
      <input
        type={type}
        list={listId}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        autoComplete={autoComplete}
        inputMode={inputMode}
        onBlur={onBlur}
        aria-invalid={Boolean(errorText)}
        aria-describedby={describedBy}
      />
      <datalist id={listId}>
        {options.map((option) => (
          <option key={`${option.value}-${option.label}`} value={option.value}>
            {option.label}
          </option>
        ))}
      </datalist>
      {helpText && (
        <span className="muted" id={helpId}>
          {helpText}
        </span>
      )}
      {errorText && (
        <span className="field-error" id={errorId}>
          {errorText}
        </span>
      )}
    </label>
  )
}
