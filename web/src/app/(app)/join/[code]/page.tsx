import type { Metadata } from "next";
import { PlayerForm } from "@/components/player-form";
import { ButtonLink } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState } from "@/components/ui/states";
import { SubmitButton } from "@/components/ui/submit-button";
import { askToJoinClub } from "@/lib/actions";
import { apiWithSession } from "@/lib/api/server";
import { isJoinCode, joinPath } from "@/lib/join";
import { getViewer } from "@/lib/viewer";
import { PageColumn } from "@/components/shell/page-column";

export const metadata: Metadata = { title: "Join a club" };

/**
 * A Club's join link (#222, ADR-0032), shared by its Managers. It names the
 * Club, and the Acting Player asks to join with a button: opening the link
 * changes nothing, since chat apps fetch links for their previews. A Manager
 * then approves. A link that's off, reset or never existed says only that it
 * doesn't work. Signed out, the proxy sends the visitor to sign in with the
 * code, and the emailed link brings them back here.
 */
export default async function JoinPage({ params, searchParams }: PageProps<"/join/[code]">) {
  const [{ code }, { error }, viewer, api] = await Promise.all([params, searchParams, getViewer(), apiWithSession()]);
  const lookup = isJoinCode(code) ? await api.GET("/join-links/{code}", { params: { path: { code } } }) : null;
  if (lookup && !lookup.data && lookup.response.status !== 404) {
    throw new Error(`GET /join-links failed with ${lookup.response.status}`);
  }
  const club = lookup?.data;

  if (!club) {
    return (
      <PageColumn>
        <h1 className="text-4xl leading-none font-black">Join a club</h1>
        <Card title="This link doesn't work any more">
          <p className="text-fg-muted">Ask your club&apos;s manager for a new one.</p>
          <ButtonLink href="/" variant="secondary" className="mt-4">
            Go to Home
          </ButtonLink>
        </Card>
      </PageColumn>
    );
  }

  const { actingPlayer } = viewer;
  const membership = actingPlayer?.clubs.find((candidate) => candidate.id === club.clubId);
  return (
    <PageColumn>
      <h1 className="text-4xl leading-none font-black">Join {club.clubName}</h1>

      {!actingPlayer ? (
        // A first sign-in: a name to ask as, then the button below.
        <Card title="Welcome! What's your name?">
          {error === "name" && (
            <div className="mb-4">
              <ErrorState title="That name didn't work" description="Enter a name of up to 100 characters." />
            </div>
          )}
          <PlayerForm
            id="welcome-name"
            label="Your name"
            hint="The name other players see on ladders and scores. You can change it, and add your level, in Profiles."
            submitLabel="Save my name"
            from={joinPath(code)}
          />
        </Card>
      ) : membership?.approved ? (
        <Card title={`${actingPlayer.name} is a member`}>
          <p className="text-fg-muted">
            {actingPlayer.name} is in {club.clubName} already.
          </p>
          <ButtonLink href="/" variant="secondary" className="mt-4">
            Go to Home
          </ButtonLink>
        </Card>
      ) : membership ? (
        <Card title="Waiting for approval">
          <p className="text-fg-muted">
            {actingPlayer.name}&apos;s request to join {club.clubName} is with the club&apos;s manager. Once you&apos;re in,
            your leagues and games show on Home.
          </p>
        </Card>
      ) : (
        <Card title="Ask to join">
          <p className="text-fg-muted">A manager of the club approves each request.</p>
          <form action={askToJoinClub} className="mt-4">
            <input type="hidden" name="code" value={code} />
            <input type="hidden" name="playerId" value={actingPlayer.id} />
            <SubmitButton fullWidth pendingLabel="Asking…">
              Ask to join {club.clubName}
            </SubmitButton>
          </form>
          {viewer.players.length > 1 && (
            <p className="mt-3 text-sm text-fg-muted">
              Asking as {actingPlayer.name}. Switch player at the top to ask as someone else.
            </p>
          )}
        </Card>
      )}
    </PageColumn>
  );
}
