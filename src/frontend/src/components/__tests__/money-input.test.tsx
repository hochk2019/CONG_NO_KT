import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import MoneyInput from '../MoneyInput'

function MoneyInputHarness() {
  const [value, setValue] = useState('')

  return (
    <>
      <label>
        Số tiền
        <MoneyInput value={value} onValueChange={setValue} />
      </label>
      <output data-testid="raw-value">{value}</output>
    </>
  )
}

describe('MoneyInput', () => {
  it('formats thousands while typing and keeps raw digits in state', async () => {
    const user = userEvent.setup()

    render(<MoneyInputHarness />)

    const input = screen.getByLabelText('Số tiền')
    await user.type(input, '1000000')

    expect(input).toHaveValue('1.000.000')
    expect(screen.getByTestId('raw-value')).toHaveTextContent('1000000')
  })

  it('removes grouping safely when backspacing a formatted integer', async () => {
    const user = userEvent.setup()

    render(<MoneyInputHarness />)

    const input = screen.getByLabelText('Số tiền')
    await user.type(input, '1000')
    await user.type(input, '{backspace}')

    expect(input).toHaveValue('100')
    expect(screen.getByTestId('raw-value')).toHaveTextContent('100')
  })

  it('preserves vi-VN decimal input with a comma separator', () => {
    render(<MoneyInputHarness />)

    const input = screen.getByLabelText('Số tiền')
    fireEvent.change(input, { target: { value: '1.234,5' } })

    expect(input).toHaveValue('1.234,5')
    expect(screen.getByTestId('raw-value')).toHaveTextContent('1234.5')
  })
})
