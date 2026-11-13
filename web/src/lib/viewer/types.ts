/** Permission roles held by a User (CONTEXT: Manager, Admin). */
export type Role = "Manager" | "Admin";

/** Just enough of a Player to name and switch to it. */
export interface PlayerSummary {
  id: string;
  name: string;
}

/** The logged-in User as every page sees them, including who they are acting as. */
export interface Viewer {
  userId: string;
  email: string;
  roles: Role[];
  players: PlayerSummary[];
  /** The Player the User is acting as; null for a User with no Player (e.g. a Manager-only account). */
  actingPlayer: PlayerSummary | null;
}
