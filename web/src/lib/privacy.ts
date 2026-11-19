/**
 * Who answers for the data, from configuration (#51, ADR-0024): the
 * controller's name and the address for data requests. Read on the server at
 * request time, so production sets them without a rebuild.
 */
export function privacyContact(): { controller: string; email: string | null } {
  return {
    controller: process.env.PRIVACY_CONTROLLER?.trim() || "Hits & Giggles",
    email: process.env.PRIVACY_CONTACT_EMAIL?.trim() || null,
  };
}
