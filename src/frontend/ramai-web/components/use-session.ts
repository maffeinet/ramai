"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, type Profile } from "../lib/api-client";

export function useSession() {
  const router = useRouter();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);
  const deny = useCallback((failure: unknown) => {
    setProfile(null); setError(failure);
    if (failure instanceof ApiError && failure.status === 401) router.replace("/login");
  }, [router]);
  useEffect(() => {
    let controller = new AbortController();
    const verify = () => {
      controller.abort(); controller = new AbortController();
      const signal = controller.signal;
      setLoading(true); setError(null); setProfile(null);
      api.profile(signal).then(value => { if (!signal.aborted) setProfile(value); })
        .catch(failure => { if (!signal.aborted) deny(failure); })
        .finally(() => { if (!signal.aborted) setLoading(false); });
    };
    const hide = () => { controller.abort(); setProfile(null); setLoading(true); };
    const show = (event: PageTransitionEvent) => { if (event.persisted) verify(); };
    verify(); window.addEventListener("pagehide", hide); window.addEventListener("pageshow", show);
    return () => { controller.abort(); window.removeEventListener("pagehide", hide); window.removeEventListener("pageshow", show); };
  }, [deny]);
  async function logout() {
    setProfile(null); setLoading(true); setError(null);
    try { await api.logout(); router.replace("/login"); }
    catch (failure) { deny(failure); setLoading(false); }
  }
  return { profile, error, loading, deny, logout };
}
