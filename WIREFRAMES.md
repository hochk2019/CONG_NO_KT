# WIREFRAMES - Cong No Golden (text)

## Login
- Fields: username, password
- Actions: Login

## Dashboard
- KPI cards: total receivable, overdue, credit
- Top debtors table
- (Optional) trend chart (6 months)

## Customers list
- Filters: search (tax code/name), owner, status
- Table: tax_code, name, owner, current_balance, status
- Actions: view detail

## Customer detail
- Tabs: Overview, Invoices, Advances, Receipts, Statement, Audit
- Overview cards: current_balance, overdue, credit

## Import Wizard
- Step 1: upload file, select period, select seller
- Step 2: preview rows with OK/WARN/ERROR, filters
- Step 3: commit with confirmation
- History: list batches, view detail, rollback (admin)

## Receipts
- Fields: seller, customer, receipt_date, applied_period_start, amount, method
- Mode: BY_INVOICE / BY_PERIOD / FIFO
- Preview allocations table (editable if allowed)
- Actions: save draft, approve, void

## Advances
- Fields: seller, customer, advance_date, amount, description
- Actions: save draft, approve, void

## Reports
- Summary filters: seller, customer, owner, period
- Tabs: summary, statement, aging
- Actions: export excel

## Admin
- Users/Roles management
- Period lock list + lock/unlock (with reason)
- Audit log viewer
