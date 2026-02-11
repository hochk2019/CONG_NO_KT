import { Link } from 'react-router-dom'

export default function ForbiddenPage() {
  return (
    <div className="center-page">
      <h2>Không có quyền truy cập</h2>
      <p className="muted">
        Bạn không có quyền vào khu vực này. Vui lòng liên hệ quản trị hệ thống.
      </p>
      <Link className="btn btn-primary" to="/dashboard">
        Quay về tổng quan
      </Link>
    </div>
  )
}
