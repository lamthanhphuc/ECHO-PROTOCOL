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
}: {
  userId: string;
  currentBalance: number;
}) {
  const router = useRouter();

  const [amount, setAmount] =
    useState("1000");

  const [reason, setReason] =
    useState("Shop testing");

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
        "Credit phải từ 1 đến 100,000.",
      );
      return;
    }

    if (reason.length > 200) {
      setSuccess(false);
      setMessage(
        "Reason tối đa 200 ký tự.",
      );
      return;
    }

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
            "Không thể cộng credit.",
        );
        return;
      }

      setBalance(
        payload.data.balanceAfter,
      );

      setSuccess(true);

      setMessage(
        `Đã cộng ${payload.data.amount.toLocaleString()} CR. Balance mới: ${payload.data.balanceAfter.toLocaleString()} CR.`,
      );

      router.refresh();
    } catch {
      setSuccess(false);
      setMessage(
        "Không thể kết nối Backend.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-5 rounded-lg border border-white/10 bg-black/20 p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="eyebrow">
            Admin wallet control
          </p>

          <h2 className="mt-1 text-lg font-semibold">
            Grant Credits
          </h2>

          <p className="muted mt-1 text-sm">
            Dùng cho test Shop và kiểm tra purchase flow.
          </p>
        </div>

        <div className="text-right">
          <p className="eyebrow">
            Current balance
          </p>

          <p className="mt-1 text-xl font-semibold text-emerald-300">
            {balance.toLocaleString()} CR
          </p>
        </div>
      </div>

      <form
        className="mt-5 grid gap-4"
        onSubmit={handleSubmit}
      >
        <div>
          <p className="eyebrow mb-2">
            Quick grant
          </p>

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
                +{preset.toLocaleString()} CR
              </button>
            ))}
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="grid gap-2">
            <span className="eyebrow">
              Custom amount
            </span>

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
            <span className="eyebrow">
              Reason
            </span>

            <input
              className="input"
              maxLength={200}
              onChange={(event) =>
                setReason(
                  event.target.value,
                )
              }
              placeholder="Shop testing"
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
              ? "ĐANG CỘNG..."
              : "GRANT CREDITS"}
          </button>

          <span className="code text-xs">
            ADMIN_GRANT
          </span>
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