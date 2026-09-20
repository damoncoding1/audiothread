interface SkeletonProps {
  className?: string;
}

export function Skeleton({ className = 'h-4 w-full' }: SkeletonProps) {
  return <span aria-hidden="true" className={`bg-sunken block animate-pulse rounded-md ${className}`} />;
}

export function AudioCardSkeleton() {
  return (
    <li className="bg-raised shadow-panel rounded-[--radius-panel] p-5">
      <Skeleton className="h-3 w-24" />
      <Skeleton className="mt-4 h-5 w-3/4" />
      <Skeleton className="mt-2 h-5 w-1/2" />
      <Skeleton className="mt-5 h-9 w-full" />
    </li>
  );
}
