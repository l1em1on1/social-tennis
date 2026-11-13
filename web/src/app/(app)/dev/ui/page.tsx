import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ResultStatusBadge, resultStatuses } from "@/components/result-status-badge";
import { Badge, type BadgeTone } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { Sheet } from "@/components/ui/sheet";
import { EmptyState, ErrorState, Skeleton, SkeletonGroup } from "@/components/ui/states";
import { devSurfacesEnabled } from "@/lib/mock-mode";

export const metadata: Metadata = { title: "UI showcase" };

const tones: BadgeTone[] = ["neutral", "accent", "success", "warning", "danger", "info"];

/**
 * Every primitive and status badge on one page, in whichever theme the device
 * uses. Dev-only: in a production build without mocks this is a 404. The E2E
 * suite runs axe over it in both colour schemes, which is what keeps the
 * token pairings accessible.
 */
export default function UiShowcasePage() {
  if (!devSurfacesEnabled) {
    notFound();
  }

  return (
    <>
      <h1 className="text-2xl font-bold">UI showcase</h1>

      <Card title="Buttons">
        <div className="flex flex-wrap gap-2">
          <Button>Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Danger</Button>
          <Button disabled>Disabled</Button>
          <ButtonLink href="/" variant="secondary">
            Link
          </ButtonLink>
        </div>
      </Card>

      <Card title="Fields">
        <div className="flex flex-col gap-4">
          <Field id="demo-name" label="Name" hint="As it appears on the ladder.">
            {(control) => <Input {...control} placeholder="Anna Smith" />}
          </Field>
          <Field id="demo-email" label="Email" error="Enter a valid email address.">
            {(control) => <Input {...control} type="email" defaultValue="anna@" />}
          </Field>
          <Field id="demo-format" label="Score format">
            {(control) => (
              <Select {...control} defaultValue="bo3">
                <option value="one">One set</option>
                <option value="bo3">Best of three</option>
              </Select>
            )}
          </Field>
        </div>
      </Card>

      <Card title="Badges">
        <div className="flex flex-wrap gap-2">
          {tones.map((tone) => (
            <Badge key={tone} tone={tone}>
              {tone}
            </Badge>
          ))}
        </div>
      </Card>

      <Card title="Result status" aside={<ResultStatusBadge status="pending" />}>
        <div className="flex flex-wrap gap-2">
          {resultStatuses.map((status) => (
            <ResultStatusBadge key={status} status={status} />
          ))}
        </div>
      </Card>

      <Card title="Sheet">
        <Sheet title="Example sheet" trigger="Open sheet">
          <p className="text-fg-muted">Sheets hold short choices, like switching Player.</p>
          <Button fullWidth>Confirm</Button>
        </Sheet>
      </Card>

      <EmptyState title="Empty state" description="What's missing, and what happens next." />
      <ErrorState title="Error state" description="What failed, in plain words." />

      <Card title="Loading">
        <SkeletonGroup label="Loading example">
          <Skeleton className="h-6 w-40" />
          <Skeleton className="h-16" />
        </SkeletonGroup>
      </Card>
    </>
  );
}
