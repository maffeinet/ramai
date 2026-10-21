"use client";
import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError, isUuid, type Workspace } from "../lib/api-client";
import { AppShell, Notice, Pending } from "./ui";
import { useSession } from "./use-session";

export function WorkspaceView() {
  const session = useSession();
  const { profile, deny } = session;
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [checking, setChecking] = useState(true); const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null); const [name, setName] = useState("");
  const [workspaceId, setWorkspaceId] = useState(""); const [newName, setNewName] = useState("");
  const attempt = useRef<{ name: string; id: string } | null>(null);
  useEffect(() => {
    if (!profile) return;
    const controller = new AbortController();
    api.workspace(controller.signal).then(value => { setWorkspace(value); setName(value.name); }).catch(failure => {
      if (controller.signal.aborted) return;
      setWorkspace(null);
      if (failure instanceof ApiError && failure.status === 409) return;
      setError(failure); if (failure instanceof ApiError && failure.status === 401) deny(failure);
    }).finally(() => { if (!controller.signal.aborted) setChecking(false); });
    return () => controller.abort();
  }, [profile, deny]);
  async function open(event: FormEvent, create: boolean) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setWorkspace(null); setError(null);
    try {
      let id = workspaceId.trim();
      if (create) {
        const proposed = newName.trim();
        if (!attempt.current || attempt.current.name !== proposed) attempt.current = { name: proposed, id: crypto.randomUUID() };
        id = (await api.create(proposed, attempt.current.id)).workspaceId;
      } else if (!isUuid(id)) throw new ApiError(400);
      await api.select(id);
      const value = await api.workspace(); setWorkspace(value); setName(value.name); setWorkspaceId("");
    } catch (failure) { setError(failure); if (failure instanceof ApiError && failure.status === 401) session.deny(failure); }
    finally { setBusy(false); setChecking(false); }
  }
  async function rename(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(null);
    try { setWorkspace(await api.rename(name)); }
    catch (failure) { setWorkspace(null); setError(failure); if (failure instanceof ApiError && failure.status === 401) session.deny(failure); }
    finally { setBusy(false); }
  }
  const visible = session.profile ? workspace : null;
  return <AppShell section="workspace" onLogout={session.logout} busy={busy || session.loading}>
    <section className="page-heading"><p className="eyebrow">IL TUO PUNTO DI PARTENZA</p><h1>Area di lavoro</h1><p className="muted">Un’azienda alla volta. Permessi verificati, dati separati.</p></section>
    <Notice error={session.error || error} />{session.loading || (session.profile && checking) ? <Pending /> : null}
    {session.profile && !checking && <>
      {visible && <section className="workspace-card" aria-labelledby="workspace-title"><div className="card-heading"><span className="badge">Workspace attivo</span><span className="role-pill">{visible.role}</span></div>
        <h2 id="workspace-title">{visible.name}</h2><p className="muted">Questo è lo spazio aziendale attualmente autorizzato per la tua sessione.</p>
        <div className="workspace-id"><span>Codice workspace</span><code>{visible.workspaceId}</code></div>
        <div className="empty-feature"><span aria-hidden="true">↗</span><div><strong>Le fondamenta sono pronte.</strong><p>Assistente AI e attività quotidiane arriveranno nella prossima verticale. Qui non sono ancora attivi.</p></div></div>
        {(visible.role === "Owner" || visible.role === "Admin") && <form className="settings-form" onSubmit={rename}><fieldset disabled={busy}><label htmlFor="workspace-name">Nome workspace</label><div className="inline-form"><input id="workspace-name" value={name} maxLength={256} required onChange={event => setName(event.target.value)} /><button className="button-secondary" type="submit">Salva nome</button></div></fieldset></form>}
      </section>}
      <div className="cards-grid"><section className="panel" aria-labelledby="select-title"><h2 id="select-title">{visible ? "Cambia azienda" : "Apri un workspace"}</h2><p className="muted">Inserisci il codice di un’azienda di cui sei membro. Il codice da solo non concede accesso.</p>
        <form onSubmit={event => open(event, false)}><fieldset disabled={busy}><label htmlFor="workspace-code">Codice workspace</label><input id="workspace-code" value={workspaceId} autoComplete="off" maxLength={36} required onChange={event => setWorkspaceId(event.target.value)} /><button className="button-secondary" type="submit">Apri workspace</button></fieldset></form></section>
      <section className="panel" aria-labelledby="create-title"><h2 id="create-title">Un nuovo spazio</h2><p className="muted">Crea un workspace e diventa il suo primo Owner. Richiede una sessione verificata con MFA.</p>
        {session.profile.mfaVerified ? <form onSubmit={event => open(event, true)}><fieldset disabled={busy}><label htmlFor="new-workspace-name">Nome nuova azienda</label><input id="new-workspace-name" value={newName} maxLength={256} required onChange={event => setNewName(event.target.value)} /><button className="button-primary" type="submit">Crea e apri workspace</button></fieldset></form>
          : <Link className="button-secondary button-link" href="/security">Configura MFA prima di creare</Link>}</section></div>
    </>}
    <p className="page-foot">Area riservata · Nessuna azione su sistemi esterni</p>
  </AppShell>;
}
