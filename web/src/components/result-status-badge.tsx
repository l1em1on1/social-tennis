import { Badge, type BadgeTone } from "@/components/ui/badge";

/**
 * Where a Game's result stands (CONTEXT: Game, Score, Retirement, Walkover).
 * `played` and `retirement` are played results; `walkover` and `void` are
 * Manager resolutions of an unplayed Game; `pending` means no Score yet.
 */
export type ResultStatus = "played" | "retirement" | "walkover" | "void" | "pending";

const statuses: Record<ResultStatus, { label: string; tone: BadgeTone }> = {
  played: { label: "Played", tone: "success" },
  retirement: { label: "Retirement", tone: "accent" },
  walkover: { label: "Walkover", tone: "info" },
  void: { label: "Void", tone: "neutral" },
  pending: { label: "Score needed", tone: "warning" },
};

export function ResultStatusBadge({ status }: { status: ResultStatus }) {
  const { label, tone } = statuses[status];
  return <Badge tone={tone}>{label}</Badge>;
}

export const resultStatuses = Object.keys(statuses) as ResultStatus[];
