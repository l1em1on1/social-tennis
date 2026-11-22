/**
 * Storybook's stand-in for `@/lib/actions` (#189): the real server actions need
 * the request (`next/headers`) and the API, so `.storybook/preview.tsx` swaps
 * this file in and the real module is never loaded. Same exports, no server.
 *
 * An action never finishes, so a pressed button stays in its pending state.
 * `createLeague` answers with a refusal, so the setup form shows its errors.
 */
import type { CreateLeagueState, JoinRefusal, SubmitScoreState } from "../actions";

export type { CreateLeagueState, JoinRefusal, SubmitScoreState };

const pending = () => new Promise<never>(() => {});

export const switchActingPlayer = pending;
export const logout = pending;
export const createPlayer = pending;
export const updatePlayer = pending;
export const joinClub = pending;
export const approveClubMember = pending;
export const removeClubMember = pending;
export const inviteToClub = pending;
export const withdrawClubInvite = pending;
export const leaveClub = pending;
export const createClub = pending;
export const renameClub = pending;
export const grantRole = pending;
export const revokeRole = pending;
export const addClubPlayer = pending;
export const askToJoinLeague = pending;
export const takeBackLeagueJoinRequest = pending;
export const pairTeam = pending;
export const unpairTeam = pending;
export const approveLeagueJoinRequest = pending;
export const rejectLeagueJoinRequest = pending;
export const addLeagueParticipant = pending;
export const removeLeagueParticipant = pending;
export const renameLeague = pending;
export const startLeague = pending;

export async function createLeague(): Promise<CreateLeagueState> {
  return {
    errors: {
      name: ["A League in this Club already has that name."],
      endDate: ["The League must end after it starts."],
    },
  };
}
export const submitScore = pending;
export const voidGame = pending;
export const unvoidGame = pending;
