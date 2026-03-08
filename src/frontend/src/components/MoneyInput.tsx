import { forwardRef, type ComponentPropsWithoutRef } from 'react'
import { formatMoneyInput, normalizeMoneyInput } from '../utils/moneyInput'

type MoneyInputProps = Omit<
  ComponentPropsWithoutRef<'input'>,
  'type' | 'inputMode' | 'value' | 'onChange'
> & {
  value: string
  onValueChange: (value: string) => void
}

const MoneyInput = forwardRef<HTMLInputElement, MoneyInputProps>(function MoneyInput(
  { value, onValueChange, ...props },
  ref,
) {
  return (
    <input
      {...props}
      ref={ref}
      type="text"
      inputMode="decimal"
      autoComplete="off"
      pattern="[0-9.,]*"
      value={formatMoneyInput(value)}
      onChange={(event) => {
        onValueChange(normalizeMoneyInput(event.target.value))
      }}
    />
  )
})

export default MoneyInput
