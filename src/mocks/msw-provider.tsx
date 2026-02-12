"use client";

import { useEffect, useState } from "react";

const useMockEnabled = process.env.NEXT_PUBLIC_USE_MOCKS === "true";

export const MSWProvider = ({
  children,
}: {
  readonly children: React.ReactNode;
}): React.JSX.Element | null => {
  const [ready, setReady] = useState(!useMockEnabled);

  useEffect(() => {
    if (!useMockEnabled) return;

    const init = async (): Promise<void> => {
      const { worker } = await import("./browser");
      await worker.start({ onUnhandledRequest: "bypass" });
      setReady(true);
    };

    void init();
  }, []);

  if (!ready) return null;

  return <>{children}</>;
};
