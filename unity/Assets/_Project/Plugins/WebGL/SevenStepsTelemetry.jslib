// Seven Steps telemetry bridge for WebGL (docs/architecture.md 11.5, docs/telemetry.md 4).
// C#: SevenSteps.Game.Telemetry.HttpTelemetry / TelemetryRunner. Import settings: WebGL only.
//   SevenSteps_RegisterVisibility(objectName)  listen to visibilitychange/pagehide and call back into C#:
//     hidden  -> SendMessage(objectName, "OnPageHidden", "hidden")
//     pagehide (when hidden was missed) -> SendMessage(objectName, "OnPageHidden", "unload")
//     visible again -> SendMessage(objectName, "OnPageVisible")
//   SevenSteps_SendBeacon(url, body) -> 1 if the browser queued it (text/plain, so no CORS preflight), else 0.
//   SevenSteps_RemoveUrlParam(name) -> drops ?name= from the address bar (history.replaceState), for one-time links (?newplayer=1).
// SendMessage runs C# synchronously, so session_end is queued and beaconed before the page goes away.
var SevenStepsTelemetryLib = {
  $SevenStepsTelemetryState: { name: null, hidden: false, ended: false },

  SevenSteps_RegisterVisibility__deps: ['$SevenStepsTelemetryState'],
  SevenSteps_RegisterVisibility: function (namePtr) {
    var st = SevenStepsTelemetryState;
    var first = st.name === null;
    st.name = UTF8ToString(namePtr);
    if (!first) return;
    function call(method, arg) {
      try {
        if (typeof SendMessage === 'function') {
          if (arg === undefined) SendMessage(st.name, method); else SendMessage(st.name, method, arg);
        } else if (typeof Module !== 'undefined' && Module.SendMessage) {
          if (arg === undefined) Module.SendMessage(st.name, method); else Module.SendMessage(st.name, method, arg);
        }
      } catch (e) { /* the game keeps working without telemetry */ }
    }
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') {
        if (st.hidden) return;
        st.hidden = true;
        call('OnPageHidden', 'hidden');
      } else if (st.hidden) {
        st.hidden = false;
        call('OnPageVisible');
      }
    });
    window.addEventListener('pagehide', function () {
      if (st.hidden || st.ended) return;
      st.ended = true;
      call('OnPageHidden', 'unload');
    });
  },

  SevenSteps_SendBeacon: function (urlPtr, bodyPtr) {
    try {
      if (!navigator.sendBeacon) return 0;
      var ok = navigator.sendBeacon(UTF8ToString(urlPtr), new Blob([UTF8ToString(bodyPtr)], { type: 'text/plain' }));
      return ok ? 1 : 0;
    } catch (e) { return 0; }
  },

  // Removes one query parameter from the address bar without reloading (history.replaceState): -newplayer is a
  // one-time facilitator link, so a reload must not start yet another player. 1 = removed or absent, 0 = failed.
  SevenSteps_RemoveUrlParam: function (namePtr) {
    try {
      var url = new URL(window.location.href);
      url.searchParams.delete(UTF8ToString(namePtr));
      window.history.replaceState(window.history.state, '', url.toString());
      return 1;
    } catch (e) { return 0; }
  },
};

autoAddDeps(SevenStepsTelemetryLib, '$SevenStepsTelemetryState');
mergeInto(LibraryManager.library, SevenStepsTelemetryLib);
