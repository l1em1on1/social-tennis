import { authHandlers } from "./auth-handlers";
import { leagueHandlers } from "./league-handlers";
import { playerHandlers } from "./player-handlers";
import { socialHandlers } from "./social-handlers";

export const handlers = [...leagueHandlers, ...socialHandlers, ...playerHandlers, ...authHandlers];
