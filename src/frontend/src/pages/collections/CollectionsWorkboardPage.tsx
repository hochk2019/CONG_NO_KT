import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  assignCollectionTask,
  generateCollectionTasks,
  listCollectionTasks,
  updateCollectionTaskStatus,
  type CollectionTask,
  type CollectionTaskStatus,
} from '../../api/collections'
import { ApiError } from '../../api/client'
import { fetchOwnerLookup, fetchUserLookup, mapOwnerOptions, type LookupOption } from '../../api/lookups'
import DataTable from '../../components/DataTable'
import { useAuth } from '../../context/AuthStore'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatDateTime, formatMoney } from '../../utils/format'

const PAGE_SIZE_STORAGE_KEY = 'pref.collections.pageSize'
const STATUS_STORAGE_KEY = 'pref.collections.status'
const ASSIGNED_STORAGE_KEY = 'pref.collections.assignedTo'
const TAKE_STORAGE_KEY = 'pref.collections.take'

const DEFAULT_PAGE_SIZE = 10
const DEFAULT_TAKE = 200
const DEFAULT_GENERATE_TAKE = 30
const DEFAULT_MIN_PRIORITY_SCORE = '0.35'
const RISK_QUERY_KEYS = ['fromRisk', 'asOfDate', 'ownerId', 'level', 'minPriorityScore'] as const

const riskLevelLabels: Record<string, string> = {
  VERY_HIGH: 'Rất cao',
  HIGH: 'Cao',
  MEDIUM: 'Trung bình',
  LOW: 'Thấp',
}

const riskLevelToPriorityScore: Record<string, string> = {
  VERY_HIGH: '0.80',
  HIGH: '0.65',
  MEDIUM: '0.50',
  LOW: '0.35',
}

const collectionStatusLabels: Record<CollectionTaskStatus, string> = {
  OPEN: 'Mở',
  IN_PROGRESS: 'Đang xử lý',
  DONE: 'Hoàn tất',
  CANCELLED: 'Đã hủy',
}

const collectionStatusPillClassNames: Record<CollectionTaskStatus, string> = {
  OPEN: 'pill pill-info',
  IN_PROGRESS: 'pill pill-warn',
  DONE: 'pill pill-ok',
  CANCELLED: 'pill pill-error',
}

type SortState = {
  key: string
  direction: 'asc' | 'desc'
}

type RowActionMode = 'assign' | 'status' | null
type RowActionDialogMode = Exclude<RowActionMode, null>
type RiskPrefillContext = {
  fromRisk: boolean
  asOfDate: string
  ownerId: string
  level: string
  minPriorityScore: string
}

const collectionColumnWidths = {
  customerName: '260px',
  ownerName: '190px',
  totalOutstanding: '140px',
  overdueAmount: '140px',
  maxDaysPastDue: '110px',
  predictedOverdueProbability: '110px',
  priorityScore: '120px',
  status: '130px',
  assignedTo: '240px',
  updatedAt: '170px',
  actionsManage: '240px',
  actionsReadonly: '120px',
} as const

const COLLECTION_TABLE_MIN_WIDTH_MANAGE = '1960px'
const COLLECTION_TABLE_MIN_WIDTH_READONLY = '1820px'

function FieldHelp({ text }: { text: string }) {
  return (
    <span className="field-help" tabIndex={0} role="note" aria-label={text} title={text}>
      ?
    </span>
  )
}

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

const getStoredValue = (key: string, fallback = '') => {
  if (typeof window === 'undefined') return fallback
  return window.localStorage.getItem(key) ?? fallback
}

const storeValue = (key: string, value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(key)
    return
  }
  window.localStorage.setItem(key, value)
}

const clampPriorityScore = (value: number) => {
  if (!Number.isFinite(value)) return 0
  if (value < 0) return 0
  if (value > 1) return 1
  return value
}

const normalizeGenerateTake = (value: number) => {
  if (!Number.isFinite(value)) return DEFAULT_GENERATE_TAKE
  return Math.min(200, Math.max(1, Math.round(value)))
}

const parseRiskDate = (value: string | null) => {
  const normalized = value?.trim() ?? ''
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : ''
}

const parseRiskOwnerId = (value: string | null) => {
  const normalized = value?.trim() ?? ''
  return normalized.length > 0 ? normalized : ''
}

const parseRiskLevel = (value: string | null) => {
  const normalized = (value ?? '').trim().toUpperCase()
  return normalized in riskLevelLabels ? normalized : ''
}

const parseRiskMinPriority = (value: string | null) => {
  const normalized = value?.trim() ?? ''
  if (!normalized) return ''
  const parsed = Number(normalized)
  if (!Number.isFinite(parsed)) return ''
  return clampPriorityScore(parsed).toFixed(2)
}

const parseRiskPrefillContext = (searchParams: URLSearchParams): RiskPrefillContext => {
  const fromRisk = searchParams.get('fromRisk') === '1'
  if (!fromRisk) {
    return {
      fromRisk: false,
      asOfDate: '',
      ownerId: '',
      level: '',
      minPriorityScore: '',
    }
  }

  const level = parseRiskLevel(searchParams.get('level'))
  const queryMinPriority = parseRiskMinPriority(searchParams.get('minPriorityScore'))
  const mappedMinPriority = level ? riskLevelToPriorityScore[level] : ''

  return {
    fromRisk: true,
    asOfDate: parseRiskDate(searchParams.get('asOfDate')),
    ownerId: parseRiskOwnerId(searchParams.get('ownerId')),
    level,
    minPriorityScore: queryMinPriority || mappedMinPriority,
  }
}

const sortCollectionTasks = (rows: CollectionTask[], sort?: SortState) => {
  if (!sort) return rows
  const sorted = [...rows]
  const direction = sort.direction === 'asc' ? 1 : -1

  const compareText = (left: string | null | undefined, right: string | null | undefined) =>
    (left ?? '').localeCompare(right ?? '', 'vi', { sensitivity: 'base' })

  const compareNumber = (left: number, right: number) => left - right

  const compareDate = (left: string, right: string) =>
    new Date(left).getTime() - new Date(right).getTime()

  sorted.sort((a, b) => {
    let value = 0
    switch (sort.key) {
      case 'customerName':
        value = compareText(a.customerName, b.customerName)
        break
      case 'ownerName':
        value = compareText(a.ownerName, b.ownerName)
        break
      case 'totalOutstanding':
        value = compareNumber(a.totalOutstanding, b.totalOutstanding)
        break
      case 'overdueAmount':
        value = compareNumber(a.overdueAmount, b.overdueAmount)
        break
      case 'maxDaysPastDue':
        value = compareNumber(a.maxDaysPastDue, b.maxDaysPastDue)
        break
      case 'predictedOverdueProbability':
        value = compareNumber(a.predictedOverdueProbability, b.predictedOverdueProbability)
        break
      case 'priorityScore':
        value = compareNumber(a.priorityScore, b.priorityScore)
        break
      case 'status':
        value = compareText(a.status, b.status)
        break
      case 'assignedTo':
        value = compareText(a.assignedTo, b.assignedTo)
        break
      case 'updatedAt':
        value = compareDate(a.updatedAt, b.updatedAt)
        break
      default:
        value = 0
        break
    }

    if (value === 0) {
      value = compareNumber(a.priorityScore, b.priorityScore)
    }

    return value * direction
  })

  return sorted
}

export default function CollectionsWorkboardPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canManage = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const [searchParams, setSearchParams] = useSearchParams()
  const riskPrefill = useMemo(() => parseRiskPrefillContext(searchParams), [searchParams])
  const appliedRiskContextKeyRef = useRef('')

  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search, 300)
  const normalizedSearchKeyword = useMemo(() => debouncedSearch.trim(), [debouncedSearch])
  const isSearchDebouncing = search.trim() !== normalizedSearchKeyword
  const [statusFilter, setStatusFilter] = useState<CollectionTaskStatus | ''>(() => {
    const stored = getStoredValue(STATUS_STORAGE_KEY)
    return stored === 'OPEN' || stored === 'IN_PROGRESS' || stored === 'DONE' || stored === 'CANCELLED'
      ? stored
      : ''
  })
  const [assignedFilter, setAssignedFilter] = useState(() => getStoredValue(ASSIGNED_STORAGE_KEY))
  const [take, setTake] = useState(() => {
    const parsed = Number(getStoredValue(TAKE_STORAGE_KEY, String(DEFAULT_TAKE)))
    return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_TAKE
  })
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [sort, setSort] = useState<SortState>()

  const [tasks, setTasks] = useState<CollectionTask[]>([])
  const [loading, setLoading] = useState(false)
  const [loadingError, setLoadingError] = useState<string | null>(null)
  const [operationMessage, setOperationMessage] = useState<string | null>(null)
  const [operationError, setOperationError] = useState<string | null>(null)
  const [rowActionTaskId, setRowActionTaskId] = useState<string | null>(null)
  const [rowActionMode, setRowActionMode] = useState<RowActionMode>(null)
  const [actionDialogTaskId, setActionDialogTaskId] = useState<string | null>(null)
  const [actionDialogMode, setActionDialogMode] = useState<RowActionMode>(null)
  const actionDialogTriggerRef = useRef<HTMLButtonElement | null>(null)
  const assignSelectRef = useRef<HTMLSelectElement | null>(null)
  const statusSelectRef = useRef<HTMLSelectElement | null>(null)
  const [reloadTick, setReloadTick] = useState(0)

  const [ownerOptions, setOwnerOptions] = useState<LookupOption[]>([])
  const [assigneeOptions, setAssigneeOptions] = useState<LookupOption[]>([])
  const [lookupsLoading, setLookupsLoading] = useState(false)

  const [generateAsOfDate, setGenerateAsOfDate] = useState(() => riskPrefill.asOfDate)
  const [generateOwnerId, setGenerateOwnerId] = useState(() => riskPrefill.ownerId)
  const [generateTake, setGenerateTake] = useState(String(DEFAULT_GENERATE_TAKE))
  const [minPriorityScore, setMinPriorityScore] = useState(
    () => riskPrefill.minPriorityScore || DEFAULT_MIN_PRIORITY_SCORE,
  )
  const [generateLoading, setGenerateLoading] = useState(false)
  const [generateInfo, setGenerateInfo] = useState<string | null>(null)
  const [generateError, setGenerateError] = useState<string | null>(null)

  const [assignDraft, setAssignDraft] = useState<Record<string, string>>({})
  const [statusDraft, setStatusDraft] = useState<Record<string, CollectionTaskStatus>>({})
  const [noteDraft, setNoteDraft] = useState<Record<string, string>>({})

  const assigneeById = useMemo(() => {
    return assigneeOptions.reduce<Record<string, string>>((acc, option) => {
      acc[option.value] = option.label
      return acc
    }, {})
  }, [assigneeOptions])

  const triggerReload = useCallback(() => {
    setReloadTick((prev) => prev + 1)
  }, [])

  const updateSearchParams = useCallback(
    (updater: (next: URLSearchParams) => void) => {
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current)
          updater(next)
          return next
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  const clearRiskContext = useCallback(() => {
    updateSearchParams((next) => {
      RISK_QUERY_KEYS.forEach((key) => next.delete(key))
    })
  }, [updateSearchParams])

  const riskContextSummary = useMemo(() => {
    if (!riskPrefill.fromRisk) return ''

    const details: string[] = []
    if (riskPrefill.asOfDate) {
      details.push(`Ngày chốt ${riskPrefill.asOfDate}`)
    }

    if (riskPrefill.ownerId) {
      const ownerLabel =
        ownerOptions.find((option) => option.value === riskPrefill.ownerId)?.label ??
        riskPrefill.ownerId
      details.push(`Phụ trách ${ownerLabel}`)
    }

    if (riskPrefill.level) {
      details.push(`Nhóm ${riskLevelLabels[riskPrefill.level] ?? riskPrefill.level}`)
    }

    if (riskPrefill.minPriorityScore) {
      details.push(`Ngưỡng ưu tiên ${riskPrefill.minPriorityScore}`)
    }

    return details.join(' • ')
  }, [ownerOptions, riskPrefill])

  useEffect(() => {
    if (!riskPrefill.fromRisk) return

    const contextKey = `${riskPrefill.asOfDate}|${riskPrefill.ownerId}|${riskPrefill.level}|${riskPrefill.minPriorityScore}`
    if (appliedRiskContextKeyRef.current === contextKey) return

    setGenerateAsOfDate(riskPrefill.asOfDate)
    setGenerateOwnerId(riskPrefill.ownerId)
    if (riskPrefill.minPriorityScore) {
      setMinPriorityScore(riskPrefill.minPriorityScore)
    }

    appliedRiskContextKeyRef.current = contextKey
  }, [riskPrefill])

  useEffect(() => {
    if (!token) {
      setOwnerOptions([])
      setAssigneeOptions([])
      return
    }

    let isActive = true
    setLookupsLoading(true)

    const loadLookups = async () => {
      try {
        const [owners, users] = await Promise.all([
          fetchOwnerLookup({ token, limit: 200 }),
          fetchUserLookup({ token, limit: 200 }),
        ])
        if (!isActive) return

        setOwnerOptions(mapOwnerOptions(owners))
        setAssigneeOptions(
          users.map((item) => ({
            value: item.id,
            label: item.name ? `${item.name} (${item.username})` : item.username,
          })),
        )
      } catch {
        if (!isActive) return
        setOwnerOptions([])
        setAssigneeOptions([])
      } finally {
        if (isActive) {
          setLookupsLoading(false)
        }
      }
    }

    void loadLookups()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) {
      setTasks([])
      setLoading(false)
      setLoadingError(null)
      return
    }

    let isActive = true
    setLoading(true)
    setLoadingError(null)

    const loadTasks = async () => {
      try {
        const rows = await listCollectionTasks({
          token,
          status: statusFilter || undefined,
          assignedTo: assignedFilter || undefined,
          search: normalizedSearchKeyword || undefined,
          take,
        })

        if (!isActive) return
        setTasks(rows)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setLoadingError(err.message)
        } else {
          setLoadingError('Không tải được danh sách thu hồi công nợ.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    void loadTasks()
    return () => {
      isActive = false
    }
  }, [token, statusFilter, assignedFilter, normalizedSearchKeyword, take, reloadTick])

  useEffect(() => {
    setAssignDraft((previous) => {
      const next: Record<string, string> = {}
      tasks.forEach((task) => {
        next[task.taskId] = task.assignedTo ?? ''
      })
      return { ...next, ...previous }
    })

    setStatusDraft((previous) => {
      const next: Record<string, CollectionTaskStatus> = {}
      tasks.forEach((task) => {
        next[task.taskId] = task.status
      })
      return { ...next, ...previous }
    })

    setNoteDraft((previous) => {
      const next: Record<string, string> = {}
      tasks.forEach((task) => {
        next[task.taskId] = task.note ?? ''
      })
      return { ...next, ...previous }
    })
  }, [tasks])

  useEffect(() => {
    storeValue(STATUS_STORAGE_KEY, statusFilter)
  }, [statusFilter])

  useEffect(() => {
    storeValue(ASSIGNED_STORAGE_KEY, assignedFilter)
  }, [assignedFilter])

  useEffect(() => {
    storeValue(TAKE_STORAGE_KEY, String(take))
  }, [take])

  const sortedTasks = useMemo(() => sortCollectionTasks(tasks, sort), [tasks, sort])
  const total = sortedTasks.length
  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages)
    }
  }, [page, totalPages])

  const pagedTasks = useMemo(() => {
    const start = (page - 1) * pageSize
    return sortedTasks.slice(start, start + pageSize)
  }, [page, pageSize, sortedTasks])

  const summary = useMemo(() => {
    return tasks.reduce(
      (acc, item) => {
        acc.totalOutstanding += item.totalOutstanding
        acc.totalOverdue += item.overdueAmount
        acc[item.status] += 1
        return acc
      },
      {
        totalOutstanding: 0,
        totalOverdue: 0,
        OPEN: 0,
        IN_PROGRESS: 0,
        DONE: 0,
        CANCELLED: 0,
      },
    )
  }, [tasks])

  const handleAssign = useCallback(
    async (task: CollectionTask) => {
      if (!token || !canManage) return

      setRowActionTaskId(task.taskId)
      setRowActionMode('assign')
      setOperationError(null)
      setOperationMessage(null)
      try {
        const nextAssignee = (assignDraft[task.taskId] ?? '').trim()
        await assignCollectionTask(token, task.taskId, {
          assignedTo: nextAssignee || null,
        })
        setOperationMessage('Đã cập nhật người phụ trách.')
        setActionDialogTaskId(null)
        setActionDialogMode(null)
        const trigger = actionDialogTriggerRef.current
        actionDialogTriggerRef.current = null
        if (trigger && typeof window !== 'undefined') {
          window.requestAnimationFrame(() => {
            trigger.focus()
          })
        }
        triggerReload()
      } catch (err) {
        if (err instanceof ApiError) {
          setOperationError(err.message)
        } else {
          setOperationError('Không thể cập nhật người phụ trách.')
        }
      } finally {
        setRowActionTaskId(null)
        setRowActionMode(null)
      }
    },
    [assignDraft, canManage, token, triggerReload],
  )

  const handleUpdateStatus = useCallback(
    async (task: CollectionTask) => {
      if (!token || !canManage) return

      setRowActionTaskId(task.taskId)
      setRowActionMode('status')
      setOperationError(null)
      setOperationMessage(null)
      try {
        const nextStatus = statusDraft[task.taskId] ?? task.status
        const nextNote = (noteDraft[task.taskId] ?? '').trim()
        await updateCollectionTaskStatus(token, task.taskId, {
          status: nextStatus,
          note: nextNote || null,
        })
        setOperationMessage('Đã cập nhật trạng thái thu hồi.')
        setActionDialogTaskId(null)
        setActionDialogMode(null)
        const trigger = actionDialogTriggerRef.current
        actionDialogTriggerRef.current = null
        if (trigger && typeof window !== 'undefined') {
          window.requestAnimationFrame(() => {
            trigger.focus()
          })
        }
        triggerReload()
      } catch (err) {
        if (err instanceof ApiError) {
          setOperationError(err.message)
        } else {
          setOperationError('Không thể cập nhật trạng thái.')
        }
      } finally {
        setRowActionTaskId(null)
        setRowActionMode(null)
      }
    },
    [canManage, noteDraft, statusDraft, token, triggerReload],
  )

  const executeGenerateQueue = useCallback(
    async (params?: {
      asOfDate?: string
      ownerId?: string
      take?: number
      minPriorityScore?: number
    }) => {
      if (!token || !canManage) return false

      setGenerateLoading(true)
      setGenerateError(null)
      setGenerateInfo(null)
      setOperationError(null)
      setOperationMessage(null)

      const parsedTake = normalizeGenerateTake(params?.take ?? Number(generateTake))
      const parsedMinPriorityScore = clampPriorityScore(
        params?.minPriorityScore ?? Number(minPriorityScore),
      )

      try {
        const result = await generateCollectionTasks(token, {
          asOfDate: (params?.asOfDate ?? generateAsOfDate) || undefined,
          ownerId: (params?.ownerId ?? generateOwnerId) || undefined,
          take: parsedTake,
          minPriorityScore: parsedMinPriorityScore,
        })
        const summary = `Tạo mới ${result.created} task trên ${result.candidates} khách hàng rủi ro. Ngưỡng ưu tiên: ${result.minPriorityScore.toFixed(2)}.`
        const detail =
          result.created > 0
            ? ''
            : result.candidates === 0
              ? ' Không có khách hàng rủi ro phù hợp với bộ lọc hiện tại.'
              : ' Không có task mới được thêm. Thường do khách hàng đã có task Mở/Đang xử lý hoặc chưa đạt ngưỡng ưu tiên.'
        setGenerateInfo(`${summary}${detail}`)
        setPage(1)
        triggerReload()
        return true
      } catch (err) {
        if (err instanceof ApiError) {
          setGenerateError(err.message)
        } else {
          setGenerateError('Không thể tạo queue thu hồi từ dữ liệu rủi ro.')
        }
        return false
      } finally {
        setGenerateLoading(false)
      }
    },
    [
      canManage,
      generateAsOfDate,
      generateOwnerId,
      generateTake,
      minPriorityScore,
      token,
      triggerReload,
    ],
  )

  const handleGenerateQueue = useCallback(async () => {
    await executeGenerateQueue()
  }, [executeGenerateQueue])

  const handleGenerateFromRiskContext = useCallback(async () => {
    if (!riskPrefill.fromRisk) return

    const didCreate = await executeGenerateQueue({
      asOfDate: riskPrefill.asOfDate || undefined,
      ownerId: riskPrefill.ownerId || undefined,
      take: normalizeGenerateTake(Number(generateTake)),
      minPriorityScore: clampPriorityScore(
        Number(riskPrefill.minPriorityScore || minPriorityScore),
      ),
    })

    if (didCreate) {
      clearRiskContext()
    }
  }, [
    clearRiskContext,
    executeGenerateQueue,
    generateTake,
    minPriorityScore,
    riskPrefill,
  ])

  const closeActionDialog = useCallback(() => {
    setActionDialogTaskId(null)
    setActionDialogMode(null)
    const trigger = actionDialogTriggerRef.current
    actionDialogTriggerRef.current = null
    if (trigger && typeof window !== 'undefined') {
      window.requestAnimationFrame(() => {
        trigger.focus()
      })
    }
  }, [])

  const openActionDialog = useCallback(
    (mode: RowActionDialogMode, taskId: string, trigger?: HTMLButtonElement | null) => {
      actionDialogTriggerRef.current = trigger ?? null
      setOperationError(null)
      setOperationMessage(null)
      setActionDialogTaskId(taskId)
      setActionDialogMode(mode)
    },
    [],
  )

  useEffect(() => {
    if (!actionDialogMode || typeof window === 'undefined') return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      closeActionDialog()
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => {
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [actionDialogMode, closeActionDialog])

  useEffect(() => {
    if (!actionDialogMode || !actionDialogTaskId || typeof window === 'undefined') return

    const rafId = window.requestAnimationFrame(() => {
      if (actionDialogMode === 'assign') {
        assignSelectRef.current?.focus()
      } else {
        statusSelectRef.current?.focus()
      }
    })

    return () => {
      window.cancelAnimationFrame(rafId)
    }
  }, [actionDialogMode, actionDialogTaskId])

  useEffect(() => {
    if (!actionDialogTaskId) return
    const exists = tasks.some((item) => item.taskId === actionDialogTaskId)
    if (exists) return

    setActionDialogTaskId(null)
    setActionDialogMode(null)
  }, [actionDialogTaskId, tasks])

  const columns = useMemo(
    () => [
      {
        key: 'customerName',
        label: 'Khách hàng',
        sortable: true,
        width: collectionColumnWidths.customerName,
        render: (row: CollectionTask) => (
          <div className="stacked-text">
            <strong>{row.customerName}</strong>
            <span className="muted">{row.customerTaxCode}</span>
          </div>
        ),
      },
      {
        key: 'ownerName',
        label: 'Chủ tài khoản',
        sortable: true,
        width: collectionColumnWidths.ownerName,
        render: (row: CollectionTask) => row.ownerName ?? '-',
      },
      {
        key: 'totalOutstanding',
        label: 'Dư nợ',
        align: 'right' as const,
        sortable: true,
        width: collectionColumnWidths.totalOutstanding,
        render: (row: CollectionTask) => formatMoney(row.totalOutstanding),
      },
      {
        key: 'overdueAmount',
        label: 'Quá hạn',
        align: 'right' as const,
        sortable: true,
        width: collectionColumnWidths.overdueAmount,
        render: (row: CollectionTask) => formatMoney(row.overdueAmount),
      },
      {
        key: 'maxDaysPastDue',
        label: 'DPD tối đa',
        align: 'right' as const,
        sortable: true,
        width: collectionColumnWidths.maxDaysPastDue,
      },
      {
        key: 'predictedOverdueProbability',
        label: 'XS quá hạn',
        align: 'right' as const,
        sortable: true,
        width: collectionColumnWidths.predictedOverdueProbability,
        render: (row: CollectionTask) => `${Math.round(row.predictedOverdueProbability * 100)}%`,
      },
      {
        key: 'priorityScore',
        label: 'Điểm ưu tiên',
        align: 'right' as const,
        sortable: true,
        width: collectionColumnWidths.priorityScore,
      },
      {
        key: 'status',
        label: 'Trạng thái',
        sortable: true,
        width: collectionColumnWidths.status,
        render: (row: CollectionTask) => collectionStatusLabels[row.status],
      },
      {
        key: 'assignedTo',
        label: 'Phụ trách',
        sortable: true,
        width: collectionColumnWidths.assignedTo,
        render: (row: CollectionTask) => {
          if (!row.assignedTo) return <span className="muted">Chưa giao</span>
          return assigneeById[row.assignedTo] ?? row.assignedTo
        },
      },
      {
        key: 'updatedAt',
        label: 'Cập nhật',
        sortable: true,
        width: collectionColumnWidths.updatedAt,
        render: (row: CollectionTask) => formatDateTime(row.updatedAt),
      },
      {
        key: 'actions',
        label: 'Xử lý',
        width: canManage
          ? collectionColumnWidths.actionsManage
          : collectionColumnWidths.actionsReadonly,
        render: (row: CollectionTask) => {
          if (!canManage) return <span className="muted">Chỉ xem</span>

          const isAssignLoading = rowActionTaskId === row.taskId && rowActionMode === 'assign'
          const isStatusLoading = rowActionTaskId === row.taskId && rowActionMode === 'status'
          const isRowBusy = rowActionTaskId === row.taskId
          const assigneeStateLabel = row.assignedTo ? 'Đã giao' : 'Chưa giao'
          const statusPillClassName = collectionStatusPillClassNames[row.status]

          return (
            <div className="stacked-text">
              <div className="chip-row">
                <span className={row.assignedTo ? 'pill pill-info' : 'pill pill-warn'}>
                  {assigneeStateLabel}
                </span>
                <span className={statusPillClassName}>{collectionStatusLabels[row.status]}</span>
              </div>
              <div className="inline-actions inline-actions--tight">
                <button
                  className="btn btn-outline btn-table"
                  type="button"
                  onClick={(event) => openActionDialog('assign', row.taskId, event.currentTarget)}
                  disabled={isRowBusy}
                  aria-label={`Mở popup giao việc ${row.customerTaxCode}`}
                  title="Mở popup giao người phụ trách"
                >
                  {isAssignLoading ? 'Đang lưu...' : 'Giao'}
                </button>
                <button
                  className="btn btn-primary btn-table"
                  type="button"
                  onClick={(event) => openActionDialog('status', row.taskId, event.currentTarget)}
                  disabled={isRowBusy}
                  aria-label={`Mở popup cập nhật ${row.customerTaxCode}`}
                  title="Mở popup cập nhật trạng thái và ghi chú"
                >
                  {isStatusLoading ? 'Đang lưu...' : 'Cập nhật'}
                </button>
              </div>
            </div>
          )
        },
      },
    ],
    [
      assigneeById,
      canManage,
      openActionDialog,
      rowActionMode,
      rowActionTaskId,
    ],
  )

  const actionDialogTask = useMemo(
    () => tasks.find((task) => task.taskId === actionDialogTaskId) ?? null,
    [actionDialogTaskId, tasks],
  )
  const actionDialogAssignValue = actionDialogTask
    ? assignDraft[actionDialogTask.taskId] ?? actionDialogTask.assignedTo ?? ''
    : ''
  const actionDialogStatusValue = actionDialogTask
    ? statusDraft[actionDialogTask.taskId] ?? actionDialogTask.status
    : 'OPEN'
  const actionDialogNoteValue = actionDialogTask
    ? noteDraft[actionDialogTask.taskId] ?? actionDialogTask.note ?? ''
    : ''
  const isDialogAssignLoading =
    actionDialogMode === 'assign' &&
    actionDialogTaskId !== null &&
    rowActionTaskId === actionDialogTaskId &&
    rowActionMode === 'assign'
  const isDialogStatusLoading =
    actionDialogMode === 'status' &&
    actionDialogTaskId !== null &&
    rowActionTaskId === actionDialogTaskId &&
    rowActionMode === 'status'
  const isDialogBusy = actionDialogTaskId !== null && rowActionTaskId === actionDialogTaskId

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Workboard thu hồi công nợ</h2>
          <p className="muted">
            Tổng hợp queue ưu tiên, giao người phụ trách và cập nhật kết quả xử lý tại một nơi.
          </p>
        </div>
      </div>

      <section className="card guide-card">
        <div className="card-row">
          <div>
            <h3>Cách vận hành trang này</h3>
            <p className="muted">
              Mỗi vòng xử lý thường đi theo 3 bước: tạo queue từ rủi ro, phân công người xử lý,
              rồi cập nhật trạng thái cho từng task.
            </p>
          </div>
        </div>
        <ol className="guide-steps">
          <li>Lọc danh sách theo trạng thái/người phụ trách để tập trung đúng nhóm cần làm.</li>
          {canManage ? (
            <li>
              Vào khối <strong>Tạo queue từ cảnh báo rủi ro</strong> để sinh thêm task mới khi
              cần.
            </li>
          ) : (
            <li>
              Theo dõi danh sách task được giao và tập trung xử lý theo mức ưu tiên hiện có.
            </li>
          )}
          <li>
            Ở cột <strong>Xử lý</strong>, bấm <strong>Giao</strong> hoặc{' '}
            <strong>Cập nhật</strong> để mở popup thao tác nhanh cho từng task.
          </li>
        </ol>
      </section>

      <section className="card">
        <div className="filters-grid">
          <label className="field">
            <span>Từ khóa</span>
            <input
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
              placeholder="MST, tên khách hàng..."
            />
            <span className="field-hint muted">
              {isSearchDebouncing
                ? 'Đang lọc theo từ khóa...'
                : 'Lọc theo MST/tên trong queue hiện có.'}
            </span>
          </label>

          <label className="field">
            <span>Trạng thái</span>
            <select
              value={statusFilter}
              onChange={(event) => {
                const next = event.target.value as CollectionTaskStatus | ''
                setStatusFilter(next)
                setPage(1)
              }}
            >
              <option value="">Tất cả</option>
              <option value="OPEN">Mở</option>
              <option value="IN_PROGRESS">Đang xử lý</option>
              <option value="DONE">Hoàn tất</option>
              <option value="CANCELLED">Đã hủy</option>
            </select>
          </label>

          <label className="field">
            <span>Người phụ trách</span>
            <select
              value={assignedFilter}
              onChange={(event) => {
                setAssignedFilter(event.target.value)
                setPage(1)
              }}
              disabled={lookupsLoading}
            >
              <option value="">Tất cả</option>
              {assigneeOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span className="field-label">
              Số task tải về
              <FieldHelp text="Giới hạn số task API tải về trước khi phân trang trên màn hình." />
            </span>
            <select
              value={take}
              onChange={(event) => {
                setTake(Number(event.target.value))
                setPage(1)
              }}
            >
              <option value={50}>50</option>
              <option value={100}>100</option>
              <option value={200}>200</option>
              <option value={500}>500</option>
            </select>
          </label>
        </div>

        <div className="inline-actions" style={{ marginTop: 12 }}>
          <button className="btn btn-primary" type="button" onClick={triggerReload}>
            Làm mới
          </button>
          <button
            className="btn btn-ghost"
            type="button"
            onClick={() => {
              setSearch('')
              setStatusFilter('')
              setAssignedFilter('')
              setTake(DEFAULT_TAKE)
              setSort(undefined)
              setPage(1)
            }}
          >
            Xóa bộ lọc
          </button>
          <span className="muted">Tổng {tasks.length} task hiển thị.</span>
        </div>
      </section>

      {canManage && (
        <section className="card">
          <div className="card-row">
            <div>
              <h3>Tạo queue từ cảnh báo rủi ro</h3>
              <p className="muted">
                Hệ thống lấy dữ liệu từ mô hình rủi ro và tự tạo danh sách ưu tiên thu hồi.
              </p>
            </div>
          </div>

          {riskPrefill.fromRisk && (
            <div className="context-banner" role="status">
              <div>
                <p className="context-banner__title">Đang dùng ngữ cảnh từ Cảnh báo rủi ro</p>
                <p className="muted">
                  {riskContextSummary || 'Bạn có thể dùng nhanh bộ lọc đã chọn ở trang Risk Alerts.'}
                </p>
              </div>
              <div className="inline-actions inline-actions--tight">
                <button
                  className="btn btn-primary"
                  type="button"
                  onClick={() => void handleGenerateFromRiskContext()}
                  disabled={generateLoading}
                >
                  {generateLoading ? 'Đang tạo queue...' : 'Áp dụng và tạo queue ngay'}
                </button>
                <button
                  className="btn btn-ghost"
                  type="button"
                  onClick={clearRiskContext}
                  disabled={generateLoading}
                >
                  Bỏ ngữ cảnh
                </button>
              </div>
            </div>
          )}

          <div className="filters-grid">
            <label className="field">
              <span>Tính đến ngày</span>
              <input
                type="date"
                value={generateAsOfDate}
                onChange={(event) => setGenerateAsOfDate(event.target.value)}
              />
            </label>

            <label className="field">
              <span>Chủ tài khoản</span>
              <select
                value={generateOwnerId}
                onChange={(event) => setGenerateOwnerId(event.target.value)}
                disabled={lookupsLoading}
              >
                <option value="">Tất cả</option>
                {ownerOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span className="field-label">
                Số task cần tạo
                <FieldHelp text="Số khách hàng tối đa được đưa vào queue trong một lần tạo." />
              </span>
              <input
                type="number"
                min={1}
                max={200}
                value={generateTake}
                onChange={(event) => setGenerateTake(event.target.value)}
              />
            </label>

            <label className="field">
              <span className="field-label">
                Ngưỡng ưu tiên (0-1)
                <FieldHelp text="Task chỉ được tạo khi điểm ưu tiên lớn hơn hoặc bằng ngưỡng này." />
              </span>
              <input
                type="number"
                min={0}
                max={1}
                step="0.01"
                value={minPriorityScore}
                onChange={(event) => setMinPriorityScore(event.target.value)}
              />
            </label>
          </div>

          <div className="inline-actions" style={{ marginTop: 12 }}>
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => void handleGenerateQueue()}
              disabled={generateLoading}
            >
              {generateLoading ? 'Đang tạo queue...' : 'Tạo queue'}
            </button>
          </div>

          {generateInfo && <div className="alert alert--success">{generateInfo}</div>}
          {generateError && (
            <div className="alert alert--error" role="alert">
              {generateError}
            </div>
          )}
        </section>
      )}

      <section className="card">
        <div className="stat-grid">
          <div className="stat-card">
            <p className="stat-card__label">Tổng dư nợ</p>
            <h3>{formatMoney(summary.totalOutstanding)}</h3>
            <span className="stat-card__meta">Quá hạn {formatMoney(summary.totalOverdue)}</span>
          </div>
          <div className="stat-card">
            <p className="stat-card__label">Mở</p>
            <h3>{summary.OPEN}</h3>
            <span className="stat-card__meta">Task chưa bắt đầu</span>
          </div>
          <div className="stat-card">
            <p className="stat-card__label">Đang xử lý</p>
            <h3>{summary.IN_PROGRESS}</h3>
            <span className="stat-card__meta">Task đang theo dõi</span>
          </div>
          <div className="stat-card">
            <p className="stat-card__label">Hoàn tất / Hủy</p>
            <h3>
              {summary.DONE} / {summary.CANCELLED}
            </h3>
            <span className="stat-card__meta">Task đã đóng</span>
          </div>
        </div>
      </section>

      {operationMessage && <div className="alert alert--success">{operationMessage}</div>}
      {operationError && (
        <div className="alert alert--error" role="alert">
          {operationError}
        </div>
      )}
      {loadingError && (
        <div className="alert alert--error" role="alert">
          {loadingError}
        </div>
      )}

      <section className="card">
        <DataTable
          columns={columns}
          rows={pagedTasks}
          getRowKey={(row) => row.taskId}
          minWidth={canManage ? COLLECTION_TABLE_MIN_WIDTH_MANAGE : COLLECTION_TABLE_MIN_WIDTH_READONLY}
          showScrollHint={false}
          sort={sort}
          onSort={setSort}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(value) => {
            setPageSize(value)
            setPage(1)
            storePageSize(value)
          }}
          emptyMessage={
            loading
              ? 'Đang tải queue...'
              : normalizedSearchKeyword
                ? `Không tìm thấy task nào khớp từ khóa "${normalizedSearchKeyword}".`
                : 'Chưa có task thu hồi nào.'
          }
        />
      </section>

      {actionDialogTask && actionDialogMode && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng popup thao tác"
            onClick={closeActionDialog}
            disabled={isDialogBusy}
          />
          <div
            className="modal modal--narrow"
            role="dialog"
            aria-modal="true"
            aria-labelledby="collection-action-dialog-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="collection-action-dialog-title">
                  {actionDialogMode === 'assign'
                    ? 'Giao người phụ trách'
                    : 'Cập nhật trạng thái thu hồi'}
                </h3>
                <p className="muted">
                  {actionDialogTask.customerName} ({actionDialogTask.customerTaxCode})
                </p>
              </div>
              <button
                className="btn btn-ghost btn-table"
                type="button"
                onClick={closeActionDialog}
                disabled={isDialogBusy}
              >
                Đóng
              </button>
            </div>
            <div className="modal-body">
              {actionDialogMode === 'assign' ? (
                <>
                  <label className="field">
                    <span>Người phụ trách</span>
                    <select
                      ref={assignSelectRef}
                      value={actionDialogAssignValue}
                      onChange={(event) =>
                        setAssignDraft((prev) => ({
                          ...prev,
                          [actionDialogTask.taskId]: event.target.value,
                        }))
                      }
                      disabled={isDialogBusy}
                    >
                      <option value="">Chưa giao</option>
                      {assigneeOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </label>
                  <p className="muted">
                    Có thể để trống để bỏ phân công, sau đó bấm <strong>Lưu phân công</strong>.
                  </p>
                </>
              ) : (
                <>
                  <label className="field">
                    <span>Trạng thái</span>
                    <select
                      ref={statusSelectRef}
                      value={actionDialogStatusValue}
                      onChange={(event) =>
                        setStatusDraft((prev) => ({
                          ...prev,
                          [actionDialogTask.taskId]: event.target.value as CollectionTaskStatus,
                        }))
                      }
                      disabled={isDialogBusy}
                    >
                      <option value="OPEN">Mở</option>
                      <option value="IN_PROGRESS">Đang xử lý</option>
                      <option value="DONE">Hoàn tất</option>
                      <option value="CANCELLED">Đã hủy</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>Ghi chú xử lý</span>
                    <input
                      value={actionDialogNoteValue}
                      onChange={(event) =>
                        setNoteDraft((prev) => ({
                          ...prev,
                          [actionDialogTask.taskId]: event.target.value,
                        }))
                      }
                      placeholder="Ví dụ: Khách hẹn thanh toán ngày mai"
                      disabled={isDialogBusy}
                    />
                  </label>
                </>
              )}
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                className="btn btn-ghost"
                type="button"
                onClick={closeActionDialog}
                disabled={isDialogBusy}
              >
                Hủy
              </button>
              <button
                className={actionDialogMode === 'assign' ? 'btn btn-outline' : 'btn btn-primary'}
                type="button"
                onClick={() => {
                  if (actionDialogMode === 'assign') {
                    void handleAssign(actionDialogTask)
                    return
                  }
                  void handleUpdateStatus(actionDialogTask)
                }}
                disabled={isDialogBusy}
              >
                {actionDialogMode === 'assign'
                  ? isDialogAssignLoading
                    ? 'Đang lưu...'
                    : 'Lưu phân công'
                  : isDialogStatusLoading
                    ? 'Đang lưu...'
                    : 'Lưu trạng thái'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
