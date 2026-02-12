"use client";

import { useMemo } from "react";

import { MSWProvider } from "@/mocks/msw-provider";
import { createMockServices, ServiceProvider } from "@/services";

export const Providers = ({ children }: { readonly children: React.ReactNode }): React.JSX.Element => {
  const services = useMemo(() => createMockServices(), []);

  return (
    <MSWProvider>
      <ServiceProvider services={services}>{children}</ServiceProvider>
    </MSWProvider>
  );
};
