From npm: `@floating-ui/dom` 1.8.0 and `@floating-ui/core` 1.8.0, their `dist/*.browser.min.mjs`
builds (MIT, `LICENSE`). The one change: the dom build's `from"@floating-ui/core"` is rewritten to
`from"./floating-ui.core.browser.min.mjs"`, since a browser can't resolve a package name.
