import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { test, expect } from "@playwright/test";
import { Brand, Notice, Pending } from "../../components/ui";
import { ApiError } from "../../lib/api-client";
test("error notice has an accessible alert and never formats a raw exception", () => {
  const html = renderToStaticMarkup(<Notice error={new Error("sensitive-detail")} />);
  expect(html).toContain('role="alert"'); expect(html).not.toContain("sensitive-detail");
});
test("server role denial is explained without implying UI grants access", () => {
  const html = renderToStaticMarkup(<Notice error={new ApiError(403, "synthetic-reference")} />);
  expect(html).toContain("Nessun dato aziendale"); expect(html).toContain("synthetic-reference");
});
test("pending state has a status role and brand links only to local workspace", () => {
  expect(renderToStaticMarkup(<Pending />)).toContain('role="status"');
  expect(renderToStaticMarkup(<Brand />)).toContain('href="/workspace"');
});
