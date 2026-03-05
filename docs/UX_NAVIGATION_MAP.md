# UX_NAVIGATION_MAP - Cong No Golden

## Main navigation (sidebar)
- Tổng quan (`/dashboard`)
- Nhập liệu (`/imports`)
- Khoản trả hộ (`/advances`)
- Khách hàng (`/customers`)
- Thu hồi nợ (`/collections`)
- Thu tiền (`/receipts`)
- Báo cáo (`/reports`)
- Cảnh báo rủi ro (`/risk`)
- Admin:
  - Người dùng (`/admin/users`)
  - Khóa kỳ (`/admin/period-locks`)
  - Nhật ký (`/admin/audit`)
  - Tình trạng dữ liệu (`/admin/health`)
  - Tích hợp ERP (`/admin/erp-integration`)
  - Sao lưu dữ liệu (`/admin/backup`)

## Route inventory and entry points
| Route | Business scope | Entry point |
| --- | --- | --- |
| `/dashboard` | Executive/KPI overview | Sidebar menu |
| `/notifications` | Reminder, escalation, risk alerts history | Notification bell/toast deep-link (không nằm trên sidebar) |
| `/imports` | Invoice/Advance/Receipt import pipeline + manual invoice entry | Sidebar menu |
| `/advances` | Advance management + import template | Sidebar menu |
| `/customers` | Customer ledger + document drill-down | Sidebar menu + search palette deep-link |
| `/collections` | Collection task queue/workboard | Sidebar menu |
| `/receipts` | Draft/approve receipt + allocations | Sidebar menu |
| `/reports` | Aging/statement/summary reporting | Sidebar menu |
| `/risk` | Risk scoring + alerts + action recommendations | Sidebar menu |
| `/admin/users` | User and role administration | Sidebar menu (Admin) |
| `/admin/period-locks` | Accounting period lock/unlock | Sidebar menu (Admin/Supervisor) |
| `/admin/audit` | Audit and compliance trail | Sidebar menu (Admin/Supervisor) |
| `/admin/health` | Data and integrations health checks | Sidebar menu (Admin/Supervisor) |
| `/admin/erp-integration` | ERP sync status and controls | Sidebar menu (Admin/Supervisor) |
| `/admin/backup` | Backup/restore operations | Sidebar menu (Admin/Supervisor) |
| `/dashboard-preview` | Lightweight preview surface (non-primary workspace) | Explicit deep-link only |

## Key flows
- Login -> Dashboard
- Import Wizard: Upload -> Preview -> Commit -> History
- Invoices: Nhập file hoặc nhập thủ công tại `Imports` -> tra cứu/đối soát tại `Khách hàng`
- Collections: Generate queue -> assign priority -> track outcomes
- Receipts: Create draft -> Preview allocations -> Approve
- Advances: Create -> Approve -> Void/Reversal
- Reports: Filters -> View -> Export/Print
- Admin: Manage users, lock/unlock periods, monitor integrations, run backup/restore
