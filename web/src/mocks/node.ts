import { setupServer } from "msw/node";
import { handlers } from "./handlers";

/** Node-mode interception: the BFF's server-side fetches to API_URL are answered by the handlers. */
export const server = setupServer(...handlers);
