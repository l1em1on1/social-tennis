import type { PlayerSummary, Role } from "./types";

/**
 * STAND-IN DATA — replaced, not extended:
 *   - roles come from #18 (Admin: role management),
 *   - Players come from #4 (Player profiles and Club membership).
 *
 * Until those endpoints exist the shell still needs roles and Players to
 * render, so they're derived deterministically from the login email. That
 * keeps both the mocked and the real-API E2E runs able to choose a persona
 * just by the email they log in with:
 *
 *   local part contains "admin"   → Admin role
 *   local part contains "manager" → Manager role
 *   local part contains "parent"  → also holds two child Players
 *
 * A `+tag` suffix is ignored for the name, so tests can use unique emails.
 */
export function stubIdentity(email: string): { roles: Role[]; players: PlayerSummary[] } {
  const local = email.split("@")[0].toLowerCase();
  const base = local.split("+")[0];

  const roles: Role[] = [];
  if (base.includes("manager")) roles.push("Manager");
  if (base.includes("admin")) roles.push("Admin");

  const players: PlayerSummary[] = [{ id: "stub-self", name: titleCase(base) }];
  if (base.includes("parent")) {
    players.push({ id: "stub-child-1", name: "Alex Junior" }, { id: "stub-child-2", name: "Sam Junior" });
  }

  return { roles, players };
}

function titleCase(value: string): string {
  return value
    .split(/[._-]+/)
    .filter(Boolean)
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(" ");
}
