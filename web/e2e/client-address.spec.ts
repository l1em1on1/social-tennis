// Plain logic, no page: @playwright/test rather than Chromatic's test, so no
// snapshot is captured.
import { expect, test } from "@playwright/test";
import { clientAddress } from "../src/lib/client-address";

test("the client address is the right-most forwarded entry, the one the ingress added (#108)", () => {
  // A browser that sends its own X-Forwarded-For still lands in the bucket of
  // the address the ingress appended.
  expect(clientAddress("198.51.100.1, 203.0.113.7")).toBe("203.0.113.7");
  expect(clientAddress("203.0.113.7")).toBe("203.0.113.7");
  expect(clientAddress(" 198.51.100.1 ,203.0.113.7 ")).toBe("203.0.113.7");
});

test("with no forwarded address there is no client address", () => {
  expect(clientAddress(null)).toBeUndefined();
  expect(clientAddress("")).toBeUndefined();
  expect(clientAddress("198.51.100.1, ")).toBeUndefined();
});
