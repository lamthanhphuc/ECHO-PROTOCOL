"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

interface GrantCreditsData {
  userId: string;
  transactionId: string;
  reference: string;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  reason: string;
  createdAtUtc: string;
}

interface GrantCreditsResponse {
  success: boolean;
  message: string;
  data: GrantCreditsData | null;
  errorCode: string | null;
}

export function AdminCreditGrant({
  userId,
  currentBalance,
  recipient,
}: {
  userId: string;
  currentBalance: number;
  recipient: string;
}) {
  const router = useRouter();

  const [amount, setAmount] =
    useState("1000");

  const [reason, setReason] =
    useState("");

  const [balance, setBalance] =
    useState(currentBalance);

  const [submitting, setSubmitting] =
    useState(false);

  const [message, setMessage] =
    useState("");

  const [success, setSuccess] =
    useState(false);

  const presets =
    [100, 500, 1000, 5000];

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault();

    const numericAmount =
      Number(amount);

    if (
      !Number.isInteger(numericAmount) ||
      numericAmount < 1 ||
      numericAmount > 100000
    ) {
      setSuccess(false);
      setMessage(
        "Số xu phải từ 1 đến 100.000.",
      );
      return;
    }

    if (!reason.trim() || reason.length > 200) {
      setSuccess(false);
      setMessage(
        "Nhập lý do từ 1 đến 200 ký tự.",
      );
      return;
    }

    const summary = [
      "Cộng xu cho " + recipient,
      "Số dư dự kiến: " + balance.toLocaleString("vi-VN") + " → " + (balance + numericAmount).toLocaleString("vi-VN") + " xu",
      "Lý do: " + reason.trim(),
    ];
    if (!window.confirm(summary.join("\n"))) return;

    setSubmitting(true);
    setSuccess(false);
    setMessage("");

    try {
      const response =
        await fetch(
          `/api/bff/admin/users/${encodeURIComponent(userId)}/wallet/credit`,
          {
            method: "POST",
            headers: {
              "Content-Type":
                "application/json",
            },
            body: JSON.stringify({
              amount: numericAmount,
              reason,
            }),
          },
        );

      const payload =
        (await response.json()) as GrantCreditsResponse;

      if (
        !response.ok ||
        !payload.success ||
        !payload.data
      ) {
        setSuccess(false);
        setMessage(
          payload.message ||
            "Không thể cộng xu.",
        );
        return;
      }

      setBalance(
        payload.data.balanceAfter,
      );

      setSuccess(true);

      setMessage(
        `Đã cộng ${payload.data.amount.toLocaleString("vi-VN")} xu. Số dư mới: ${payload.data.balanceAfter.toLocaleString("vi-VN")} xu.`,
      );

      router.refresh();
    } catch {
      setSuccess(false);
      setMessage(
        "Không thể kết nối máy chủ.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-5 rounded-lg border border-white/10 bg-black/20 p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="mt-1 text-lg font-semibold">
            Cộng xu cho người dùng
          </h2>
        </div>

        <div className="text-right">
          <p className="muted text-sm">Số dư hiện tại</p>

          <p className="mt-1 text-xl font-semibold text-emerald-300">
            {balance.toLocaleString("vi-VN")} xu
          </p>
        </div>
      </div>

      <form
        className="mt-5 grid gap-4"
        onSubmit={handleSubmit}
      >
        <div>
          <p className="mb-2 text-sm">Chọn nhanh số xu</p>

          <div className="flex flex-wrap gap-2">
            {presets.map((preset) => (
              <button
                className="button button-secondary"
                key={preset}
                onClick={() =>
                  setAmount(
                    String(preset),
                  )
                }
                type="button"
              >
                +{preset.toLocaleString("vi-VN")} xu
              </button>
            ))}
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="grid gap-2">
            <span>Số xu</span>

            <input
              className="input"
              max={100000}
              min={1}
              onChange={(event) =>
                setAmount(
                  event.target.value,
                )
              }
              required
              step={1}
              type="number"
              value={amount}
            />
          </label>

          <label className="grid gap-2">
            <span>Lý do</span>

            <input
              className="input"
              maxLength={200}
              onChange={(event) =>
                setReason(
                  event.target.value,
                )
              }
              placeholder="Nhập lý do cộng xu"
              required
              value={reason}
            />
          </label>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <button
            className="button"
            disabled={submitting}
            type="submit"
          >
            {submitting
              ? "Đang cộng…"
              : "Cộng xu"}
          </button>
        </div>

        {message && (
          <p
            className={
              success
                ? "text-sm text-emerald-300"
                : "text-sm text-rose-300"
            }
          >
            {message}
          </p>
        )}
      </form>
    </div>
  );
}
