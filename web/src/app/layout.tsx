import type { Metadata, Viewport } from "next";
import { Fraunces, Geist, Geist_Mono } from "next/font/google";
import { connection } from "next/server";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

// The display serif for page and card titles (#226, Clay court). A variable font, so no weights to list.
const fraunces = Fraunces({
  variable: "--font-fraunces",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "Hits & Giggles",
    template: "%s · Hits & Giggles",
  },
  description: "Club league scores, ladders, and fixtures.",
};

export const viewport: Viewport = {
  // Matches --st-bg in globals.css so the mobile browser chrome blends in.
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#fbf4ea" },
    { media: "(prefers-color-scheme: dark)", color: "#18110c" },
  ],
};

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Every page, including the not-found page, renders per request: Next puts
  // the proxy's CSP nonce on its scripts only while rendering one (#53, Next's
  // CSP guide: "all pages must be dynamically rendered").
  await connection();
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable} ${fraunces.variable} h-full antialiased`}>
      <body className="min-h-full">{children}</body>
    </html>
  );
}
