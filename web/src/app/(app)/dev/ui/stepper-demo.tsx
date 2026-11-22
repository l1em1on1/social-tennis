"use client";

import { useState } from "react";
import { Stepper } from "@/components/ui/stepper";

/** The Stepper holds no value of its own, so the showcase gives it one. */
export function StepperDemo() {
  const [games, setGames] = useState(4);
  return <Stepper label="Set 1, Alex Morgan" value={games} max={6} onChange={setGames} />;
}
