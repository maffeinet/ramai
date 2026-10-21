"use client";
import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "../lib/api-client";
import { AppShell, Notice, Pending } from "./ui";
import { useSession } from "./use-session";

export function SecurityView() {
  const session = useSession();
  const [password, setPassword] = useState(""); const [code, setCode] = useState("");
  const [recovery, setRecovery] = useState(false); const [key, setKey] = useState<string | null>(null);
  const [codes, setCodes] = useState<string[] | null>(null); const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false); const [signInRequired, setSignInRequired] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const request = useRef(new AbortController());
  useEffect(() => {
    request.current = new AbortController();
    const clear = () => { request.current.abort(); setKey(null); setCodes(null); setPassword(""); setCode(""); setAcknowledged(false); };
    const restore = () => { request.current = new AbortController(); setBusy(false); };
    window.addEventListener("pagehide", clear);
    window.addEventListener("pageshow", restore);
    return () => { request.current.abort(); window.removeEventListener("pagehide", clear); window.removeEventListener("pageshow", restore); };
  }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(null);
    const signal = request.current.signal;
    try {
      if (key) {
        const result = await api.confirm({ password, code }, signal);
        if (signal.aborted) return;
        setKey(null); setCodes(result.recoveryCodes);
      } else {
        const result = await api.enroll({ password, ...(code ? recovery ? { recoveryCode: code } : { code } : {}) }, signal);
        if (signal.aborted) return;
        if (result.requiresSignIn) { setSignInRequired(true); setKey(null); }
        else if (result.sharedKey) setKey(result.sharedKey);
        else throw new ApiError(502);
      }
    } catch (failure) { if (!signal.aborted) { setError(failure); if (failure instanceof ApiError && failure.status === 401) { setKey(null); session.deny(failure); } } }
    finally { setPassword(""); setCode(""); setBusy(false); }
  }
  function leave() { setKey(null); setCodes(null); setPassword(""); setCode(""); session.logout(); }
  return <AppShell section="security" onLogout={leave} busy={busy || session.loading}>
    <section className="page-heading"><p className="eyebrow">UN PASSAGGIO IN PIÙ, PER PROTEGGERTI</p><h1>Sicurezza account</h1><p className="muted">MFA TOTP e recupero personale. Nessuna modifica ai permessi aziendali.</p></section>
    <Notice error={session.error || error} />{session.loading && <Pending />}
    {codes ? <section className="panel security-panel"><span className="badge">Mostrati una sola volta</span><h2>Conserva i codici di recupero</h2><p>La sessione è stata terminata. Salvali nel tuo password manager: ognuno può essere usato una sola volta. Non condividerli.</p>
      <ul className="recovery-codes" aria-label="Codici di recupero">{codes.map(value => <li key={value}><code>{value}</code></li>)}</ul>
      <label className="check-label"><input type="checkbox" checked={acknowledged} onChange={event => setAcknowledged(event.target.checked)} />Ho conservato i codici in un luogo sicuro</label>
      {acknowledged && <Link className="button-primary button-link" href="/login" onClick={() => setCodes(null)}>Torna al login</Link>}</section>
    : signInRequired ? <section className="panel security-panel"><h2>Verifica nuovamente l’accesso</h2><p>Per completare il recupero, accedi nuovamente e torna in questa pagina. Nessuna sessione viene aggiornata automaticamente.</p><Link href="/login" className="button-primary button-link">Torna al login</Link></section>
    : session.profile && <section className="panel security-panel"><span className="badge">{session.profile.mfaVerified ? "Sessione con MFA verificata" : "Configura il secondo fattore"}</span><h2>{key ? "Conferma l’autenticatore" : "Proteggi il tuo account"}</h2>
      {key ? <><p>Aggiungi un account TOTP nella tua app autenticatore, con nome Agentilio, e inserisci manualmente questa chiave. Non inviarla a servizi QR esterni.</p><div className="secret-box"><span>Chiave autenticatore</span><code data-testid="authenticator-key">{key}</code></div><p className="field-help">Configurazione in attesa: 10 minuti. Conferma con le sei cifre della nuova app e la password corrente.</p></>
        : <p className="muted">Richiede la password corrente. Se MFA è già attiva, serve anche un codice della vecchia app o un codice di recupero.</p>}
      <form onSubmit={submit}><fieldset disabled={busy}><label htmlFor="security-password">Password corrente</label><input id="security-password" type="password" autoComplete="current-password" value={password} maxLength={512} required onChange={event => setPassword(event.target.value)} />
      <label htmlFor="security-code">{key ? "Codice della nuova app" : recovery ? "Codice di recupero corrente" : "Codice autenticatore corrente (se già attivo)"}</label><input id="security-code" value={code} type="text" autoComplete="one-time-code" inputMode={recovery && !key ? "text" : "numeric"} required={Boolean(key)} maxLength={recovery && !key ? 64 : 6} onChange={event => setCode(event.target.value)} />
      {!key && <button type="button" className="text-button" onClick={() => { setRecovery(!recovery); setCode(""); }}>{recovery ? "Usa l’app autenticatore" : "Usa un codice di recupero"}</button>}
      <button className="button-primary" type="submit">{busy ? "Verifica in corso…" : key ? "Conferma MFA" : "Prepara autenticatore"}</button></fieldset></form>
      <p className="field-help">Chiave e codici non vengono salvati nel browser. Uscendo dalla pagina non saranno più visibili.</p></section>}
  </AppShell>;
}
