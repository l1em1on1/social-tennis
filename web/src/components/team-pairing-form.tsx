"use client";

import { useState } from "react";
import { Field } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { SubmitButton } from "@/components/ui/submit-button";
import { pairTeam } from "@/lib/actions";

export interface UnpairedPlayer {
  playerId: string;
  name: string;
  gender: "Male" | "Female" | null;
}

/**
 * A Manager pairs two of a doubles League's unpaired Players into a Team (#6).
 * In mixed doubles it warns about two Players of the same gender but still
 * lets the Manager pair them, to make up the numbers (owner, 2026-11-20).
 */
export function TeamPairingForm({
  leagueId,
  mixed,
  players,
}: {
  leagueId: string;
  mixed: boolean;
  players: UnpairedPlayer[];
}) {
  const [first, setFirst] = useState("");
  const [second, setSecond] = useState("");
  const genderOf = (playerId: string) => players.find((player) => player.playerId === playerId)?.gender ?? null;
  const sameGender = mixed && first !== "" && second !== "" && genderOf(first) !== null && genderOf(first) === genderOf(second);
  const options = (exclude: string) =>
    players
      .filter((player) => player.playerId !== exclude)
      .map((player) => (
        <option key={player.playerId} value={player.playerId}>
          {mixed && player.gender ? `${player.name} (${player.gender === "Male" ? "man" : "woman"})` : player.name}
        </option>
      ));

  return (
    <form action={pairTeam} className="flex flex-col gap-3">
      <input type="hidden" name="leagueId" value={leagueId} />
      <Field id="pair-first" label="First player">
        {(control) => (
          <Select {...control} name="firstPlayerId" required value={first} onChange={(event) => setFirst(event.target.value)}>
            <option value="" disabled>
              Pick a player…
            </option>
            {options(second)}
          </Select>
        )}
      </Field>
      <Field id="pair-second" label="Second player">
        {(control) => (
          <Select {...control} name="secondPlayerId" required value={second} onChange={(event) => setSecond(event.target.value)}>
            <option value="" disabled>
              Pick a player…
            </option>
            {options(first)}
          </Select>
        )}
      </Field>
      <p role="status" className="text-sm text-fg-muted">
        {sameGender && "Both are the same gender. A mixed doubles team is usually a man and a woman, but you can still pair them."}
      </p>
      <SubmitButton variant="secondary" pendingLabel="Pairing…">
        Pair as a team
      </SubmitButton>
    </form>
  );
}
