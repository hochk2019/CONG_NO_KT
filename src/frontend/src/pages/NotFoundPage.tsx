import { Link } from 'react-router-dom'

export default function NotFoundPage() {
  return (
    <div className="center-page">
      <h2>Không tìm thấy trang</h2>
      <p className="muted">Đường dẫn này không tồn tại hoặc đã bị chuyển.</p>
      <Link className="btn btn-outline" to="/dashboard">
        Về trang chính
      </Link>
    </div>
  )
}
