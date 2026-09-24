import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "ECHO PROTOCOL", template: "%s · ECHO PROTOCOL" },
  description: "ECHO PROTOCOL player and administration portal",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="vi"><body>{children}</body></html>;
}
