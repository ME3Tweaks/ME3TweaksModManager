# Wavelist editor page

`wavelist.html` is the wavelist grid. It takes its data from `window.__WAVELIST__`, which the host
injects before navigation; the `fetch('sample-payload.json')` fallback exists so the page can be
opened on its own during development.

`sample-payload.json` is real output from `WavelistEditorPayload` against an installed LMP, checked in
so the page can be worked on without the game. Regenerate it rather than editing it by hand.

**Opening it from the filesystem does not work**, because `fetch` is blocked on `file://`. Either
serve the folder over HTTP, or build a self-contained copy with the payload inlined:

```
python -c "import io,json; h=io.open('wavelist.html',encoding='utf-8').read(); p=io.open('sample-payload.json',encoding='utf-8').read(); io.open('_local.html','w',encoding='utf-8').write(h.replace('<script>\n// The payload is injected','<script>window.__WAVELIST__=' + p + ';</script>\n<script>\n// The payload is injected',1))"
```

`_local.html` is throwaway - do not check it in, it goes stale against both files above.

## Hosting

WebView2 is already available: `ME3TweaksCoreWPF` references `Microsoft.Web.WebView2` and M3 gets it
transitively. Do not add a package reference to `MassEffectModManagerCore.csproj`.

Follow `ME3TweaksCoreWPF/LogViewer/MLogViewerControl` - embedded zip of assets extracted to temp,
served by intercepting `WebResourceRequested`, with its own path-traversal guard (the built-in one
went away with `SetVirtualHostNameToFolderMapping`). Inject the payload with
`AddScriptToExecuteOnDocumentCreatedAsync` rather than the log viewer's `ExecuteScriptAsync`-after-a-
sleep, which is a race; this page reads `window.__WAVELIST__` before anything else runs.

## Notes for whoever works on this next

- The grid is deliberately one screen: 11 waves x 4 difficulties, no navigation. The old app made
  this 308 separate page loads. See `../../docs/05-wavelists.md` section 4.
- `.gridscroll` scrolls in both axes and that is load-bearing. An overflow container becomes the
  containing block for `position: sticky` descendants, so the header row can only stick to the
  container - trying to pin it to the page header instead lands it on top of the first row.
- Difficulty labels fall back to the game's own token (`DO_Level5`) rather than inventing a name for
  a difficulty the page has not been told about. The count is data, never four.
- A cell shows `no budget` rather than a bar on the extraction wave and on objective waves, because
  the budget genuinely does not apply there.
- The filler slot is priced but not charged: it spends whatever budget is left.
