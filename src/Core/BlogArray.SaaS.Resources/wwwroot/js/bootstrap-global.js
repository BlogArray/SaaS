// Bootstrap 6 is distributed as an ES module and no longer creates a `bootstrap` global.
// site.js and the inline view scripts are classic scripts that call `bootstrap.Dialog`,
// `bootstrap.Drawer`, `bootstrap.Tooltip`, etc., so expose the module namespace globally.
// Module scripts execute before DOMContentLoaded, so code that runs at (or after) page
// ready can rely on window.bootstrap. Importing the bundle also wires up the data API.
//
// The ?v= query is part of the module URL on purpose: the bundle keeps the same path as the
// v5 UMD file, and a cached v5 copy imported as a module still runs (setting its own
// `bootstrap` global) but exports nothing. Bump it whenever the Bootstrap version changes.
import * as bootstrap from '../lib/bootstrap/dist/js/bootstrap.bundle.min.js?v=6.0.0-alpha.1';

if (typeof bootstrap.Dialog !== 'function') {
    console.error('bootstrap-global.js: the loaded Bootstrap bundle is not the expected v6 ES module. Clear the browser cache and verify wwwroot/lib/bootstrap/dist/js is current (npm run build:lib).');
}

window.bootstrap = bootstrap;
