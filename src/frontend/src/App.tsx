import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth, RequireRole } from './context/AuthContext'
import { NotificationCenterProvider } from './context/NotificationCenterContext'
import AppShell from './layouts/AppShell'
import {
  loadAdminAuditPage,
  loadAdminBackupPage,
  loadAdminHealthPage,
  loadAdminPeriodLocksPage,
  loadAdminUsersPage,
  loadAdvancesPage,
  loadCustomersPage,
  loadDashboardPage,
  loadDashboardPreviewPage,
  loadForbiddenPage,
  loadImportsPage,
  loadLoginPage,
  loadNotFoundPage,
  loadNotificationsPage,
  loadReceiptsPage,
  loadReportsPage,
  loadRiskAlertsPage,
} from './pages/pageLoaders'

const AdminAuditPage = lazy(loadAdminAuditPage)
const AdminBackupPage = lazy(loadAdminBackupPage)
const AdminHealthPage = lazy(loadAdminHealthPage)
const AdminPeriodLocksPage = lazy(loadAdminPeriodLocksPage)
const AdminUsersPage = lazy(loadAdminUsersPage)
const AdvancesPage = lazy(loadAdvancesPage)
const CustomersPage = lazy(loadCustomersPage)
const DashboardPage = lazy(loadDashboardPage)
const DashboardPreviewPage = lazy(loadDashboardPreviewPage)
const ForbiddenPage = lazy(loadForbiddenPage)
const ImportsPage = lazy(loadImportsPage)
const LoginPage = lazy(loadLoginPage)
const NotFoundPage = lazy(loadNotFoundPage)
const NotificationsPage = lazy(loadNotificationsPage)
const ReceiptsPage = lazy(loadReceiptsPage)
const ReportsPage = lazy(loadReportsPage)
const RiskAlertsPage = lazy(loadRiskAlertsPage)

export default function App() {
  return (
    <Suspense fallback={<div className="page-loading">Đang tải...</div>}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/dashboard-preview" element={<DashboardPreviewPage />} />
          <Route element={<NotificationCenterProvider><AppShell /></NotificationCenterProvider>}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/notifications" element={<NotificationsPage />} />
            <Route path="/imports" element={<ImportsPage />} />
            <Route path="/customers" element={<CustomersPage />} />
            <Route path="/advances" element={<AdvancesPage />} />
            <Route path="/receipts" element={<ReceiptsPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/risk" element={<RiskAlertsPage />} />
            <Route
              path="/admin/period-locks"
              element={
                <RequireRole roles={['Admin', 'Supervisor']}>
                  <AdminPeriodLocksPage />
                </RequireRole>
              }
            />
            <Route
              path="/admin/users"
              element={
                <RequireRole roles={['Admin']}>
                  <AdminUsersPage />
                </RequireRole>
              }
            />
            <Route
              path="/admin/audit"
              element={
                <RequireRole roles={['Admin', 'Supervisor']}>
                  <AdminAuditPage />
                </RequireRole>
              }
            />
            <Route
              path="/admin/health"
              element={
                <RequireRole roles={['Admin', 'Supervisor']}>
                  <AdminHealthPage />
                </RequireRole>
              }
            />
            <Route
              path="/admin/backup"
              element={
                <RequireRole roles={['Admin', 'Supervisor']}>
                  <AdminBackupPage />
                </RequireRole>
              }
            />
          </Route>
        </Route>
        <Route path="/403" element={<ForbiddenPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
