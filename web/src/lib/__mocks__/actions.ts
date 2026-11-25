/**
 * Storybook's stand-in for `@/lib/actions` (#189): the real server actions need
 * the request (`next/headers`) and the API, so `.storybook/preview.tsx` swaps
 * this file in and the real module is never loaded. Same exports, no server.
 *
 * An action never finishes, so a pressed button stays in its pending state.
 * `createLeague` answers with a refusal, so the setup form shows its errors.
 */
import type { CloseStageState, CreateLeagueState, JoinRefusal, SubmitScoreState } from "../actions";

export type { CloseStageState, CreateLeagueState, JoinRefusal, SubmitScoreState };

const pending = () => new Promise<never>(() => {});

export const switchActingPlayer = pending;
export const logout = pending;
export const createPlayer = pending;
export const updatePlayer = pending;
export const askToJoinClub = pending;
export const turnOnJoinLink = pending;
export const resetJoinLink = pending;
export const turnOffJoinLink = pending;
export const approveClubMember = pending;
export const removeClubMember = pending;
export const addToClub = pending;
export const withdrawClubInvite = pending;
export const leaveClub = pending;
export const createClub = pending;
export const renameClub = pending;
export const grantLeagueManagerRole = pending;
export const removeLeagueManagerRole = pending;
export const assignLeagueManager = pending;
export const unassignLeagueManager = pending;
export const makeClubManager = pending;
export const removeClubManager = pending;
export const grantAdmin = pending;
export const revokeAdmin = pending;
export const deleteUser = pending;
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
export const closeStage = pending;
export const moveTeam = pending;
