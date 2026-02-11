import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../context/AuthStore'

export default function LoginPage() {
  const { login, isAuthenticated, isBootstrapping } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (!isBootstrapping && isAuthenticated) {
      const next = (location.state as { from?: Location })?.from?.pathname ?? '/dashboard'
      navigate(next, { replace: true })
    }
  }, [isAuthenticated, isBootstrapping, location.state, navigate])

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(username.trim(), password)
      const next = (location.state as { from?: Location })?.from?.pathname ?? '/dashboard'
      navigate(next, { replace: true })
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Đăng nhập thất bại, vui lòng thử lại.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="auth-layout">
      <div className="auth-panel">
        <div className="brand brand--large">
          <span className="brand__kicker">Golden Logistics</span>
          <span className="brand__title">Quản lý công nợ</span>
        </div>
        <p className="auth-copy">
          Đăng nhập để theo dõi các đợt import, phê duyệt và xuất báo cáo công nợ.
        </p>
      </div>
      <div className="auth-card">
        <h2>Đăng nhập</h2>
        <p className="muted">Dùng tài khoản quản trị hoặc kế toán của bạn.</p>
        <form onSubmit={handleSubmit} className="form-stack">
          <label className="field">
            <span>Tên đăng nhập</span>
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              placeholder="admin"
              autoComplete="username"
            />
          </label>
          <label className="field">
            <span>Mật khẩu</span>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="********"
              autoComplete="current-password"
            />
          </label>
          {error && <div className="alert alert--error" role="alert" aria-live="assertive">{error}</div>}
          <button className="btn btn-primary btn-block" disabled={isSubmitting}>
            {isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập'}
          </button>
        </form>
      </div>
    </div>
  )
}
