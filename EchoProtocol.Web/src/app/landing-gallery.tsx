"use client";

import Image from "next/image";
import { useRef, useState } from "react";
import styles from "./landing.module.css";

const photos = [
  ["main-menu", "Menu chính"], ["lobby", "Phòng chờ"],
  ...[1, 2, 3, 4].map((n) => [`research-${n}`, `Cơ sở nghiên cứu · ${n}`]),
  ...[1, 2, 3, 4].map((n) => [`flooded-${n}`, `Vùng ngập · ${n}`]),
  ["stalker", "Quái vật rình rập"], ["lizard", "Quái vật bò sát"],
];
const title = "ECHO PROTOCOL: RESEARCH FACILITY";

export function LandingGallery() {
  const [view, setView] = useState("Đoạn giới thiệu");
  const [playing, setPlaying] = useState(false);
  const [index, setIndex] = useState(0);
  const dialog = useRef<HTMLDialogElement>(null);
  const [file, caption] = photos[index];

  function step(direction: number) {
    setIndex((current) => (current + direction + photos.length) % photos.length);
  }

  return (
    <section id="gallery" className={styles.gallery} aria-labelledby="gallery-title">
      <div className={styles.section}>
        <h2 id="gallery-title">TRƯNG BÀY</h2>
        <div className={styles.galleryControls} role="group" aria-label="Nội dung trưng bày">
          {["Đoạn giới thiệu", "Hình ảnh"].map((option) => (
            <button key={option} type="button" aria-pressed={view === option} onClick={() => {
              setView(option);
              if (option === "Hình ảnh") setPlaying(false);
            }}>{option}</button>
          ))}
        </div>
        {view === "Đoạn giới thiệu" ? (
          <div className={styles.galleryVideo}>
            <div className={styles.galleryMedia}>
              {playing ? (
                <iframe src="https://www.youtube-nocookie.com/embed/eWeeLHnXCnA?autoplay=1" title={title} allow="autoplay; encrypted-media; picture-in-picture" referrerPolicy="strict-origin-when-cross-origin" allowFullScreen />
              ) : (
                <button className={styles.galleryPlay} type="button" aria-label={`Phát ${title}`} onClick={() => setPlaying(true)}>
                  <Image src="/media/stalker.webp" alt="" fill sizes="(max-width: 960px) 90vw, 56rem" />
                  <span aria-hidden="true">▶</span>
                  <strong>{title}</strong>
                </button>
              )}
            </div>
            <a href="https://www.youtube.com/watch?v=eWeeLHnXCnA" target="_blank" rel="noopener noreferrer">Xem trên YouTube ↗</a>
          </div>
        ) : (
          <div className={styles.galleryGrid}>
            {photos.map(([name, label], photoIndex) => (
              <button key={name} type="button" className={styles.galleryMedia} onClick={() => {
                setIndex(photoIndex);
                dialog.current?.showModal();
              }}>
                <Image src={`/media/${name}.webp`} alt={label} fill sizes="(max-width: 540px) 90vw, (max-width: 900px) 45vw, 28vw" />
              </button>
            ))}
          </div>
        )}
        <dialog ref={dialog} className={styles.galleryDialog} aria-label="Xem ảnh trưng bày" onKeyDown={(event) => {
          if (event.key === "ArrowLeft") step(-1);
          if (event.key === "ArrowRight") step(1);
        }}>
          <div className={styles.galleryDialogBar}>
            <span aria-live="polite">{caption} · {index + 1}/{photos.length}</span>
            <button type="button" aria-label="Đóng ảnh" onClick={() => dialog.current?.close()}>×</button>
          </div>
          <div className={styles.galleryFullImage}>
            <Image src={`/media/${file}.webp`} alt={caption} fill sizes="96vw" />
          </div>
          <div className={styles.galleryControls}>
            <button type="button" aria-label="Ảnh trước" onClick={() => step(-1)}>←</button>
            <button type="button" aria-label="Ảnh tiếp theo" onClick={() => step(1)}>→</button>
          </div>
        </dialog>
      </div>
    </section>
  );
}
