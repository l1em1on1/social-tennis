import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import type { ReactNode } from "react";
import { privacyContact } from "@/lib/privacy";

export const metadata: Metadata = { title: "Privacy" };

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-lg font-semibold">{title}</h2>
      {children}
    </section>
  );
}

/**
 * The privacy notice (#51, ADR-0024): public, so it opens without signing in.
 * The controller and the contact address come from configuration.
 */
export default async function PrivacyPage() {
  // Configuration is read per request (Next.js docs, "Runtime environment variables").
  await connection();
  const { controller, email } = privacyContact();
  const contact = email ? (
    <a href={`mailto:${email}`} className="font-medium text-accent underline">
      {email}
    </a>
  ) : (
    "the club's Admin"
  );

  return (
    <main className="mx-auto flex w-full max-w-md flex-col gap-6 px-4 py-8 leading-relaxed">
      <h1 className="text-4xl leading-none font-black">Privacy notice</h1>

      <Section title="Who we are">
        <p>
          {controller} runs this app for its tennis leagues and socials, and is responsible for the data in it. Questions
          and requests go to {contact}.
        </p>
      </Section>

      <Section title="What we hold">
        <ul className="list-disc pl-5">
          <li>
            Your email address, which is how you sign in, when you last signed in, which clubs and leagues you manage,
            and whether you&apos;re an Admin.
          </li>
          <li>For each of your players: name, gender, level and rating.</li>
          <li>
            Which clubs and leagues each player is in or has asked to join, the answers, and their games and scores: the
            days they say they&apos;re free to play, and the date and court note agreed for a game.
          </li>
          <li>An email address a Manager pre-approves for a club, before that person has signed in.</li>
          <li>Each sign-in link, with the address it went to, until the nightly clean-up after it&apos;s used or expires.</li>
        </ul>
        <p>
          A Manager may add a member by name and email before they have signed in. The sign-in email links to this
          notice.
        </p>
      </Section>

      <Section title="Why">
        <p>
          To run the leagues and socials you take part in. That is our legitimate interest, so we don&apos;t ask for
          consent, and we send nothing but sign-in emails.
        </p>
      </Section>

      <Section title="Children">
        <p>
          A child&apos;s player is held by their parent&apos;s login, with no email or login of its own. Children
          don&apos;t sign in.
        </p>
      </Section>

      <Section title="Cookies">
        <p>
          Two, both needed for the app to work: one keeps you signed in, the other remembers which of your players
          you&apos;re playing as. No analytics or tracking.
        </p>
      </Section>

      <Section title="Sign-in requests">
        <p>
          Sign-in links are limited per network address. With each link we keep a one-way hash of the address it was
          asked from: not the address itself, though it could be matched back to it. It goes with the link, in the
          nightly clean-up. The address itself is kept only in memory, for a minute.
        </p>
      </Section>

      <Section title="Who sees it">
        <ul className="list-disc pl-5">
          <li>Admins: everyone&apos;s email address and roles.</li>
          <li>Managers: their club&apos;s members with their email addresses, requests to join, and pre-approved addresses.</li>
          <li>
            League managers: the names of the club&apos;s members, to add them to a league, and the email address, gender
            and rating of players in their leagues or asking to join one.
          </li>
          <li>
            Other members: players&apos; names in clubs and leagues, and their games and scores. A league says who runs it,
            by name. A game&apos;s agreed date and court note are seen by its league.
          </li>
          <li>
            The other players in a game: the days that suit everyone in it, which shows them which of your free days they
            share. Your whole list of free days is seen by you alone.
          </li>
        </ul>
        <p>
          Outside the club, nobody: we don&apos;t sell or share it. The app runs on Microsoft Azure, which also sends the
          emails, with its database at Neon.
        </p>
      </Section>

      <Section title="How long we keep it">
        <p>
          While you use the app. An account with no sign-in for 24 months is deleted, in the way described below: we record
          when you last signed in for that. An account you have never signed in to is kept, as when a manager adds you and
          records your scores. Backups of the database are kept for up to three weeks.
        </p>
      </Section>

      <Section title="Seeing, correcting or deleting your data">
        <p>
          Email {contact}, and we&apos;ll do it. When an account is deleted, its login goes, and its players are kept as
          &ldquo;Former player&rdquo;, so other members&apos; results stay correct. The days they said they were free go.
        </p>
      </Section>

      <Link href="/" className="text-accent underline">
        Back to the app
      </Link>
    </main>
  );
}
