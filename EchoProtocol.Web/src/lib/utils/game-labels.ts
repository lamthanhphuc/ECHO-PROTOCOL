const mapNames: Record<string, string> = {
  SciFi: "Cơ sở nghiên cứu",
  Scene_A: "Vùng ngập",
  Scene_B: "Vùng ngập · Săn alien",
};

const difficultyNames: Record<string, string> = { EASY: "Dễ", NORMAL: "Thường", HARD: "Khó" };
const modeNames: Record<string, string> = { EXPEDITION: "Khám phá", PURGE: "Thanh trừng" };
const monsterNames: Record<string, string> = { STALKER: "Stalker", LIZARD: "Lizard" };
const routeNames: Record<string, string> = {
  DEFAULT_ROUTE: "Tuyến mặc định",
  SCENE_A_AUTHORED: "Tuyến có sẵn của bản đồ",
  SCENE_B_AUTHORED: "Tuyến có sẵn của bản đồ",
};

export const mapLabel = (id?: string | null) => id ? mapNames[id] ?? id : "—";
export const difficultyLabel = (id?: string | null) => id ? difficultyNames[id] ?? id : "—";
export const modeLabel = (id: string) => modeNames[id] ?? id;
export const monsterLabel = (id: string) => monsterNames[id] ?? id;
export const routeLabel = (id: string) => routeNames[id] ?? id;
export const gameplayGroupLabel = (group: string) => group === "Purge"
  ? "Nhiệm vụ Thanh trừng"
  : group.replace(/^Convoy/, "Hộ tống");
export const gameplayLabel = (label: string) => label
  .replace(/\bZone\b/g, "Khu vực")
  .replace(/\bconvoy\b/gi, "xe hộ tống")
  .replace("Nhịp spawn Purge", "Thời gian giữa hai lượt Fishcat xuất hiện")
  .replace("Số chặng chạy được sau mỗi lần nạp", "Số chặng sau mỗi lần nạp");

export const minionAttackDamage = 10;
