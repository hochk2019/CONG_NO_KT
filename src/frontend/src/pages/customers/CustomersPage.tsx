import { useCallback, useState } from 'react'
import { useAuth } from '../../context/AuthStore'
import type { CustomerListItem } from '../../api/customers'
import CustomerListSection from './CustomerListSection'
import CustomerTransactionsSection from './CustomerTransactionsSection'

export default function CustomersPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canManageCustomers = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const [selectedTaxCode, setSelectedTaxCode] = useState<string | null>(null)
  const [selectedName, setSelectedName] = useState('')

  const handleSelectCustomer = useCallback((row: CustomerListItem) => {
    setSelectedTaxCode(row.taxCode)
    setSelectedName(row.name)
  }, [])

  const handleClearSelection = useCallback(() => {
    setSelectedTaxCode(null)
    setSelectedName('')
  }, [])

  return (
    <div className="page-stack">
      <CustomerListSection
        token={token}
        canManageCustomers={canManageCustomers}
        selectedTaxCode={selectedTaxCode}
        selectedName={selectedName}
        onSelectCustomer={handleSelectCustomer}
      />
      <CustomerTransactionsSection
        token={token}
        canManageCustomers={canManageCustomers}
        selectedTaxCode={selectedTaxCode}
        selectedName={selectedName}
        onClearSelection={handleClearSelection}
      />
    </div>
  )
}
