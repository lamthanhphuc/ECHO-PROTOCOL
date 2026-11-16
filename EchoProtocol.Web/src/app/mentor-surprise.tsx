"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import styles from "./mentor-surprise.module.css";

export function MentorSurprise() {
  const dialog = useRef<HTMLDialogElement>(null);
  const letterTitle = useRef<HTMLHeadingElement>(null);
  const ending = useRef<HTMLDivElement>(null);
  const [stage, setStage] = useState<"locked" | "decoding" | "letter">("locked");
  const [endingVisible, setEndingVisible] = useState(false);
  const revealed = stage === "letter";

  useEffect(() => {
    if (stage !== "decoding") return;
    const timer = window.setTimeout(() => setStage("letter"), 1500);
    return () => window.clearTimeout(timer);
  }, [stage]);

  useEffect(() => {
    if (!revealed || !ending.current) return;
    letterTitle.current?.focus();
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) {
        setEndingVisible(true);
        observer.disconnect();
      }
    }, { root: dialog.current, threshold: .25 });
    observer.observe(ending.current);
    return () => observer.disconnect();
  }, [revealed]);

  return (
    <>
      <button className={styles.trigger} type="button" onClick={() => dialog.current?.showModal()}>
        <span aria-hidden="true" />01 TÍN HIỆU CHƯA ĐỌC
      </button>
      <dialog ref={dialog} className={styles.dialog} data-revealed={revealed} aria-labelledby="mentor-title" onClose={() => {
        setStage("locked");
        setEndingVisible(false);
      }}>
        <div className={styles.bar}>
          <span className={styles.code} aria-label={stage === "locked" ? "Mã tín hiệu đang bị che" : "EP-20.11"}>
            {stage === "locked" ? <>EP-<span className={styles.noise}>▒▒</span>.<span className={styles.noise}>▒▒</span></> : "EP-20.11"}
          </span>
          <button className={styles.close} type="button" aria-label="Đóng thông điệp" onClick={() => dialog.current?.close()}>×</button>
        </div>
        {revealed ? (
          <article className={styles.letter}>
            <h2 id="mentor-title" ref={letterTitle} tabIndex={-1}>GỬI ĐẾN THẦY — NGƯỜI ĐỒNG HÀNH CÙNG ECHO PROTOCOL</h2>
            <p className={styles.greeting}><strong>Chúc mừng Ngày Nhà giáo Việt Nam 20/11!</strong></p>
            <a className={styles.photo} href="/media/20_11-review.webp" target="_blank" rel="noreferrer" aria-label="Xem ảnh lớp ở kích thước lớn">
              <Image src="/media/20_11-review.webp" alt="Ảnh chụp cùng thầy và cả lớp" width={1800} height={665} />
            </a>
            <p>Trong ECHO PROTOCOL, chúng em luôn tìm cách vượt qua những nhiệm vụ khó khăn để tìm được đường trở về.</p>
            <p>Còn trong hành trình thực hiện đồ án, cũng có những lúc cả nhóm loay hoay trước những vấn đề chưa biết phải giải quyết từ đâu. Những lời góp ý, những câu hỏi và sự hướng dẫn của thầy đã giúp chúng em nhìn nhận vấn đề rõ ràng hơn, biết cách suy nghĩ, tìm hướng giải quyết và từng bước hoàn thiện sản phẩm.</p>
            <p>Điều chúng em nhận được không chỉ là một đồ án tốt hơn, mà còn là những bài học quý giá về cách làm việc, tinh thần trách nhiệm và sự kiên trì khi đối mặt với khó khăn.</p>
            <p>Nhân ngày 20/11, cả nhóm muốn gửi đến thầy lời cảm ơn chân thành vì đã dành thời gian và tâm huyết đồng hành cùng chúng em.</p>
            <p><strong>Kính chúc thầy luôn mạnh khỏe, hạnh phúc và tiếp tục truyền cảm hứng cho nhiều thế hệ sinh viên.</strong></p>
            <p><em>Cảm ơn thầy vì đã là một phần trong hành trình ECHO PROTOCOL.</em></p>
            <p className={styles.signature}>Nhóm phát triển <strong>ECHO PROTOCOL</strong></p>
            <div ref={ending} className={styles.ending} data-visible={endingVisible} lang="en">
              <strong>SPECIAL MISSION COMPLETED</strong>
              <p><em>Thank you for helping us find our way when we didn&apos;t know where to begin.</em></p>
            </div>
            <button className={styles.action} type="button" onClick={() => dialog.current?.close()}>Trở lại ECHO PROTOCOL</button>
          </article>
        ) : stage === "decoding" ? (
          <div className={styles.transmission} role="status">
            <h2 id="mentor-title">ECHO TRANSMISSION</h2>
            <p><span aria-hidden="true">✓</span> Tín hiệu đã được giải mã</p>
            <p><span aria-hidden="true">✓</span> Đã xác nhận người nhận đặc biệt</p>
            <p><span aria-hidden="true">♡</span> <strong>Thông điệp: LỜI CẢM ƠN</strong></p>
          </div>
        ) : (
          <div className={styles.stage}>
            <h2 id="mentor-title">Tín hiệu kỳ lạ, hãy cẩn thận</h2>
            <button className={styles.action} type="button" onClick={() => setStage("decoding")}>Giải mã tín hiệu</button>
          </div>
        )}
      </dialog>
    </>
  );
}
