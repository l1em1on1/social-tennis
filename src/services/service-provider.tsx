"use client";

import { createContext, useContext } from "react";

import type { Services } from "./types";

const ServiceContext = createContext<Services | null>(null);

export const useServices = (): Services => {
  const services = useContext(ServiceContext);
  if (!services) {
    throw new Error("useServices must be used within a ServiceProvider");
  }
  return services;
};

export const ServiceProvider = ({
  services,
  children,
}: {
  readonly services: Services;
  readonly children: React.ReactNode;
}): React.JSX.Element => {
  return <ServiceContext.Provider value={services}>{children}</ServiceContext.Provider>;
};
