import { Skeleton, SkeletonGroup } from "@/components/ui/states";

/** Streaming fallback for every signed-in page: the standard Skeleton pattern. */
export default function Loading() {
  return (
    <SkeletonGroup>
      <Skeleton className="h-8 w-32" />
      <Skeleton className="h-32" />
      <Skeleton className="h-20" />
    </SkeletonGroup>
  );
}
