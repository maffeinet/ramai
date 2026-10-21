import Link from "next/link";
import type { ReactNode } from "react";
import { ApiError } from "../lib/api-client";

export function Brand() {
  return <Link className="brand" href="/workspace" aria-label="Agentilio, area di lavoro"><span className="brand-mark" aria-hidden="true">a</span>agentilio<span className="brand-dot">.</span></Link>;
}
export function Notice({ error }: { error: unknown }) {
  if (!error) return null;
  const status = error instanceof ApiError ? error.status : 0;
  const messages: Record<number, string> = {
    0: "Connessione non disponibile. Riprova quando il servizio è raggiungibile.",
    400: "Richiesta non accettata. Verifica i campi e riprova.",
    401: "Accesso non riuscito o sessione scaduta. Verifica credenziali e secondo fattore.",
    403: "Non hai i permessi richiesti o manca la verifica MFA. Nessun dato aziendale è stato caricato.",
    404: "La risorsa richiesta non è disponibile.", 409: "Operazione non disponibile nello stato corrente. Verifica il workspace e riprova.",
    429: "Troppi tentativi. Attendi prima di riprovare.",
    502: "Risposta del servizio non valida. Nessun accesso ai dati è stato confermato.",
    503: "Servizio temporaneamente non disponibile. Riprova più tardi.",
  };
  return <div className="notice notice-error" role="alert"><p>{messages[status] ?? "Operazione non completata. Riprova più tardi."}</p>
    {error instanceof ApiError && error.correlationId && <small>Riferimento: {error.correlationId}</small>}</div>;
}
export function Pending() { return <p className="pending" role="status">Verifica accesso in corso…</p>; }
export function AppShell({ children, section, onLogout, busy = false }: { children: ReactNode; section: "workspace" | "security"; onLogout: () => void; busy?: boolean }) {
  return <div className="app-shell"><aside className="sidebar"><Brand /><p className="nav-label">IL TUO SPAZIO</p>
    <nav aria-label="Navigazione principale"><Link href="/workspace" aria-current={section === "workspace" ? "page" : undefined}>Area di lavoro</Link>
      <Link href="/security" aria-current={section === "security" ? "page" : undefined}>Sicurezza account</Link></nav>
    <div className="sidebar-foot"><span className="security-dot" /> Sessione protetta<button className="button-quiet" onClick={onLogout} disabled={busy}>Esci</button></div></aside>
    <main className="app-main"><header className="app-top"><span>Agentilio / {section === "workspace" ? "Workspace" : "Sicurezza"}</span><span className="badge">Accesso riservato</span></header>{children}</main></div>;
}
