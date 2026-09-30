mergeInto(LibraryManager.library, {
  $RpsGoogleAuth: {
    active: null,
    loading: null,
    clientId: null,
    api: function () { return window.google && window.google.accounts && window.google.accounts.id; },
    load: function () {
      var self = RpsGoogleAuth;
      if (self.api()) return Promise.resolve(self.api());
      if (self.loading) return self.loading;
      self.loading = new Promise(function (resolve, reject) {
        var script = document.createElement('script');
        var done = false;
        var timer = setTimeout(function () { finish('GoogleUnavailable'); }, 15000);
        function finish(error) {
          if (done) return; done = true; clearTimeout(timer);
          script.onload = script.onerror = null;
          if (error) { try { script.remove(); } catch (_) {} reject(new Error(error)); }
          else resolve(self.api());
        }
        script.src = 'https://accounts.google.com/gsi/client'; script.async = true;
        script.onload = function () { finish(self.api() ? null : 'GoogleUnavailable'); };
        script.onerror = function () { finish('GoogleUnavailable'); };
        document.head.appendChild(script);
      }).catch(function (e) { self.loading = null; throw e; });
      return self.loading;
    },
    init: function (api, clientId) {
      var self = RpsGoogleAuth;
      if (self.clientId && self.clientId !== clientId) return false;
      if (self.clientId) return true;
      api.initialize({
        client_id: clientId,
        auto_select: true,
        ux_mode: 'popup',
        callback: function (response) {
          var a = self.active;
          if (!a || !response) return;
          if (!a.silent && response.state !== a.id) return;
          if (!response.credential || response.credential.length > 16384) return self.finish(a, '', 'GoogleInvalidCredential');
          self.finish(a, response.credential, '');
        }
      });
      self.clientId = clientId;
      return true;
    },
    cleanup: function (a) {
      if (!a) return;
      clearTimeout(a.timer);
      if (a.overlay) try { a.overlay.remove(); } catch (_) {}
    },
    finish: function (a, token, error) {
      var self = RpsGoogleAuth;
      if (self.active !== a) return;
      self.active = null; self.cleanup(a);
      var msg = JSON.stringify({ requestId: a.id, idToken: token || '', error: error || '' });
      setTimeout(function () { try { SendMessage(a.target, 'OnWebGoogleResult', msg); } catch (_) {} }, 0);
    },
    cancel: function (signOut) {
      var self = RpsGoogleAuth; var a = self.active; self.active = null; self.cleanup(a);
      var api = self.api();
      if (api) { try { api.cancel(); } catch (_) {} if (signOut) try { api.disableAutoSelect(); } catch (_) {} }
    },
    validate: function (clientId, a) {
      var host = window.location.hostname;
      if (window.location.protocol !== 'https:' && host !== 'localhost' && host !== '127.0.0.1') { this.finish(a, '', 'HttpsRequired'); return false; }
      if (!clientId || !/\.apps\.googleusercontent\.com$/.test(clientId)) { this.finish(a, '', 'GoogleConfiguration'); return false; }
      return true;
    },
    silent: function (clientId, target, id) {
      var self = RpsGoogleAuth;
      if (self.active) self.finish(self.active, '', 'UserCanceled');
      var a = { id: id, target: target, silent: true, overlay: null }; self.active = a;
      a.timer = setTimeout(function () { self.finish(a, '', 'SilentFailed'); }, 12000);
      if (!self.validate(clientId, a)) return;
      self.load().then(function (api) {
        if (self.active !== a || !self.init(api, clientId)) return self.finish(a, '', 'SilentFailed');
        try { api.prompt(function (n) {
          if (self.active !== a || !n) return;
          var unavailable = (n.isNotDisplayed && n.isNotDisplayed()) || (n.isSkippedMoment && n.isSkippedMoment()) || (n.isDismissedMoment && n.isDismissedMoment());
          if (unavailable) self.finish(a, '', 'SilentFailed');
        }); } catch (_) { self.finish(a, '', 'SilentFailed'); }
      }).catch(function () { self.finish(a, '', 'SilentFailed'); });
    },
    start: function (clientId, target, id, locale) {
      var self = RpsGoogleAuth;
      if (self.active) self.finish(self.active, '', 'UserCanceled');
      var a = { id: id, target: target, silent: false, overlay: null }; self.active = a;
      a.timer = setTimeout(function () { self.finish(a, '', 'GoogleTimeout'); }, 120000);
      if (!self.validate(clientId, a)) return;
      var overlay = document.createElement('div'); a.overlay = overlay;
      overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.72);padding:16px;box-sizing:border-box;';
      var panel = document.createElement('div'); panel.style.cssText = 'width:100%;max-width:380px;padding:24px;border-radius:20px;background:#fff;color:#172033;font:16px Arial,sans-serif;text-align:center;box-sizing:border-box;';
      var title = document.createElement('h2'); title.textContent = 'Entrar no RPS Arena'; title.style.cssText='font-size:20px;margin:0 0 20px;';
      var host = document.createElement('div'); host.style.cssText='display:flex;justify-content:center;min-height:44px;';
      var cancel = document.createElement('button'); cancel.type='button'; cancel.textContent='Cancelar'; cancel.style.cssText='margin-top:20px;min-height:48px;padding:8px 24px;'; cancel.onclick=function(){self.finish(a,'','UserCanceled');};
      panel.appendChild(title); panel.appendChild(host); panel.appendChild(cancel); overlay.appendChild(panel); document.body.appendChild(overlay);
      self.load().then(function(api){ if(self.active!==a || !self.init(api,clientId)) return self.finish(a,'','GoogleConfiguration'); api.renderButton(host,{type:'standard',theme:'outline',size:'large',text:'continue_with',state:a.id,locale:locale||'pt-BR',width:300}); }).catch(function(){self.finish(a,'','GoogleUnavailable');});
    }
  },

  RpsGoogleWebSignIn__deps: ['$RpsGoogleAuth'],
  RpsGoogleWebSignIn: function (clientId, target, requestId, locale) { RpsGoogleAuth.start(UTF8ToString(clientId), UTF8ToString(target), UTF8ToString(requestId), UTF8ToString(locale)); },
  RpsGoogleWebSignInSilent__deps: ['$RpsGoogleAuth'],
  RpsGoogleWebSignInSilent: function (clientId, target, requestId, locale) { RpsGoogleAuth.silent(UTF8ToString(clientId), UTF8ToString(target), UTF8ToString(requestId)); },
  RpsGoogleWebCancel__deps: ['$RpsGoogleAuth'],
  RpsGoogleWebCancel: function () { RpsGoogleAuth.cancel(false); },
  RpsGoogleWebSignOut__deps: ['$RpsGoogleAuth'],
  RpsGoogleWebSignOut: function () { RpsGoogleAuth.cancel(true); },

  RpsWsConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try { if (window.__rpsSocket) window.__rpsSocket.close(); } catch (_) {}
    var ws = new WebSocket(url); window.__rpsSocket = ws;
    ws.onopen = function () { try { SendMessage('RpsArena', 'OnWsOpen', ''); } catch (_) {} };
    ws.onmessage = function (e) { try { SendMessage('RpsArena', 'OnWsMessage', String(e.data)); } catch (_) {} };
    ws.onclose = function (e) { try { SendMessage('RpsArena', 'OnWsClose', e.reason || ('code ' + e.code)); } catch (_) {} };
    ws.onerror = function () { try { SendMessage('RpsArena', 'OnWsClose', 'websocket error'); } catch (_) {} };
  },
  RpsWsSend: function (messagePtr) { var m=UTF8ToString(messagePtr); if(window.__rpsSocket && window.__rpsSocket.readyState===1) window.__rpsSocket.send(m); },
  RpsWsClose: function () { try { if(window.__rpsSocket) window.__rpsSocket.close(); } catch (_) {} window.__rpsSocket=null; }
});
