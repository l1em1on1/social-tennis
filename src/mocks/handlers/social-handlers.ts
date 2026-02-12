import { http, HttpResponse } from "msw";

import { API_PATHS } from "@/lib/api-paths";
import type { Social, Vote } from "@/models";

import { mockSocials } from "../fixtures";

let socials = [...mockSocials];

export const socialHandlers = [
  http.get(API_PATHS.socials, () => {
    return HttpResponse.json(socials);
  }),

  http.get(API_PATHS.social(":id"), ({ params }) => {
    const social = socials.find((s) => s.id === params.id);
    if (!social) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(social);
  }),

  http.post(API_PATHS.socials, async ({ request }) => {
    const body = (await request.json()) as Omit<Social, "id" | "playerIds" | "votes" | "games">;
    const social: Social = {
      ...body,
      id: `social-${Date.now()}`,
      playerIds: [],
      votes: [],
      games: [],
    };
    socials = [...socials, social];
    return HttpResponse.json(social, { status: 201 });
  }),

  http.patch(API_PATHS.social(":id"), async ({ params, request }) => {
    const body = (await request.json()) as Partial<Pick<Social, "name" | "date">>;
    const index = socials.findIndex((s) => s.id === params.id);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const updated = { ...socials[index], ...body };
    socials = socials.map((s) => (s.id === params.id ? updated : s));
    return HttpResponse.json(updated);
  }),

  http.delete(API_PATHS.social(":id"), ({ params }) => {
    socials = socials.filter((s) => s.id !== params.id);
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(API_PATHS.socialVotes(":id"), async ({ params, request }) => {
    const body = (await request.json()) as Omit<Vote, "id">;
    const index = socials.findIndex((s) => s.id === params.id);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const social = socials[index];
    const vote: Vote = { ...body, id: `vote-${Date.now()}` };
    const updated = { ...social, votes: [...social.votes, vote] };
    socials = socials.map((s) => (s.id === params.id ? updated : s));
    return HttpResponse.json(updated);
  }),

  http.delete(API_PATHS.socialVote(":socialId", ":voteId"), ({ params }) => {
    const index = socials.findIndex((s) => s.id === params.socialId);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const social = socials[index];
    const updated = { ...social, votes: social.votes.filter((v) => v.id !== params.voteId) };
    socials = socials.map((s) => (s.id === params.socialId ? updated : s));
    return HttpResponse.json(updated);
  }),
];
