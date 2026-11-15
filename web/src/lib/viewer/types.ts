/** Permission roles held by a User (CONTEXT: Manager, Admin). An Admin always has Manager too. */
export type Role = "Manager" | "Admin";

/** A Club a Player is in, or has asked to join. */
export interface PlayerClub {
  id: string;
  name: string;
  /** False while the request waits for a Manager. */
  approved: boolean;
}

/** What a Player plays as, for men's, women's and mixed leagues. */
export type PlayerGender = "Male" | "Female";

/** One of the User's Players: who they are, and which Clubs they are in. No Rating: Managers see that. */
export interface PlayerSummary {
  id: string;
  name: string;
  /** Not asked at first sign-in, so it may be unset. */
  gender: PlayerGender | null;
  /** The club's informal grouping, 1 to 5; unset until given. */
  level: number | null;
  clubs: PlayerClub[];
}

/** The logged-in User as every page sees them, including who they are acting as. */
export interface Viewer {
  userId: string;
  email: string;
  roles: Role[];
  players: PlayerSummary[];
  /** The Player the User is acting as; null for a User with no Player (just signed up, or a Manager-only account). */
  actingPlayer: PlayerSummary | null;
}
