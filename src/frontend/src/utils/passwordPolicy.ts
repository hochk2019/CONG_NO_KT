const MIN_PASSWORD_LENGTH = 8

export const validatePasswordPolicy = (password: string): string | null => {
  const value = (password ?? '').trim()

  if (!value) {
    return 'Mật khẩu là bắt buộc.'
  }

  if (value.length < MIN_PASSWORD_LENGTH) {
    return `Mật khẩu phải có ít nhất ${MIN_PASSWORD_LENGTH} ký tự.`
  }

  if (!/[A-Z]/.test(value)) {
    return 'Mật khẩu phải có ít nhất 1 chữ hoa.'
  }

  if (!/[a-z]/.test(value)) {
    return 'Mật khẩu phải có ít nhất 1 chữ thường.'
  }

  if (!/[0-9]/.test(value)) {
    return 'Mật khẩu phải có ít nhất 1 chữ số.'
  }

  return null
}
