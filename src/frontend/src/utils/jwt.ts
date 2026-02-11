type JwtPayload = Record<string, unknown>

type DecodedJwt = {
  username: string | null
  roles: string[]
}

const roleKeys = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
]

const nameKeys = [
  'name',
  'unique_name',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
]

const base64UrlDecode = (input: string) => {
  let base64 = input.replace(/-/g, '+').replace(/_/g, '/')
  const padding = base64.length % 4
  if (padding) {
    base64 += '='.repeat(4 - padding)
  }
  return atob(base64)
}

const collectRoles = (payload: JwtPayload) => {
  const roles: string[] = []
  roleKeys.forEach((key) => {
    const value = payload[key]
    if (typeof value === 'string') {
      roles.push(value)
    } else if (Array.isArray(value)) {
      value.forEach((item) => {
        if (typeof item === 'string') {
          roles.push(item)
        }
      })
    }
  })
  return Array.from(new Set(roles))
}

const findName = (payload: JwtPayload) => {
  for (const key of nameKeys) {
    const value = payload[key]
    if (typeof value === 'string' && value.trim().length > 0) {
      return value
    }
  }
  return null
}

export const decodeJwt = (token: string): DecodedJwt => {
  try {
    const [, payload] = token.split('.')
    if (!payload) {
      return { username: null, roles: [] }
    }
    const decoded = JSON.parse(base64UrlDecode(payload)) as JwtPayload
    return {
      username: findName(decoded),
      roles: collectRoles(decoded),
    }
  } catch {
    return { username: null, roles: [] }
  }
}
