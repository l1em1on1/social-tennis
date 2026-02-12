import { mockLeagues, mockPlayers, mockSocials } from "@/mocks/fixtures";

import { createMockAuthService } from "./auth-service";
import { createMockLeagueService } from "./league-service";
import { createMockPlayerService } from "./player-service";
import { createMockSocialService } from "./social-service";
import type { Services } from "./types";

export const createMockServices = (): Services => ({
  league: createMockLeagueService(mockLeagues),
  social: createMockSocialService(mockSocials),
  player: createMockPlayerService(mockPlayers),
  auth: createMockAuthService(),
});
