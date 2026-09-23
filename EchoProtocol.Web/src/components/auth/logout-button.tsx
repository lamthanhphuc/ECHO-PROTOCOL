"use client";

export function LogoutButton() {
  return <form action="/api/auth/logout" method="post">
    <button className="button button-secondary w-full" type="submit">Đăng xuất</button>
  </form>;
}
