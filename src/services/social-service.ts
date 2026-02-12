import type { Social } from "@/models";

import type { SocialService } from "./types";

export const createMockSocialService = (initialSocials: readonly Social[]): SocialService => {
  let socials = [...initialSocials];

  return {
    getAll: async () => socials,

    getById: async (id) => socials.find((s) => s.id === id) ?? null,

    create: async (data) => {
      const social: Social = {
        ...data,
        id: `social-${Date.now()}`,
        playerIds: [],
        votes: [],
        games: [],
      };
      socials = [...socials, social];
      return social;
    },

    update: async (id, data) => {
      const index = socials.findIndex((s) => s.id === id);
      if (index === -1) throw new Error(`Social ${id} not found`);
      const updated = { ...socials[index], ...data };
      socials = socials.map((s) => (s.id === id ? updated : s));
      return updated;
    },

    remove: async (id) => {
      socials = socials.filter((s) => s.id !== id);
    },

    addVote: async (socialId, voteData) => {
      const index = socials.findIndex((s) => s.id === socialId);
      if (index === -1) throw new Error(`Social ${socialId} not found`);
      const social = socials[index];
      const vote = { ...voteData, id: `vote-${Date.now()}` };
      const updated = { ...social, votes: [...social.votes, vote] };
      socials = socials.map((s) => (s.id === socialId ? updated : s));
      return updated;
    },

    removeVote: async (socialId, voteId) => {
      const index = socials.findIndex((s) => s.id === socialId);
      if (index === -1) throw new Error(`Social ${socialId} not found`);
      const social = socials[index];
      const updated = { ...social, votes: social.votes.filter((v) => v.id !== voteId) };
      socials = socials.map((s) => (s.id === socialId ? updated : s));
      return updated;
    },
  };
};
