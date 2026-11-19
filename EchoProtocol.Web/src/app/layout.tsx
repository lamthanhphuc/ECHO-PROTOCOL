import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "ECHO PROTOCOL", template: "%s · ECHO PROTOCOL" },
  description: "Cổng người chơi và quản trị ECHO PROTOCOL",
  icons: { icon: "/echo-login-logo.png?v=3" },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="vi"><body>
    <div className="site-content">{children}</div>
  </body></html>;
}
