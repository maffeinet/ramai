import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";

export const metadata: Metadata = {
  title: "Agentilio · Area riservata",
  description: "Accesso sicuro al tuo spazio aziendale Agentilio.",
  robots: { index: false, follow: false },
};

type RootLayoutProps = Readonly<{
  children: ReactNode;
}>;

export default function RootLayout({ children }: RootLayoutProps) {
  return (
    <html lang="it">
      <body>{children}</body>
    </html>
  );
}
