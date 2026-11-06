import policies from "@/content/policies.json";
import Link from "next/link";
import { backendRequest } from "@/lib/api/backend-client";
import { getSession } from "@/lib/auth/session";
import Acceptance from "./Acceptance";

export const metadata = { title: "Thông tin và chính sách" };

export default async function LegalPage() {
  let documents = policies;
  // Match the exact embedded UTF-8 bytes used by Unity and the API.
  let hash = "6052cf1107587c8c906cec1a13f9dfbb489c9136988b6ea4279ec832586093e4";
  try {
    const current = await backendRequest<{ bundle: typeof policies; contentSha256: string }>("/api/agreements/documents");
    documents = current.bundle; hash = current.contentSha256;
  } catch { /* Public text remains readable during a backend outage; acceptance still requires the API. */ }
  const session = await getSession();
  return <main style={{ maxWidth: 1000, margin: "40px auto", padding: "24px", color: "#e6e3da", background: "#10191c", lineHeight: 1.8 }}>
    <Link href="/">← ECHO PROTOCOL</Link>
    <h1>Thông tin và chính sách</h1>
    <p>Dành cho người từ 18 tuổi</p>
    <p>Cập nhật: {documents.effectiveDate.split("-").reverse().join("/")}<br />
      Hỗ trợ: <a href={`mailto:${documents.supportEmail}`}>{documents.supportEmail}</a></p>
    <nav aria-label="Các chính sách">{documents.documents.map(doc => <p key={doc.id}><a href={`#${doc.id}`}>{doc.titleVi}</a></p>)}</nav>
    {documents.documents.map(doc => <section id={doc.id} key={doc.id} style={{ borderTop: "1px solid #543337", marginTop: 32, paddingTop: 16 }}>
      <h2>{doc.titleVi}</h2>
      <div lang="vi" style={{ whiteSpace: "pre-wrap" }}>{doc.bodyVi}</div>
      <p><a href="#">↑ Đầu trang</a></p>
    </section>)}
    {session ? <Acceptance version={documents.bundleVersion} hash={hash} /> : <p><Link href="/login">Đăng nhập để xác nhận</Link></p>}
  </main>;
}
