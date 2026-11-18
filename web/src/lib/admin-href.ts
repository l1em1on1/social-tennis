/**
 * The Admin page at a search and starting point in its Users list (#134),
 * optionally with an error to show. The first page has no offset. Used by the
 * page's pager and by the role actions, so a refused change lands back on the
 * same search and page.
 */
export function adminHref({ search = "", offset = 0, error }: { search?: string; offset?: number; error?: string }) {
  const params = new URLSearchParams();
  if (error) params.set("error", error);
  if (search) params.set("q", search);
  if (offset > 0) params.set("offset", String(offset));
  const query = params.toString();
  return query ? `/admin?${query}` : "/admin";
}
