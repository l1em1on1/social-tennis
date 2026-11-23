import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { connection } from "next/server";
import "./globals.css";
// PROTOTYPE (#226): design directions, picked by <html data-direction>. Not for main.
import "./prototype-directions.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
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
    { media: "(prefers-color-scheme: light)", color: "#f6f7f4" },
    { media: "(prefers-color-scheme: dark)", color: "#11140f" },
  ],
};

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Every page, including the not-found page, renders per request: Next puts
  // the proxy's CSP nonce on its scripts only while rendering one (#53, Next's
  // CSP guide: "all pages must be dynamically rendered").
  await connection();
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}>
      <body className="min-h-full">{children}</body>
    </html>
  );
}
