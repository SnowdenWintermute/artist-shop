/**
 * Calls NotifyTabReturn each time this tab becomes visible again, e.g. after creating a series in
 * another tab.
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 */
export function watchTabReturn(dotNetReference) {
  function onVisibilityChange() {
    if (document.visibilityState !== "visible") {
      return;
    }

    dotNetReference
      .invokeMethodAsync("NotifyTabReturn")
      .catch((error) => console.error("NotifyTabReturn failed", error));
  }

  document.addEventListener("visibilitychange", onVisibilityChange);

  return {
    dispose() {
      document.removeEventListener("visibilitychange", onVisibilityChange);
    },
  };
}
