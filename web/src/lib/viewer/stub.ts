import type { Role } from "./types";

/**
 * STAND-IN DATA — replaced, not extended: roles come from #18 (Admin: role
 * management).
 *
 * Until that endpoint exists the shell still needs roles to render, so they
 * are derived from the login email. That lets both the mocked and the
 * real-API E2E runs choose a persona just by the email they log in with:
 *
 *   local part contains "admin"   → Admin role
 *   local part contains "manager" → Manager role
 *
 * A `+tag` suffix is ignored, so tests can use unique emails.
 */
export function stubIdentity(email: string): { roles: Role[] } {
  const base = email.split("@")[0].toLowerCase().split("+")[0];

  const roles: Role[] = [];
  if (base.includes("manager")) roles.push("Manager");
  if (base.includes("admin")) roles.push("Admin");

  return { roles };
}
