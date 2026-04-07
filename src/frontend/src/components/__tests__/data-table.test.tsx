import { fireEvent, render, screen } from '@testing-library/react'
import DataTable from '../DataTable'

type TestRow = {
  id: string
  name: string
}

const columns = [
  {
    key: 'name',
    label: 'Tên',
  },
]

const rows: TestRow[] = [
  { id: 'row-1', name: 'Khách hàng A' },
]

const setScrollerMetrics = (
  scroller: HTMLDivElement,
  metrics: { clientWidth: number; scrollWidth: number; scrollLeft: number },
) => {
  Object.defineProperty(scroller, 'clientWidth', {
    configurable: true,
    value: metrics.clientWidth,
  })
  Object.defineProperty(scroller, 'scrollWidth', {
    configurable: true,
    value: metrics.scrollWidth,
  })
  Object.defineProperty(scroller, 'scrollLeft', {
    configurable: true,
    writable: true,
    value: metrics.scrollLeft,
  })
}

describe('DataTable', () => {
  it('shows the scroll hint while horizontal overflow remains', () => {
    render(
      <DataTable
        columns={columns}
        rows={rows}
        getRowKey={(row) => row.id}
      />,
    )

    const scroller = screen.getByRole('table').parentElement as HTMLDivElement
    setScrollerMetrics(scroller, {
      clientWidth: 320,
      scrollWidth: 640,
      scrollLeft: 0,
    })

    fireEvent.scroll(scroller)

    expect(scroller).toHaveClass('table-scroll')
    expect(scroller).not.toHaveClass('table-scroll--no-hint')
  })

  it('hides the scroll hint after reaching the far right edge', () => {
    render(
      <DataTable
        columns={columns}
        rows={rows}
        getRowKey={(row) => row.id}
      />,
    )

    const scroller = screen.getByRole('table').parentElement as HTMLDivElement
    setScrollerMetrics(scroller, {
      clientWidth: 320,
      scrollWidth: 640,
      scrollLeft: 0,
    })
    fireEvent.scroll(scroller)

    setScrollerMetrics(scroller, {
      clientWidth: 320,
      scrollWidth: 640,
      scrollLeft: 320,
    })
    fireEvent.scroll(scroller)

    expect(scroller).toHaveClass('table-scroll--no-hint')
  })
})
