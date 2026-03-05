import { apiFetch } from './client'

export type CollectionTaskStatus = 'OPEN' | 'IN_PROGRESS' | 'DONE' | 'CANCELLED'

export type CollectionTask = {
  taskId: string
  customerTaxCode: string
  customerName: string
  ownerId?: string | null
  ownerName?: string | null
  totalOutstanding: number
  overdueAmount: number
  maxDaysPastDue: number
  predictedOverdueProbability: number
  riskLevel: string
  aiSignal: string
  priorityScore: number
  status: CollectionTaskStatus
  assignedTo?: string | null
  note?: string | null
  createdAt: string
  updatedAt: string
  completedAt?: string | null
}

export type CollectionTaskListParams = {
  token: string
  status?: CollectionTaskStatus
  assignedTo?: string
  search?: string
  take?: number
}

export type GenerateCollectionTasksRequest = {
  asOfDate?: string
  ownerId?: string
  take?: number
  minPriorityScore?: number
}

export type GenerateCollectionTasksResult = {
  created: number
  candidates: number
  minPriorityScore: number
  tasks: CollectionTask[]
}

export const listCollectionTasks = async (params: CollectionTaskListParams) => {
  const query = new URLSearchParams({
    take: String(params.take ?? 200),
  })
  if (params.status) query.append('status', params.status)
  if (params.assignedTo) query.append('assignedTo', params.assignedTo)
  if (params.search) query.append('search', params.search)

  return apiFetch<CollectionTask[]>(`/collections/tasks?${query.toString()}`, {
    token: params.token,
  })
}

export const generateCollectionTasks = async (
  token: string,
  payload: GenerateCollectionTasksRequest,
) => {
  return apiFetch<GenerateCollectionTasksResult>('/collections/tasks/generate', {
    method: 'POST',
    token,
    body: {
      as_of_date: payload.asOfDate ?? null,
      owner_id: payload.ownerId ?? null,
      take: payload.take ?? null,
      min_priority_score: payload.minPriorityScore ?? null,
    },
  })
}

export const assignCollectionTask = async (
  token: string,
  taskId: string,
  payload: { assignedTo?: string | null },
) => {
  return apiFetch<CollectionTask>(`/collections/tasks/${taskId}/assign`, {
    method: 'POST',
    token,
    body: {
      assigned_to: payload.assignedTo ?? null,
    },
  })
}

export const updateCollectionTaskStatus = async (
  token: string,
  taskId: string,
  payload: { status: CollectionTaskStatus; note?: string | null },
) => {
  return apiFetch<CollectionTask>(`/collections/tasks/${taskId}/status`, {
    method: 'POST',
    token,
    body: {
      status: payload.status,
      note: payload.note ?? null,
    },
  })
}
