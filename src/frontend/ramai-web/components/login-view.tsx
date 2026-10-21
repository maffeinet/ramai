"use client";
import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { api } from "../lib/api-client";
import { Brand, Notice } from "./ui";

export function LoginView() {
  const router = useRouter();
  const [email, setEmail] = useState(""); const [password, setPassword] = useState("");
  const [factor, setFactor] = useState(""); const [recovery, setRecovery] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(null);
    try { await api.login({ email, password, ...(factor ? recovery ? { recoveryCode: factor } : { code: factor } : {}) }); router.replace("/workspace"); }
    catch (failure) { setError(failure); }
    finally { setPassword(""); setFactor(""); setBusy(false); }
  }
  return <main className="login-shell"><section className="login-story"><Brand /><div><p className="eyebrow">MENO RUMORE. PIÙ SPAZIO.</p>
    <h1>Le tue attività.<br />Il tuo spazio.</h1><p className="story-copy">Un punto di partenza sicuro per il lavoro della tua azienda.</p></div>
    <p className="story-foot">Account personale · Aziende separate · Accesso verificato</p></section>
    <section className="login-side" aria-labelledby="login-title"><div className="login-card"><span className="badge">Bentornato</span><h2 id="login-title">Accedi ad Agentilio</h2>
    <p className="muted">Usa il tuo account verificato. L’accesso alle aziende viene controllato dal server.</p><Notice error={error} />
    <form onSubmit={submit}><fieldset disabled={busy}><label htmlFor="email">Email</label><input id="email" type="email" autoComplete="username" required maxLength={256} value={email} onChange={event => setEmail(event.target.value)} />
    <label htmlFor="password">Password</label><input id="password" type="password" autoComplete="current-password" required maxLength={512} value={password} onChange={event => setPassword(event.target.value)} />
    <label htmlFor="factor">{recovery ? "Codice di recupero" : "Codice autenticatore (se MFA attiva)"}</label><input id="factor" type="text" autoComplete="one-time-code" inputMode={recovery ? "text" : "numeric"} maxLength={recovery ? 64 : 6} value={factor} onChange={event => setFactor(event.target.value)} aria-describedby="factor-help" />
    <p id="factor-help" className="field-help">{recovery ? "Ogni codice di recupero può essere usato una sola volta." : "Inserisci le sei cifre della tua app autenticatore, se configurata."}</p>
    <button type="button" className="text-button" onClick={() => { setRecovery(!recovery); setFactor(""); }}>{recovery ? "Usa l’app autenticatore" : "Usa un codice di recupero"}</button>
    <button className="button-primary" type="submit">{busy ? "Verifica in corso…" : "Accedi"}</button></fieldset></form>
    <p className="login-note">Accesso riservato agli account già verificati. Registrazione e recupero email pubblico non sono ancora disponibili.</p></div></section></main>;
}
