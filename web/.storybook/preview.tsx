import type { Preview } from "@storybook/nextjs-vite";
import { Geist, Geist_Mono } from "next/font/google";
import "../src/app/globals.css";
import { allModes } from "./modes";

// As in the root layout, which stories don't render: the font variables
// globals.css reads, on an element that also sets the page's font and colours.
const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

const preview: Preview = {
  parameters: {
    // Every page is in the app router, so next/link, next/form and usePathname get its mocks.
    nextjs: { appDirectory: true },
    layout: "fullscreen",
    chromatic: { modes: { light: allModes.light, dark: allModes.dark } },
  },
  decorators: [
    (Story) => (
      <div className={`${geistSans.variable} ${geistMono.variable} bg-bg p-4 font-sans text-fg antialiased`}>
        <Story />
      </div>
    ),
  ],
};

export default preview;
