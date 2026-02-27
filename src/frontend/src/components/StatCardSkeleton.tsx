import Skeleton from './Skeleton'

type StatCardSkeletonProps = {
  count?: number
  className?: string
}

export default function StatCardSkeleton({
  count = 4,
  className = '',
}: StatCardSkeletonProps) {
  return (
    <div className={`stat-grid${className ? ` ${className}` : ''}`} aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <div className="stat-card" key={index}>
          <Skeleton width="62%" height="0.8rem" />
          <Skeleton width="46%" height="1.45rem" />
          <Skeleton width="58%" height="0.72rem" />
        </div>
      ))}
    </div>
  )
}
