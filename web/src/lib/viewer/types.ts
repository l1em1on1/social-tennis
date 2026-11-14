/** Permission roles held by a User (CONTEXT: Manager, Admin). An Admin always has Manager too. */
export type Role = "Manager" | "Admin";

/** A Club a Player is in, or has asked to join. */
export interface PlayerClub {
  id: string;
  name: string;
  /** False while the request waits for a Manager. */
  approved: boolean;
}

/** One of the User's Players: enough to name it, switch to it, and say which Clubs it is in. */
export interface PlayerSummary {
  id: string;
  name: string;
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
