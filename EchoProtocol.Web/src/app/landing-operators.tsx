"use client";

import Image from "next/image";
import { useState } from "react";
import styles from "./landing.module.css";

const operators = [
  { name: "Astronaut", image: "astronaut", text: "Cựu kỹ sư cứu hộ quỹ đạo. Vẫn để trống kênh radio cho đồng đội cũ." },
  { name: "Jammo", image: "jammo", text: "Robot bảo trì thích cứu người hơn sửa máy. Giữ lại mọi lời cảm ơn." },
  { name: "Cursed Priest", image: "priest", text: "Vị tu sĩ mang lời nguyền. Vẫn dẫn đường cho những ai chưa tìm được lối về." },
  { name: "Mercenary", image: "mercenary", text: "Lính đánh thuê kỳ cựu. Nhận hợp đồng vượt xa tầm phủ sóng của đài cứu hộ cuối cùng." },
];

export function LandingOperators() {
  const [selected, setSelected] = useState(0);
  const operator = operators[selected];

  return (
    <div className={styles.roster}>
      <div className={styles.featuredPortrait}>
        <Image src={`/media/${operator.image}.webp`} alt={operator.name} fill sizes="(max-width: 640px) 100vw, 45vw" />
      </div>
      <div className={styles.rosterDetails}>
        <div aria-live="polite">
          <h3>{operator.name}</h3>
          <p>{operator.text}</p>
        </div>
        <div className={styles.rosterOptions} role="group" aria-label="Chọn nhân vật để xem">
          {operators.map(({ name, image }, index) => (
            <button key={name} type="button" aria-pressed={selected === index} onClick={() => setSelected(index)}>
              <span className={styles.optionPortrait}><Image src={`/media/${image}.webp`} alt="" fill sizes="96px" /></span>
              <span>{name}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
