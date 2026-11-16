"use client";

import Image from "next/image";
import { useState } from "react";
import styles from "./landing.module.css";

const maps = [
  {
    id: "research", name: "Cơ sở nghiên cứu",
    description: "Cơ sở nghiên cứu bị cô lập sau một sự cố chưa rõ nguyên nhân. Nguồn điện gián đoạn, hệ thống an ninh chập chờn và lối sơ tán vẫn bị khóa. Dưới ánh đèn còn sót lại, những hành lang thép nối các khu làm việc với kho thiết bị bỏ dở.",
    tension: "Trong những lối đi hẹp, ánh đèn chỉ soi được tới khúc ngoặt kế tiếp. Một quái vật rình rập vẫn lẩn khuất giữa các căn phòng bị phong tỏa, nơi tiếng động có thể vọng xa hơn tầm nhìn.",
    enemy: "Quái vật rình rập", portrait: "stalker",
  },
  {
    id: "flooded", name: "Vùng ngập",
    description: "Sương mù phủ lên khu dân cư bỏ hoang. Những mái nhà và tháp chuông thấp thoáng sau hàng cây, còn nước tràn qua lối đi giữa các công trình. Các trạm điện và trạm tiếp sóng nằm rải rác dọc tuyến sơ tán.",
    tension: "Trong vùng ngập, một quái vật bò sát lẩn khuất giữa các công trình bỏ trống. Sương dày che khuất những đoạn đường nối các khu nhà, khiến cả đội khó nhìn thấy thứ đang bám theo mình.",
    enemy: "Quái vật bò sát", portrait: "lizard",
  },
];

export function LandingMaps() {
  const [mapIndex, setMapIndex] = useState(0);
  const [photo, setPhoto] = useState(1);
  const map = maps[mapIndex];

  function selectMap(index: number) {
    setMapIndex(index);
    setPhoto(1);
  }

  return (
    <section id="maps" className={styles.section} aria-labelledby="maps-title">
      <h2 id="maps-title" className={styles.mapHeading}>Bản đồ</h2>
      <div className={styles.mapFrame}>
        <button className={styles.mapPrev} type="button" aria-label="Bản đồ trước" onClick={() => selectMap((mapIndex + maps.length - 1) % maps.length)}>←</button>
        <button className={styles.mapNext} type="button" aria-label="Bản đồ tiếp theo" onClick={() => selectMap((mapIndex + 1) % maps.length)}>→</button>
        <article className={styles.mapCard}>
          <div className={styles.mapDetails}>

            <h3 aria-live="polite">{map.name}</h3>
            <p>{map.description}</p>
            <p>{map.tension}</p>
          </div>
            <div className={styles.mapPhoto}>
              <Image src={photo === 5 ? `/media/${map.portrait}.webp` : `/media/${map.id}-${photo}.webp`} alt={photo === 5 ? map.enemy : `${map.name} · Ảnh ${photo} / 5`} fill sizes="(max-width: 760px) 90vw, 50vw" />
              <div className={styles.mapDots} role="group" aria-label="Ảnh bản đồ">
                {[1, 2, 3, 4, 5].map((index) => (
                  <button key={index} type="button" aria-label={`Ảnh ${index} / 5`} aria-pressed={index === photo} onClick={() => setPhoto(index)} />
                ))}
              </div>
            </div>
        </article>
      </div>
    </section>
  );
}
