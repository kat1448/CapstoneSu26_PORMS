import type { ReactNode } from "react";

type BadgeTone = "danger" | "info" | "muted" | "success" | "warning";

type BadgeProps = {
  children: ReactNode;
  tone: BadgeTone;
};

export function Badge({ children, tone }: BadgeProps) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}
