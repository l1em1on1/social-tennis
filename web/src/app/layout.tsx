import type { Metadata, Viewport } from "next";
import { Big_Shoulders, Figtree, Geist_Mono } from "next/font/google";
import { connection } from "next/server";
import "./globals.css";

// The text face (#254, Hard court). Variable fonts, so no weights to list.
const figtree = Figtree({
  variable: "--font-figtree",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

// The display face for the app's name and the page and card titles: tall, like a scoreboard's.
// Its optical size axis gives the display cut at title sizes.
const bigShoulders = Big_Shoulders({
  variable: "--font-big-shoulders",
  subsets: ["latin"],
  axes: ["opsz"],
});

export const metadata: Metadata = {
  title: {
    default: "Hits & Giggles",
    template: "%s · Hits & Giggles",
  },
  description: "Club league scores, ladders, and fixtures.",
};

export const viewport: Viewport = {
  // Matches --st-court in globals.css, the header's blue, so the mobile browser chrome runs into it.
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#0b57a8" },
    { media: "(prefers-color-scheme: dark)", color: "#0a3a73" },
  ],
};

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Every page, including the not-found page, renders per request: Next puts
  // the proxy's CSP nonce on its scripts only while rendering one (#53, Next's
  // CSP guide: "all pages must be dynamically rendered").
  await connection();
  return (
    <html lang="en-GB" className={`${figtree.variable} ${geistMono.variable} ${bigShoulders.variable} h-full antialiased`}>
      <body className="min-h-full">{children}</body>
    </html>
  );
}
