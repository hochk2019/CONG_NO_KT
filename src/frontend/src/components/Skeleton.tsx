type SkeletonProps = {
  width?: string
  height?: string
  borderRadius?: string
  count?: number
  className?: string
}

export default function Skeleton({
  width = '100%',
  height = '1rem',
  borderRadius = '8px',
  count = 1,
  className = '',
}: SkeletonProps) {
  return (
    <>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          className={`skeleton ${className}`.trim()}
          style={{ width, height, borderRadius }}
          aria-hidden="true"
        />
      ))}
    </>
  )
}
