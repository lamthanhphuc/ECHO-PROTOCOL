import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "ECHO PROTOCOL", template: "%s · ECHO PROTOCOL" },
  description: "ECHO PROTOCOL player and administration portal",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="vi"><body>
    <div className="blood-edge" aria-hidden="true">
      {Array.from({ length: 8 }, (_, index) => <span className="blood-stream" key={index} />)}
    </div>
    <div className="site-content">{children}</div>
    <footer style={{ textAlign: "center", padding: 20, fontSize: 14 }}>
      <a href="/legal">Chính sách / Policies · 18+</a>
    </footer>
  </body></html>;
}
