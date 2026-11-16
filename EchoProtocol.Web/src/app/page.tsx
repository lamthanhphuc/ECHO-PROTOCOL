import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { LandingGallery } from "./landing-gallery";
import { MentorSurprise } from "./mentor-surprise";
import { LandingMaps } from "./landing-maps";
import { LandingOperators } from "./landing-operators";
import styles from "./landing.module.css";

const description = "ECHO PROTOCOL là game kinh dị sinh tồn. Cùng đồng đội hoàn thành nhiệm vụ và tìm đường thoát.";

export const metadata: Metadata = {
  title: { absolute: "ECHO PROTOCOL — Kinh dị sinh tồn" },
  description,
  openGraph: {
    title: "ECHO PROTOCOL",
    description,
    type: "website",
    locale: "vi_VN",
    ...(process.env.WEB_PUBLIC_ORIGIN && {
      images: [{
        url: new URL("/media/facility.webp", process.env.WEB_PUBLIC_ORIGIN).href,
        alt: "Hành lang trong artwork ECHO PROTOCOL",
      }],
    }),
  },
};

const aboutFeatures = [
  {
    title: "Một mình hoặc cùng đồng đội",
    description: "Tiến vào vùng phong tỏa một mình hoặc lập đội tối đa 4 người. Khi có đồng đội, hãy chia nhau tìm vật phẩm, phối hợp hoàn thành nhiệm vụ và ứng cứu nhau khi nguy hiểm ập đến.",
  },
  {
    title: "Hai giao thức, hai cơn ác mộng",
    description: "Trong Khám phá, khôi phục các hệ thống thiết yếu để mở đường sơ tán. Trong Thanh trừng, truy tìm và tiêu hủy những sinh vật bất thường trước khi cả đội bị dồn vào đường cùng.",
  },
  {
    title: "Luôn có thứ đang săn đuổi",
    description: "Quái vật có thể phát hiện, lần theo và tấn công khi bạn mất cảnh giác. Cắt đuôi được chúng chỉ cho bạn thêm thời gian để tìm chỗ ẩn hoặc đổi đường.",
  },
  {
    title: "Sống sót, nhận thưởng, chuẩn bị",
    description: "Tích lũy kinh nghiệm và tín dụng từ những trận đủ điều kiện nhận thưởng. Xem thành tích trong hồ sơ và dùng tín dụng để mở khóa vật phẩm, nhân vật, trang phục hoặc bạn đồng hành trong cửa hàng.",
  },
];

export default function Home() {
  return (
    <main className={styles.landing}>
      <a className={styles.skip} href="#intro">Đến nội dung</a>
      <header className={styles.header}>
        <div className={styles.bar}>
          <Link className={styles.brand} href="/" aria-label="ECHO PROTOCOL, trang chủ"><Image src="/media/logo-new.webp" alt="" width={159} height={160} /></Link>
          <nav className={styles.nav} aria-label="Trang chủ">
            <a href="#about">Giới thiệu</a>
            <a href="#story">Cốt truyện</a>
            <a href="#maps">Bản đồ</a>
            <a href="#operators">Nhân vật</a>
            <a href="#gallery">Trưng bày</a>
          </nav>
          <Link className={styles.portal} href="/login">Đăng nhập</Link>
        </div>
      </header>

      <section id="intro" className={styles.hero} aria-labelledby="game-title">
        <Image className={styles.heroImage} src="/media/facility.webp" alt="" fill priority sizes="100vw" />
        <div className={styles.heroContent}>
          <h1 id="game-title">ECHO<br />PROTOCOL</h1>
          <p className={styles.tagline}>Tín hiệu vẫn đang hoạt động.</p>
          <p className={styles.heroLead}>Echo Protocol là một trò chơi kinh dị sinh tồn hợp tác dành cho 1-4 người chơi. Cùng đồng đội khôi phục hệ thống, hoàn thành nhiệm vụ và tìm đường thoát khi có thứ đang săn đuổi.</p>
          <div className={styles.heroActions}>
            <a className={styles.cta} href="#maps">Xem bản đồ <span aria-hidden="true">↓</span></a>
            <a className={styles.secondary} href="#operators">Xem nhân vật</a>
          </div>
          <MentorSurprise />
        </div>
      </section>

      <section id="about" className={styles.aboutSection} aria-labelledby="about-title">
        <div className={styles.section}>
          <div className={styles.aboutHead}>
            <div className={styles.signalDivider} aria-hidden="true"><span /></div>
            <h2 id="about-title">GIAO THỨC KỲ LẠ LIỆU BẠN CÓ LẮNG NGHE?</h2>
          </div>
          <div className={styles.aboutGrid}>
            {aboutFeatures.map(({ title, description }) => (
              <article key={title}>
                <span className={styles.signalMark} aria-hidden="true" />
                <div><h3>{title}</h3><p>{description}</p></div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section id="story" className={styles.section} aria-labelledby="story-title">
        <div className={styles.storyPanel}>
          <div className={styles.storyImage}>
            <Image src="/media/research-1.webp" alt="" fill sizes="(max-width: 760px) 95vw, 65vw" />
          </div>
          <div className={styles.storyContent}>
            <h2 id="story-title">NHỮNG KHU VỰC BỊ LÃNG QUÊN</h2>
            <p>Sau một loạt sự cố bất thường, nhiều khu vực bị phong tỏa và mất liên lạc với thế giới bên ngoài. Nguồn điện lần lượt tắt, các tuyến sơ tán bị cắt đứt. Thế nhưng từ bên kia ranh giới phong tỏa, những tín hiệu cảnh báo vẫn tiếp tục truyền về.</p>
            <p>Các báo cáo cuối cùng chỉ ghi nhận những cuộc chạm trán với sinh vật chưa xác định. Sau đó, tín hiệu liên lạc đột ngột biến mất.</p>
            <p>Giao thức ECHO được kích hoạt. Tùy tình hình tại hiện trường, các đội phản ứng được điều động để khôi phục hệ thống và mở đường sơ tán, hoặc thanh trừng những sinh vật đang đe dọa khu vực.</p>
            <p>Càng tiến sâu, họ càng khó giữ được đường quay về. Sau những cánh cửa đã khóa và dọc các tuyến sơ tán bị bỏ lại, có thứ vẫn di chuyển.</p>
            <p className={styles.storyQuestion}>Điều gì đã thực sự xảy ra bên trong vùng phong tỏa?</p>
            <a className={styles.secondary} href="#maps">Khám phá các khu vực <span aria-hidden="true">↓</span></a>
          </div>
        </div>
      </section>

      <LandingMaps />

      <section id="operators" className={styles.section} aria-labelledby="operators-title">
        <div className={styles.sectionHead}><h2 id="operators-title">Nhân vật có thể chọn</h2></div>
        <LandingOperators />
      </section>

      <LandingGallery />

    </main>
  );
}
