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
      <h1 className="text-2xl font-bold">Privacy notice</h1>

      <Section title="Who we are">
        <p>
          {controller} runs this app for its tennis leagues and socials, and is responsible for the data in it. Questions
          and requests go to {contact}.
        </p>
      </Section>

      <Section title="What we hold">
        <ul className="list-disc pl-5">
          <li>Your email address, which is how you sign in, and whether you&apos;re a Manager or Admin.</li>
          <li>For each of your players: name, gender, level and rating.</li>
          <li>
            Which clubs and leagues each player is in or has asked to join, the answers, and their games and scores.
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

      <Section title="Who else sees it">
        <p>
          Nobody: we don&apos;t sell or share it. The app runs on Microsoft Azure, which also sends the emails, with its
          database at Neon.
        </p>
      </Section>

      <Section title="How long we keep it">
        <p>
          While you use the app. Accounts with no sign-in for 24 months are to be deleted; that starts once the app
          records sign-ins, which is being added. Backups of the database are kept for up to three weeks.
        </p>
      </Section>

      <Section title="Seeing, correcting or deleting your data">
        <p>
          Email {contact}, and we&apos;ll do it. When an account is deleted, its login goes, and its players are kept as
          &ldquo;Former player&rdquo;, so other members&apos; results stay correct.
        </p>
      </Section>

      <Link href="/" className="text-accent underline">
        Back to the app
      </Link>
    </main>
  );
}
