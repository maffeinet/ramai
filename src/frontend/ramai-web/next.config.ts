import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  async rewrites() {
    const configured = process.env.RAMAI_API_ORIGIN;
    if (!configured) return [];
    const origin = new URL(configured);
    if (origin.protocol !== "https:" || origin.username || origin.password || origin.pathname !== "/" || origin.search || origin.hash)
      throw new Error("RAMAI_API_ORIGIN must be a credential-free HTTPS origin.");
    return [{ source: "/api/v1/:path*", destination: `${origin.origin}/api/v1/:path*` }];
  },
  async headers() {
    return [{ source: "/:path*", headers: [
      { key: "Cache-Control", value: "no-store" }, { key: "Referrer-Policy", value: "no-referrer" },
      { key: "X-Content-Type-Options", value: "nosniff" }, { key: "X-Frame-Options", value: "DENY" },
      { key: "Content-Security-Policy", value: "frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'" },
    ] }];
  },
};

export default nextConfig;
