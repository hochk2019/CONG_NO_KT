import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth, RequirePermission, RequireRole } from './context/AuthContext'
import { NotificationCenterProvider } from './context/NotificationCenterContext'
import AppShell from './layouts/AppShell'
import {
  loadAdminAuditPage,
  loadAdminBackupPage,
  loadAdminErpIntegrationPage,
  loadAdminHealthPage,
  loadAdminPeriodLocksPage,
  loadAdminUsersPage,
  loadAdvancesPage,
  loadCollectionsPage,
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
const AdminErpIntegrationPage = lazy(loadAdminErpIntegrationPage)
const AdminHealthPage = lazy(loadAdminHealthPage)
const AdminPeriodLocksPage = lazy(loadAdminPeriodLocksPage)
const AdminUsersPage = lazy(loadAdminUsersPage)
const AdvancesPage = lazy(loadAdvancesPage)
const CollectionsPage = lazy(loadCollectionsPage)
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
            <Route path="/collections" element={<CollectionsPage />} />
            <Route path="/receipts" element={<ReceiptsPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/risk" element={<RiskAlertsPage />} />
            <Route
              path="/admin/period-locks"
              element={
                <RequirePermission permissions={['period.lock.manage']}>
                  <AdminPeriodLocksPage />
                </RequirePermission>
              }
            />
            <Route
              path="/admin/users"
              element={
                <RequirePermission permissions={['admin.manage']}>
                  <AdminUsersPage />
                </RequirePermission>
              }
            />
            <Route
              path="/admin/audit"
              element={
                <RequirePermission permissions={['audit.view']}>
                  <AdminAuditPage />
                </RequirePermission>
              }
            />
            <Route
              path="/admin/health"
              element={
                <RequirePermission permissions={['admin.health.view']}>
                  <AdminHealthPage />
                </RequirePermission>
              }
            />
            <Route
              path="/admin/erp-integration"
              element={
                <RequireRole roles={['Admin', 'Supervisor']}>
                  <AdminErpIntegrationPage />
                </RequireRole>
              }
            />
            <Route
              path="/admin/backup"
              element={
                <RequirePermission permissions={['backup.manage']}>
                  <AdminBackupPage />
                </RequirePermission>
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
