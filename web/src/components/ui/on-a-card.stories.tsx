import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { useState } from "react";
import { ResultStatusBadge, resultStatuses } from "../result-status-badge";
import { Badge, type BadgeTone } from "./badge";
import { Button, ButtonLink } from "./button";
import { Card } from "./card";
import { Combobox } from "./combobox";
import { Field, Input } from "./field";
import { Pager, pageRange } from "./pager";
import { SearchForm } from "./search-form";
import { Select } from "./select";
import { EmptyState, ErrorState, Skeleton, SkeletonGroup } from "./states";
import { Stepper } from "./stepper";

const tones: BadgeTone[] = ["neutral", "accent", "success", "warning", "danger", "info"];

/** The Stepper holds no value of its own, so the story gives it one. */
function Games() {
  const [games, setGames] = useState(4);
  return <Stepper label="Set 1, Alex Morgan" value={games} max={6} onChange={setGames} />;
}

/**
 * The primitives as the app mostly shows them, on a card: the other stories
 * draw them on the page, and a token pairing that passes there can fail on the
 * card's colour. Storybook's accessibility check (storybook-tests/) runs on
 * this story in both themes, which is what `/dev/ui` was for (#232).
 */
const meta = { title: "UI/On a card", component: Card } satisfies Meta<typeof Card>;
export default meta;
type Story = StoryObj<typeof meta>;

export const EveryPrimitive: Story = {
  args: { children: null },
  render: () => (
    <div className="flex flex-col gap-4">
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
          <Field id="card-name" label="Name" hint="As it appears on the ladder.">
            {(control) => <Input {...control} placeholder="Anna Smith" />}
          </Field>
          <Field id="card-email" label="Email" error="Enter a valid email address.">
            {(control) => <Input {...control} type="email" defaultValue="anna@" />}
          </Field>
          <Field id="card-format" label="Score format">
            {(control) => (
              <Select {...control} defaultValue="bo3">
                <option value="one">One set</option>
                <option value="bo3">Best of three</option>
              </Select>
            )}
          </Field>
          <Field id="card-partner" label="Partner" hint="Type a name and pick from the list.">
            {(control) => (
              <Combobox
                {...control}
                name="partnerId"
                listLabel="Players"
                placeholder="Type a name…"
                options={[
                  { value: "a1", label: "Alex Morgan" },
                  { value: "a2", label: "Jo Bennett" },
                  { value: "a3", label: "Priya Shah" },
                ]}
              />
            )}
          </Field>
        </div>
      </Card>

      <Card title="Stepper">
        <Games />
      </Card>

      <Card title="Search and paging">
        <div className="flex flex-col gap-4">
          <SearchForm
            action="/admin"
            id="card-search"
            label="Find a user"
            hint="Part of an email address or a player's name."
            defaultValue="anna"
          />
          <p className="text-sm text-fg-muted">{pageRange(50, 50, 312)} matching “anna”, newest first.</p>
          <Pager label="users, first page" nextHref="/admin?offset=50" />
          <Pager label="users, middle page" previousHref="/admin" nextHref="/admin?offset=100" />
          <Pager label="users, last page" previousHref="/admin?offset=250" />
        </div>
      </Card>

      <Card title="Badges" aside={<ResultStatusBadge status="pending" />}>
        <div className="flex flex-wrap gap-2">
          {tones.map((tone) => (
            <Badge key={tone} tone={tone}>
              {tone}
            </Badge>
          ))}
          {resultStatuses.map((status) => (
            <ResultStatusBadge key={status} status={status} />
          ))}
        </div>
      </Card>

      <EmptyState title="Empty state" description="What's missing, and what happens next." />
      <ErrorState title="Error state" description="What failed, in plain words." />

      <Card title="Loading">
        <SkeletonGroup label="Loading example">
          <Skeleton className="h-6 w-40" />
          <Skeleton className="h-16" />
        </SkeletonGroup>
      </Card>
    </div>
  ),
};
