import { useCallback, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthStore'
import type { CustomerListItem } from '../../api/customers'
import Customer360Section from './Customer360Section'
import CustomerListSection from './CustomerListSection'
import CustomerTransactionsSection from './CustomerTransactionsSection'

type CustomerTab = 'invoices' | 'advances' | 'receipts'

const parseTaxCode = (value: string | null) => {
  const normalized = value?.trim() ?? ''
  return normalized.length > 0 ? normalized : null
}

const parseTab = (value: string | null): CustomerTab | undefined => {
  if (value === 'invoices' || value === 'advances' || value === 'receipts') {
    return value
  }
  return undefined
}

const parseDoc = (value: string | null) => {
  const normalized = value?.trim() ?? ''
  return normalized.length > 0 ? normalized : null
}

export default function CustomersPage() {
  const { state } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const token = state.accessToken ?? ''
  const canManageCustomers = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const queryTaxCode = useMemo(() => parseTaxCode(searchParams.get('taxCode')), [searchParams])
  const queryTab = useMemo(() => parseTab(searchParams.get('tab')), [searchParams])
  const queryDoc = useMemo(() => parseDoc(searchParams.get('doc')), [searchParams])

  const selectedTaxCode = queryTaxCode
  const [selectedCustomer, setSelectedCustomer] = useState<{ taxCode: string; name: string } | null>(null)
  const selectedName =
    selectedCustomer && selectedCustomer.taxCode === selectedTaxCode ? selectedCustomer.name : ''

  const updateSearchParams = useCallback((updater: (next: URLSearchParams) => void) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      updater(next)
      return next
    }, { replace: true })
  }, [setSearchParams])

  const handleSelectCustomer = useCallback((row: CustomerListItem) => {
    setSelectedCustomer({ taxCode: row.taxCode, name: row.name })
    updateSearchParams((next) => {
      next.set('taxCode', row.taxCode)
      next.delete('tab')
      next.delete('doc')
    })
  }, [updateSearchParams])

  const handleClearSelection = useCallback(() => {
    setSelectedCustomer(null)
    updateSearchParams((next) => {
      next.delete('taxCode')
      next.delete('tab')
      next.delete('doc')
    })
  }, [updateSearchParams])

  const handleTabChange = useCallback((tab: CustomerTab) => {
    if (!selectedTaxCode) return
    updateSearchParams((next) => {
      next.set('taxCode', selectedTaxCode)
      next.set('tab', tab)
      next.delete('doc')
    })
  }, [selectedTaxCode, updateSearchParams])

  return (
    <div className="page-stack">
      <CustomerListSection
        token={token}
        canManageCustomers={canManageCustomers}
        selectedTaxCode={selectedTaxCode}
        selectedName={selectedName}
        onSelectCustomer={handleSelectCustomer}
      />
      <Customer360Section token={token} selectedTaxCode={selectedTaxCode} selectedName={selectedName} />
      <CustomerTransactionsSection
        token={token}
        canManageCustomers={canManageCustomers}
        selectedTaxCode={selectedTaxCode}
        selectedName={selectedName}
        initialTab={queryTab}
        initialDoc={queryDoc}
        onTabChange={handleTabChange}
        onClearSelection={handleClearSelection}
      />
    </div>
  )
}
