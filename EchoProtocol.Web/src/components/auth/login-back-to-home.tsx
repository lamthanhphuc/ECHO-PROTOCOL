"use client";

import { useEffect } from "react";

export function LoginBackToHome() {
  useEffect(() => {
    const url = window.location.href;
    if (!window.history.state?.loginBackToHome) {
      window.history.pushState({ ...window.history.state, loginBackToHome: true }, "", url);
    }
    const goHome = () => window.location.replace("/");
    window.addEventListener("popstate", goHome);
    return () => window.removeEventListener("popstate", goHome);
  }, []);
  return null;
}
