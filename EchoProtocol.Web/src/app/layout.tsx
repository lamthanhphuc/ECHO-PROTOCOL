import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "ECHO PROTOCOL", template: "%s · ECHO PROTOCOL" },
  description: "Cổng người chơi và quản trị ECHO PROTOCOL",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="vi"><body>
    <div className="site-content">{children}</div>
    <footer style={{ textAlign: "center", padding: 20, fontSize: 14 }}>
      <a href="/legal">Chính sách · Dành cho người từ 18 tuổi</a>
    </footer>
  </body></html>;
}
